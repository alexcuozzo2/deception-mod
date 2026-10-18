#!/usr/bin/env python3
"""Build/test/audit own native helpers on the target host. Never installs or admits a game profile."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import stat
import struct
import subprocess
import sys
import xml.etree.ElementTree as ET

PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT / "src/Native"
CONTRACT_PATH = SOURCE / "bridge-build-contract.json"
KINDS = ("state", "execution", "command")
SYSTEMS = {"osx-x64": "Darwin", "win-x64": "Windows", "linux-x64": "Linux"}
MAX_BINARY = 1024 * 1024 - 1  # Existing managed own-library extraction bound.


class BuildError(ValueError):
    pass


def require(ok, message):
    if not ok:
        raise BuildError(message)


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def regular(path):
    require(path.exists() and stat.S_ISREG(path.lstat().st_mode), f"Not a regular file: {path}")
    require(not path.is_symlink(), f"Linked file refused: {path}")
    return path


def no_links(path):
    path = Path(os.path.abspath(path.expanduser()))
    for part in (path, *path.parents):
        require(not part.is_symlink(), f"Linked path refused: {part}")
    return path


def fresh(path):
    path = no_links(path)
    require(not path.exists(), f"Refusing existing output: {path}")
    require(not path.is_relative_to(SOURCE), "Build/output must be outside native sources")
    return path


def contract():
    data = json.loads(regular(CONTRACT_PATH).read_text())
    require(data.get("schema") == 1 and set(data.get("bridges", {})) == set(KINDS), "Unknown bridge build contract")
    for kind, item in data["bridges"].items():
        require(item["prefix"] in ("dcp_state_bridge_", "dcp_exec_bridge_", "dcp_cmd_bridge_"), "Invalid export prefix")
        require(len(item["exports"]) == len(set(item["exports"])) and all(re.fullmatch(r"[a-z_]+", s) for s in item["exports"]), "Invalid export list")
    return data


def filename(kind, rid):
    require(rid in SYSTEMS and kind in KINDS, "Unknown RID/helper")
    return ("" if rid == "win-x64" else "lib") + f"deception-{kind}-bridge" + {"win-x64": ".dll", "osx-x64": ".dylib", "linux-x64": ".so"}[rid]


def target_host(rid, system=None, machine=None):
    require(rid in SYSTEMS, "Unknown target RID")
    system = platform.system() if system is None else system
    machine = platform.machine() if machine is None else machine
    require(SYSTEMS[rid] == system, "Build and CTest must run on the target OS; no foreign binaries or emulator guessed")
    allowed = {"x86_64", "amd64"}
    if system in ("Darwin", "Windows"):
        allowed |= {"arm64", "aarch64"}  # Target remains x64; actual execution must pass.
    require(machine.lower() in allowed, "Host cannot run this audited x64 build recipe")


def tool(name):
    path = shutil.which(str(name))
    require(path is not None, f"Required existing tool unavailable: {name}; no download attempted")
    # Preserve the selected executable name: clang++ can be a symlink to clang,
    # and the driver uses argv[0] to select C++ linking. This does not change the
    # separate no-links policy for source/build/pack artifact paths.
    return os.path.abspath(path)


def invoke(args, log=None):
    args = [str(x) for x in args]
    result = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    if log:
        log.write("$ " + json.dumps(args) + "\n" + result.stdout + "\n")
        log.flush()
    require(result.returncode == 0, f"Command failed ({result.returncode}): {args[0]}\n{result.stdout[-5000:]}")
    return result.stdout


def binary_header(raw, rid):
    """Reject wrong-format/architecture binaries without loading them."""
    require(0 < len(raw) <= MAX_BINARY, "Empty/oversized helper binary")
    if rid == "osx-x64":
        require(len(raw) >= 32, "Truncated Mach-O header")
        magic, cpu, _, kind = struct.unpack_from("<IIII", raw)
        require(magic == 0xFEEDFACF and cpu == 0x01000007 and kind == 6, "Expected thin x64 Mach-O dylib")
        return "Mach-O64/x86-64"
    if rid == "linux-x64":
        require(len(raw) >= 64 and raw[:7] == b"\x7fELF\x02\x01\x01", "Expected little-endian ELF64")
        kind, cpu = struct.unpack_from("<HH", raw, 16)
        require(kind == 3 and cpu == 62, "Expected x64 ELF shared library")
        return "ELF64/x86-64"
    require(rid == "win-x64", "Unknown target RID")
    require(len(raw) >= 64 and raw[:2] == b"MZ", "Expected PE image")
    offset = struct.unpack_from("<I", raw, 0x3C)[0]
    require(offset >= 64 and offset + 26 <= len(raw) and raw[offset:offset + 4] == b"PE\0\0", "Malformed PE header")
    cpu, = struct.unpack_from("<H", raw, offset + 4)
    chars, = struct.unpack_from("<H", raw, offset + 22)
    magic, = struct.unpack_from("<H", raw, offset + 24)
    require(cpu == 0x8664 and magic == 0x20B and chars & 0x2000, "Expected x64 PE32+ DLL")
    return "PE32+/AMD64"


def parse_exports(text, rid):
    names = []
    for line in text.splitlines():
        if rid == "win-x64":
            match = re.match(r"^\s*\d+\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]{8,16}\s+(\S+)(.*)$", line)
            if match:
                require(not match[2].strip(), "Forwarded/decorated PE export refused")
                names.append(match[1])
        elif line.strip():
            columns = line.split()
            require(len(columns) >= 2, "Malformed nm output")
            name = columns[-1]
            if rid == "osx-x64":
                require(name.startswith("_"), "Malformed Mach-O exported C name")
                name = name[1:]
            names.append(name)
    require(names and len(names) == len(set(names)), "Missing/duplicate exported symbols")
    return sorted(names)


def parse_dependencies(text, rid, name):
    if rid == "osx-x64":
        dependencies = [line.strip().split(" (", 1)[0] for line in text.splitlines()[1:] if line.strip()]
        require(dependencies.count("@rpath/" + name) == 1, "Unexpected dylib install name")
        dependencies.remove("@rpath/" + name)
        allowed = {"/usr/lib/libc++.1.dylib", "/usr/lib/libSystem.B.dylib"}
        require(set(dependencies) == allowed, "Unexpected Mac helper dependency")
    elif rid == "linux-x64":
        dependencies = re.findall(r"\(NEEDED\).*?\[([^\]]+)\]", text)
        allowed = {"libstdc++.so.6", "libgcc_s.so.1", "libc.so.6", "libm.so.6", "libpthread.so.0", "libc++.so.1", "libc++abi.so.1"}
        require(dependencies and set(dependencies) <= allowed, "Unexpected Linux helper dependency")
        require(not re.search(r"\((?:RPATH|RUNPATH)\)", text), "Library must not contain a build-machine search path")
    else:
        dependencies = re.findall(r"^\s*([A-Za-z0-9_.-]+\.dll)\s*$", text, re.MULTILINE | re.IGNORECASE)
        require(dependencies, "Missing PE dependency table")
        allowed = re.compile(r"(?:kernel32|vcruntime140(?:_1)?|msvcp140(?:_[12]|_atomic_wait|_codecvt_ids)?|ucrtbase|api-ms-win-crt-[a-z0-9-]+)\.dll", re.IGNORECASE)
        require(all(allowed.fullmatch(d) for d in dependencies), "Unexpected Windows helper dependency (dynamic release CRT required)")
    require(len(dependencies) == len(set(dependencies)), "Duplicate native dependencies")
    return sorted(dependencies)


def audit_binary(path, kind, rid, expected, log=None):
    path = regular(no_links(path))
    fmt = binary_header(path.read_bytes(), rid)
    if rid == "win-x64":
        inspector = tool("dumpbin")
        exports = invoke([inspector, "/NOLOGO", "/EXPORTS", path], log)
        deps = invoke([inspector, "/NOLOGO", "/DEPENDENTS", path], log)
    elif rid == "osx-x64":
        exports = invoke([tool("nm"), "-gU", path], log)
        deps = invoke([tool("otool"), "-L", path], log)
    else:
        exports = invoke([tool("nm"), "-D", "--defined-only", path], log)
        deps = invoke([tool("readelf"), "-d", path], log)
    names = parse_exports(exports, rid)
    require(names == sorted(expected["prefix"] + x for x in expected["exports"]), f"Unexpected {kind} export set")
    return {"sha256": digest(path), "bytes": path.stat().st_size, "format": fmt, "exports": names,
            "dependencies": parse_dependencies(deps, rid, filename(kind, rid))}


def ctest_result(path, expected):
    root = ET.parse(regular(path)).getroot()
    cases = root.findall(".//testcase")
    require(len(cases) == 3 and {x.get("name") for x in cases} == {k + "-bridge" for k in KINDS}, "CTest did not run exactly three required harnesses")
    summaries = {}
    for case in cases:
        kind = case.get("name").removesuffix("-bridge")
        output = case.findtext("system-out", "")
        require(case.find("failure") is None and case.find("error") is None and case.find("skipped") is None,
                f"CTest {kind} failed/skipped")
        wanted = expected[kind]["testSummary"]
        require(output.splitlines().count(wanted) == 1, f"Incomplete {kind} native test summary")
        summaries[kind] = wanted
    return summaries


def inputs():
    files = sorted(SOURCE.glob("*.cpp")) + [SOURCE / "deception-platform.h", SOURCE / "CMakeLists.txt", CONTRACT_PATH, Path(__file__).resolve()]
    files += sorted((PROJECT / "tests/native").glob("*-bridge-harness.cpp"))
    return {str(p.relative_to(PROJECT)): digest(regular(p)) for p in files}


def audit_pack(path):
    path = no_links(path)
    data = json.loads(regular(path / "native-pack.json").read_text())
    require(data.get("schema") == 1 and data.get("purpose") == "deception-native-build-pack" and data.get("runtimeAdmitted") is False,
            "Not an unadmitted own-helper build pack")
    rid = data.get("rid")
    require(rid in SYSTEMS and set(data.get("artifacts", {})) == set(KINDS), "Unknown pack RID/helper inventory")
    expected = contract()["bridges"]
    require(data.get("nativeTests") == {kind: expected[kind]["testSummary"] for kind in KINDS}, "Missing/incomplete native test evidence")
    seen = {"native-pack.json"}
    for kind in KINDS:
        item = data["artifacts"][kind]
        relative = f"runtimes/{rid}/native/{filename(kind, rid)}"
        require(item.get("path") == relative, "Unexpected pack path")
        binary = regular(no_links(path / relative))
        require(item.get("sha256") == digest(binary) and item.get("bytes") == binary.stat().st_size, "Pack binary digest/length mismatch")
        require(item.get("format") == binary_header(binary.read_bytes(), rid), "Pack format mismatch")
        require(item.get("exports") == sorted(expected[kind]["prefix"] + x for x in expected[kind]["exports"]), "Pack export inventory mismatch")
        require(regular(no_links(path / (relative + ".sha256"))).read_text() == item["sha256"] + "\n", "Pack digest sidecar mismatch")
        seen.update((relative, relative + ".sha256"))
    actual = set()
    for p in path.rglob("*"):
        require(not p.is_symlink(), "Linked pack entry")
        if p.is_file():
            actual.add(p.relative_to(path).as_posix())
        else:
            require(p.is_dir(), "Unexpected pack entry")
    require(actual == seen, "Unexpected/missing pack files")
    return data


def build(args):
    target_host(args.rid)
    work, out = fresh(args.build_dir), fresh(args.output)
    require(not work.is_relative_to(out) and not out.is_relative_to(work), "Build and pack directories must be disjoint")
    cmake = tool(args.cmake)
    ctest = tool(str(Path(cmake).with_name("ctest.exe" if os.name == "nt" else "ctest")))
    expected = contract()["bridges"]
    before = inputs()
    work.mkdir(parents=True, exist_ok=False)
    with (work / "build.log").open("w", encoding="utf-8") as log:
        cmake_version = invoke([cmake, "--version"], log).splitlines()[0]
        version = re.fullmatch(r"cmake version (\d+)\.(\d+)(?:\..*)?", cmake_version)
        require(version is not None and tuple(map(int, version.groups())) >= (3, 21), "CMake/CTest3.21+ required for JUnit evidence")
        configure = [cmake, "-S", SOURCE, "-B", work, "-DCMAKE_BUILD_TYPE=Release", "-DBUILD_TESTING=ON"]
        generator = args.generator
        if args.rid == "win-x64":
            generator = generator or "Visual Studio 17 2022"
            configure += ["-G", generator]
            if generator.startswith("Visual Studio "):
                configure += ["-A", "x64"]
        elif generator:
            configure += ["-G", generator]
        if args.cxx:
            configure += ["-DCMAKE_CXX_COMPILER=" + tool(args.cxx)]
        if args.rid == "osx-x64":
            configure += ["-DCMAKE_OSX_ARCHITECTURES=x86_64", "-DCMAKE_OSX_DEPLOYMENT_TARGET=11.0"]
        invoke(configure, log)
        invoke([cmake, "--build", work, "--config", "Release", "--parallel", str(args.jobs)], log)
        invoke([ctest, "--test-dir", work, "-C", "Release", "--output-on-failure", "--test-output-size-passed", "65536", "--output-junit", work / "ctest.xml"], log)
        summaries = ctest_result(work / "ctest.xml", expected)
        # CMake generates UTF-8, independently of the Windows ANSI code page.
        index = json.loads(regular(work / "artifact-index-Release.json").read_text(encoding="utf-8"))
        require(index.get("system") == SYSTEMS[args.rid] and index.get("pointerSize") == 8 and index.get("configuration") == "Release", "Configured target mismatch")
        require(set(index.get("bridges", {})) == set(KINDS), "Unexpected CMake artifact inventory")
        artifacts = {}
        for kind in KINDS:
            lib = no_links(Path(index["bridges"][kind]["library"]))
            require(lib.is_relative_to(work) and lib.name == filename(kind, args.rid), "CMake library escaped exact build/filename")
            artifacts[kind] = audit_binary(lib, kind, args.rid, expected[kind], log)
        require(before == inputs(), "Native source/recipe changed during build; refusing pack")
        # Every check completed before publishing a fresh pack. No external DLLs,
        # game blobs, loader files or unverified game seam metadata are copied.
        out.mkdir(parents=True, exist_ok=False)
        for kind, item in artifacts.items():
            relative = f"runtimes/{args.rid}/native/{filename(kind, args.rid)}"
            destination = out / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            with destination.open("xb") as stream:
                stream.write(Path(index["bridges"][kind]["library"]).read_bytes())
            with Path(str(destination) + ".sha256").open("x") as stream:
                stream.write(item["sha256"] + "\n")
            item["path"] = relative
        manifest = {"schema": 1, "purpose": "deception-native-build-pack", "runtimeAdmitted": False,
                    "rid": args.rid, "host": {"system": platform.system(), "machine": platform.machine()},
                    "toolchain": {"cmake": cmake_version, "compiler": index["compiler"], "compilerVersion": index["compilerVersion"],
                                  "configuration": "Release", "exceptions": "/EHs /EHc-; dynamic CRT" if args.rid == "win-x64" else "-fexceptions"},
                    "artifacts": artifacts, "nativeTests": summaries, "sourceSha256": before,
                    "gameExecution": "NOT RUN; mock C++ original/callback functions only",
                    "buildEvidence": {"directory": str(work), "logSha256": digest(work / "build.log"), "ctestSha256": digest(work / "ctest.xml")}}
        with (out / "native-pack.json").open("x") as stream:
            json.dump(manifest, stream, indent=2, sort_keys=True)
            stream.write("\n")
        audit_pack(out)
    print(json.dumps({"passed": True, "rid": args.rid, "pack": str(out), "harnesses": 3, "runtimeAdmitted": False}, indent=2))


def main(argv=None):
    require(sys.version_info >= (3, 11), "Python3.11+ required; no interpreter download attempted")
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    create = sub.add_parser("build", help="Configure/build/CTest/audit on the target OS, no downloads")
    create.add_argument("--rid", required=True, choices=sorted(SYSTEMS))
    create.add_argument("--build-dir", type=Path, required=True)
    create.add_argument("--output", type=Path, required=True)
    create.add_argument("--cmake", default="cmake")
    create.add_argument("--generator")
    create.add_argument("--cxx")
    create.add_argument("--jobs", type=int, default=2)
    audit = sub.add_parser("audit", help="Read-only portable pack hash/shape recheck; no binary loading")
    audit.add_argument("pack", type=Path)
    args = parser.parse_args(argv)
    if args.command == "build":
        require(1 <= args.jobs <= 32, "Jobs must be between1 and32")
        build(args)
    else:
        result = audit_pack(args.pack)
        print(json.dumps({"passed": True, "rid": result["rid"], "runtimeAdmitted": False}))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (BuildError, OSError, ValueError, KeyError, ET.ParseError) as error:
        print("Native build refused: " + str(error), file=sys.stderr)
        sys.exit(1)
