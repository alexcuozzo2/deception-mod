#!/usr/bin/env python3
"""Read-only, allowlisted native target inventory. Python 3 standard library only.

This does not launch a game, load a DLL, copy binaries, inspect saves/logs/config,
fetch dependencies, or declare platform compatibility. Its only write is the
explicit new --output JSON outside the game root.
"""
import argparse
import hashlib
import json
import os
import platform
from pathlib import Path
import stat
import struct
import sys

SCHEMA = "deception-native-target-survey-v1"
IMAGE_PATHS = (
    "GameAssembly.dll",
    "libGameAssembly.so",
    "Polytopia_Data/Native/libGameAssembly.so",
    "Polytopia.app/Contents/Frameworks/GameAssembly.dylib",
)
REQUIRED_ASSEMBLIES = (
    "BepInEx/plugins/PolyMod.dll",
    "BepInEx/core/BepInEx.Core.dll",
    "BepInEx/core/BepInEx.Unity.IL2CPP.dll",
    "BepInEx/core/Il2CppInterop.Runtime.dll",
    "BepInEx/core/Il2CppInterop.HarmonySupport.dll",
    "BepInEx/core/0Harmony.dll",
    "BepInEx/core/MonoMod.RuntimeDetour.dll",
    "BepInEx/core/MonoMod.Utils.dll",
    "BepInEx/interop/GameLogicAssembly.dll",
    "BepInEx/interop/PolytopiaAssembly.dll",
    "BepInEx/interop/Il2Cppmscorlib.dll",
    "BepInEx/interop/UnityEngine.CoreModule.dll",
    "BepInEx/interop/UnityEngine.ImageConversionModule.dll",
)
OPTIONAL_ASSEMBLIES = (
    "BepInEx/core/LibCpp2IL.dll",
    "BepInEx/core/Polymod.MacModuleBridge.dll",
    "BepInEx/core/Polymod.MacMetadataBridge.dll",
    "BepInEx/interop/Il2CppSystem.dll",
    "BepInEx/interop/Il2CppSystem.Net.Http.dll",
    "BepInEx/interop/Il2CppMicrosoft.AspNetCore.SignalR.Client.Core.dll",
    "BepInEx/interop/UnityEngine.AudioModule.dll",
    "BepInEx/interop/UnityEngine.UI.dll",
)
MISSING_EVIDENCE = (
    "Review exact target method entry points and original seam bytes.",
    "Validate native calling conventions, object layouts, and exception boundaries.",
    "Compile and verify target-native state, command, and execution helpers.",
    "Run target-native boundary tests and fresh save/load gameplay tests.",
    "Verify the supported PolyMod API, multiplayer transport, replay, and secrecy independently.",
    "Confirm the installed game product version; assembly versions alone do not identify it.",
)


class SurveyError(ValueError):
    pass


def linked(info):
    return stat.S_ISLNK(info.st_mode) or bool(getattr(info, "st_file_attributes", 0) & 0x400)


def ordinary_path(path, directory=False, missing=False):
    """Reject links/reparse points in the explicitly supplied path and parents."""
    path = Path(os.path.abspath(os.path.expanduser(str(path))))
    for item in reversed((path, *path.parents)):
        try:
            info = item.lstat()
        except FileNotFoundError:
            if item == path and missing:
                return path
            raise SurveyError("Missing path component.") from None
        if linked(info):
            raise SurveyError("Symlink/reparse paths are not supported.")
        want_dir = item != path or directory
        if not (stat.S_ISDIR(info.st_mode) if want_dir else stat.S_ISREG(info.st_mode)):
            raise SurveyError("Expected an ordinary directory or file.")
    return path


def within(root, supplied):
    path = Path(supplied)
    if not path.is_absolute():
        path = root / path
    path = Path(os.path.abspath(str(path)))
    try:
        path.relative_to(root)
    except ValueError:
        raise SurveyError("The game image must remain inside --game-root.") from None
    return ordinary_path(path)


class Reader:
    def __init__(self, path):
        path = ordinary_path(path)
        flags = os.O_RDONLY | getattr(os, "O_BINARY", 0) | getattr(os, "O_NOFOLLOW", 0)
        self.fd = os.open(path, flags)
        self.before = os.fstat(self.fd)
        if not stat.S_ISREG(self.before.st_mode):
            os.close(self.fd)
            raise SurveyError("Expected an ordinary binary file.")
        self.size = self.before.st_size

    def close(self):
        os.close(self.fd)

    def read(self, offset, count):
        if offset < 0 or count < 0 or count > 16 * 1024 * 1024 or offset + count > self.size:
            raise SurveyError("Truncated or out-of-bounds binary header.")
        os.lseek(self.fd, offset, os.SEEK_SET)
        data = bytearray()
        while len(data) < count:
            chunk = os.read(self.fd, count - len(data))
            if not chunk:
                raise SurveyError("Binary changed while being read.")
            data.extend(chunk)
        return bytes(data)

    def unpack(self, fmt, offset):
        return struct.unpack(fmt, self.read(offset, struct.calcsize(fmt)))

    def digest(self):
        os.lseek(self.fd, 0, os.SEEK_SET)
        digest = hashlib.sha256()
        count = 0
        while True:
            chunk = os.read(self.fd, 1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
            count += len(chunk)
        after = os.fstat(self.fd)
        if count != self.size or (after.st_size, after.st_mtime_ns, after.st_ino, after.st_dev) != (
            self.before.st_size, self.before.st_mtime_ns, self.before.st_ino, self.before.st_dev
        ):
            raise SurveyError("Binary changed while being surveyed; retry after updates finish.")
        return digest.hexdigest()


def pe_header(reader):
    if reader.read(0, 2) != b"MZ":
        raise SurveyError("Not a PE image.")
    pe = reader.unpack("<I", 0x3C)[0]
    if pe < 0x40 or pe > 16 * 1024 * 1024 or reader.read(pe, 4) != b"PE\0\0":
        raise SurveyError("Invalid PE signature or header offset.")
    machine, sections, _, _, _, optional_size, characteristics = reader.unpack("<HHIIIHH", pe + 4)
    if not 1 <= sections <= 96:
        raise SurveyError("Invalid PE section count.")
    optional = pe + 24
    magic = reader.unpack("<H", optional)[0]
    if magic not in (0x10B, 0x20B):
        raise SurveyError("Invalid PE optional header.")
    directory_start = optional + (112 if magic == 0x20B else 96)
    if optional_size < directory_start - optional:
        raise SurveyError("Truncated PE optional header.")
    reader.read(optional, optional_size)
    records = []
    for index in range(sections):
        row = reader.unpack("<8sIIIIIIHHI", optional + optional_size + index * 40)
        if row[4] + row[3] > reader.size:
            raise SurveyError("PE section exceeds the file.")
        records.append(row)
    return machine, magic, characteristics, optional, optional_size, directory_start, records


def native_header(reader):
    magic = reader.read(0, 4)
    if magic[:2] == b"MZ":
        machine, bits, flags, optional, optional_size, directories, _ = pe_header(reader)
        if machine != 0x8664 or bits != 0x20B or not flags & 0x2000:
            raise SurveyError("GameAssembly must be an x64 PE32+ DLL.")
        count = reader.unpack("<I", directories - 4)[0]
        if count > 14 and optional_size >= directories - optional + 15 * 8:
            if reader.unpack("<II", directories + 14 * 8) != (0, 0):
                raise SurveyError("GameAssembly must be native, not a managed CLI assembly.")
        return {"format": "PE32+", "architecture": "x64", "target_rid": "win-x64"}
    if magic in (b"\xca\xfe\xba\xbe", b"\xca\xfe\xba\xbf"):
        count = reader.unpack(">I", 4)[0]
        wide = magic[-1] == 0xBF
        if not 1 <= count <= 16:
            raise SurveyError("Invalid Mach-O universal architecture count.")
        slices = []
        table_end = 8 + count * (32 if wide else 20)
        for index in range(count):
            row = reader.unpack(">IIQQII" if wide else ">IIIII", 8 + index * (32 if wide else 20))
            cpu, _, offset, length, alignment = row[:5]
            if offset < table_end or length < 32 or offset + length > reader.size or alignment > 30 or offset % (1 << alignment):
                raise SurveyError("Invalid Mach-O universal slice range/alignment.")
            if any(offset < old_offset + old_length and old_offset < offset + length for _, old_offset, old_length in slices):
                raise SurveyError("Overlapping Mach-O universal slices.")
            slices.append((cpu, offset, length))
        targets = [row for row in slices if row[0] == 0x01000007]
        if len(targets) != 1:
            raise SurveyError("Universal GameAssembly must contain exactly one x64 slice.")
        _, base, length = targets[0]

        class SliceReader:
            size = length
            def read(self, offset, count):
                if offset < 0 or count < 0 or offset + count > self.size:
                    raise SurveyError("Mach-O slice header exceeds its slice.")
                return reader.read(base + offset, count)
            def unpack(self, fmt, offset):
                return struct.unpack(fmt, self.read(offset, struct.calcsize(fmt)))

        view = SliceReader()
        if view.read(0, 4) != b"\xcf\xfa\xed\xfe":
            raise SurveyError("Universal x64 slice is not a Mach-O64 image.")
        result = native_header(view)
        result.update(format="Mach-O universal", selected_slice={"offset":base, "bytes":length})
        return result
    if magic == b"\x7fELF":
        ident = reader.read(0, 16)
        if ident[4:7] != b"\x02\x01\x01":
            raise SurveyError("GameAssembly must be a little-endian ELF64 image.")
        kind, machine, version, _, phoff, _, _, ehsize, phsize, phcount, _, _, _ = reader.unpack("<HHIQQQIHHHHHH", 16)
        if kind != 3 or machine != 62 or version != 1 or ehsize != 64 or phsize != 56 or not 1 <= phcount <= 4096:
            raise SurveyError("GameAssembly must be an x64 ELF shared object with valid headers.")
        load = False
        for index in range(phcount):
            ptype, _, offset, _, _, file_size, memory_size, _ = reader.unpack("<IIQQQQQQ", phoff + index * phsize)
            if file_size > memory_size or offset + file_size > reader.size:
                raise SurveyError("ELF segment exceeds the file or memory size.")
            load |= ptype == 1
        if not load:
            raise SurveyError("ELF image has no loadable segment.")
        return {"format": "ELF64", "architecture": "x64", "target_rid": "linux-x64"}
    if magic == b"\xcf\xfa\xed\xfe":
        _, cpu, _, kind, commands, command_size, _, _ = reader.unpack("<IIIIIIII", 0)
        if cpu != 0x01000007 or kind != 6 or not 1 <= commands <= 4096 or command_size > 16 * 1024 * 1024:
            raise SurveyError("GameAssembly must be a thin x64 Mach-O dylib with valid headers.")
        reader.read(32, command_size)
        offset = 32
        for _ in range(commands):
            _, size = reader.unpack("<II", offset)
            if size < 8 or size % 8 or offset + size > 32 + command_size:
                raise SurveyError("Invalid Mach-O load command.")
            offset += size
        if offset != 32 + command_size:
            raise SurveyError("Mach-O load command size does not match.")
        return {"format": "Mach-O64", "architecture": "x64", "target_rid": "osx-x64"}
    raise SurveyError("Unsupported native image: expected thin x64 PE, ELF, or Mach-O.")


# ECMA-335 table sizes up to Assembly (32); only identity/version are returned.
CODES = {
    "tdr": (2, (2, 1, 27)), "hc": (2, (4, 8, 23)),
    "hca": (5, (6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44, 43)),
    "hfm": (1, (4, 8)), "hds": (2, (2, 6, 32)), "mrp": (3, (2, 1, 26, 6, 27)),
    "hs": (1, (20, 23)), "mdr": (1, (6, 10)), "mf": (1, (4, 6)),
    "cat": (3, (6, 10)), "rs": (2, (0, 26, 35, 1)),
}
TABLES = (
    (2,"s","g","g","g"),("rs","s","s"),(4,"s","s","tdr",("t",4),("t",6)),(("t",4),),
    (2,"s","b"),(("t",6),),(4,2,2,"s","b",("t",8)),(("t",8),),(2,2,"s"),(("t",2),"tdr"),
    ("mrp","s","b"),(2,"hc","b"),("hca","cat","b"),("hfm","b"),(2,"hds","b"),(2,4,("t",2)),
    (4,("t",4)),("b",),(("t",2),("t",20)),(("t",20),),(2,"s","tdr"),(("t",2),("t",23)),
    (("t",23),),(2,"s","b"),(2,("t",6),"hs"),(("t",2),"mdr","mdr"),("s",),("b",),
    (2,"mf","s",("t",26)),(4,("t",4)),(4,4),(4,),
)


def assembly_identity(reader):
    _, _, _, optional, optional_size, directories, sections = pe_header(reader)
    if reader.unpack("<I", directories - 4)[0] < 15 or optional_size < directories - optional + 15 * 8:
        raise SurveyError("Assembly has no CLI directory.")

    def offset(rva, length):
        for row in sections:
            if row[2] <= rva and rva + length <= row[2] + row[3]:
                return row[4] + rva - row[2]
        raise SurveyError("CLI metadata RVA lies outside file-backed sections.")

    cli_rva, cli_size = reader.unpack("<II", directories + 14 * 8)
    if not cli_rva or cli_size < 16:
        raise SurveyError("Assembly has no valid CLI directory.")
    cli = offset(cli_rva, 16)
    metadata_rva, metadata_size = reader.unpack("<II", cli + 8)
    if not 20 <= metadata_size <= 16 * 1024 * 1024:
        raise SurveyError("Assembly metadata size is invalid or exceeds the survey limit.")
    metadata = reader.read(offset(metadata_rva, metadata_size), metadata_size)

    def part(pos, count):
        if pos < 0 or count < 0 or pos + count > len(metadata):
            raise SurveyError("Truncated assembly metadata.")
        return metadata[pos:pos + count]

    def unpack(fmt, pos):
        return struct.unpack(fmt, part(pos, struct.calcsize(fmt)))

    if part(0, 4) != b"BSJB":
        raise SurveyError("Invalid CLI metadata signature.")
    version_length = unpack("<I", 12)[0]
    cursor = (16 + version_length + 3) & ~3
    _, stream_count = unpack("<HH", cursor)
    cursor += 4
    if not 1 <= stream_count <= 16:
        raise SurveyError("Invalid CLI stream count.")
    streams = {}
    for _ in range(stream_count):
        start, length = unpack("<II", cursor)
        cursor += 8
        name_bytes = part(cursor, min(32, len(metadata) - cursor))
        end = name_bytes.find(b"\0")
        if end < 0:
            raise SurveyError("Invalid CLI stream name.")
        try:
            name = name_bytes[:end].decode("ascii")
        except UnicodeDecodeError:
            raise SurveyError("Non-ASCII CLI stream name.") from None
        cursor = (cursor + end + 4) & ~3
        if name in streams:
            raise SurveyError("Duplicate CLI stream.")
        part(start, length)
        streams[name] = (start, length)
    table = streams.get("#~", streams.get("#-"))
    strings = streams.get("#Strings")
    if table is None or strings is None:
        raise SurveyError("Required CLI streams are missing.")
    start, length = table
    heap = part(start + 6, 1)[0]
    valid = unpack("<Q", start + 8)[0]
    cursor = start + 24
    rows = {}
    for index in range(64):
        if valid >> index & 1:
            rows[index] = unpack("<I", cursor)[0]
            cursor += 4
    if rows.get(32) != 1:
        raise SurveyError("Assembly metadata must contain one assembly identity.")

    def width(column):
        if isinstance(column, int):
            return column
        if isinstance(column, tuple):
            return 4 if rows.get(column[1], 0) >= 65536 else 2
        if column in ("s", "g", "b"):
            return 4 if heap & {"s":1,"g":2,"b":4}[column] else 2
        bits, targets = CODES[column]
        return 4 if max(rows.get(target, 0) for target in targets) >= 1 << (16 - bits) else 2

    for index, columns in enumerate(TABLES):
        cursor += rows.get(index, 0) * sum(width(column) for column in columns)
    row_size = 16 + width("b") + 2 * width("s")
    if cursor + row_size > start + length:
        raise SurveyError("Assembly row exceeds its CLI table stream.")
    version = unpack("<HHHH", cursor + 4)
    name_index = int.from_bytes(part(cursor + 16 + width("b"), width("s")), "little")
    if not 0 < name_index < strings[1]:
        raise SurveyError("Invalid assembly name index.")
    tail = part(strings[0] + name_index, min(1024, strings[1] - name_index))
    end = tail.find(b"\0")
    if end < 0:
        raise SurveyError("Unterminated assembly name.")
    try:
        name = tail[:end].decode("utf-8")
    except UnicodeDecodeError:
        raise SurveyError("Invalid assembly name encoding.") from None
    if not name or any(ord(c) < 32 for c in name):
        raise SurveyError("Invalid assembly identity.")
    return {"name": name, "version": ".".join(map(str, version))}


def inspect_file(path, native=False):
    reader = Reader(path)
    try:
        details = native_header(reader) if native else {"assembly": assembly_identity(reader)}
        return {"bytes": reader.size, "sha256": reader.digest(), **details}
    finally:
        reader.close()


def host_info():
    system = {"Windows":"win", "Linux":"linux", "Darwin":"osx"}.get(platform.system(), "unknown")
    machine = platform.machine().lower()
    architecture = {"amd64":"x64", "x86_64":"x64", "arm64":"arm64", "aarch64":"arm64", "i386":"x86", "i686":"x86", "x86":"x86"}.get(machine, "unknown")
    return {"os": system, "architecture": architecture, "rid": system + "-" + architecture,
            "python_pointer_bits": struct.calcsize("P") * 8}


def survey(game_root, game_image=None):
    root = ordinary_path(game_root, directory=True)
    if game_image is not None:
        image = within(root, game_image)
    else:
        candidates = []
        for relative in IMAGE_PATHS:
            candidate = root / relative
            if os.path.lexists(candidate):
                candidates.append(within(root, relative))
        if not candidates:
            raise SurveyError("No known GameAssembly path found; supply --game-image pointing inside --game-root.")
        if len(candidates) != 1:
            raise SurveyError("Multiple GameAssembly candidates found; choose one with --game-image.")
        image = candidates[0]
    try:
        native = inspect_file(image, native=True)
    except SurveyError as error:
        raise SurveyError("Game image: " + str(error)) from None
    native["path"] = image.relative_to(root).as_posix()
    files, problems = [], []
    for relative in REQUIRED_ASSEMBLIES + OPTIONAL_ASSEMBLIES:
        path = root / relative
        required = relative in REQUIRED_ASSEMBLIES
        if not os.path.lexists(path):
            if required:
                problems.append({"path": relative, "problem": "Required assembly missing."})
            continue
        try:
            detail = inspect_file(path)
            files.append({"path": relative, "required": required, **detail})
        except (SurveyError, OSError) as error:
            # Never serialize OSError messages, which may include personal paths.
            problem = str(error) if isinstance(error, SurveyError) else "Assembly could not be read."
            problems.append({"path": relative, "problem": problem})
    return {"schema": SCHEMA, "host": host_info(), "game_image": native, "assemblies": files,
            "inventory_complete": not problems, "problems": problems,
            "profile_readiness": False, "missing_evidence": list(MISSING_EVIDENCE),
            "native_game_execution": "NOT RUN", "network_access": "NOT USED",
            "scope": "Allowlisted binary identities and hashes only; no save, log, account, or config content collected."}


def write_result(output, root, result):
    path = Path(os.path.abspath(os.path.expanduser(str(output))))
    try:
        path.relative_to(root)
    except ValueError:
        pass
    else:
        raise SurveyError("--output must be outside --game-root.")
    ordinary_path(path.parent, directory=True)
    if os.path.lexists(path):
        raise SurveyError("--output already exists; choose a new JSON file.")
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_BINARY", 0) | getattr(os, "O_NOFOLLOW", 0)
    data = (json.dumps(result, indent=2, sort_keys=True) + "\n").encode("utf-8")
    fd = os.open(path, flags, 0o600)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(data)
    except BaseException:
        # The caller can inspect/remove any incomplete explicitly requested output.
        raise


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root", required=True, help="Existing Polytopia installation directory (not a save directory).")
    parser.add_argument("--game-image", help="Optional exact native image path, absolute or relative to --game-root, inside that root.")
    parser.add_argument("--output", required=True, help="New JSON filename outside the game root; existing files are never replaced.")
    args = parser.parse_args(argv)
    try:
        root = ordinary_path(args.game_root, directory=True)
        result = survey(root, args.game_image)
        write_result(args.output, root, result)
    except (SurveyError, OSError) as error:
        message = str(error) if isinstance(error, SurveyError) else "A supplied path could not be read or the new output could not be written."
        print("Survey stopped: " + message, file=sys.stderr)
        return 2
    if not result["inventory_complete"]:
        print("Inventory incomplete: see relative-path problems in the JSON. No platform profile was admitted.", file=sys.stderr)
        return 2
    print("Inventory written. This is evidence for review, not a supported-platform certification.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
