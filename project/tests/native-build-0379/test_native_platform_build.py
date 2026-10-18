"""Offline recipe/pack rejection tests. Synthetic headers never execute native code."""
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

TOOL = Path(__file__).resolve().parents[2] / "tools/native-platform-build.py"
SPEC = importlib.util.spec_from_file_location("native_platform_build", TOOL)
N = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(N)


def header(rid):
    raw = bytearray(256)
    if rid == "osx-x64":
        struct.pack_into("<IIII", raw, 0, 0xFEEDFACF, 0x01000007, 3, 6)
    elif rid == "linux-x64":
        raw[:7] = b"\x7fELF\x02\x01\x01"
        struct.pack_into("<HH", raw, 16, 3, 62)
    else:
        raw[:2] = b"MZ"
        struct.pack_into("<I", raw, 0x3C, 64)
        raw[64:68] = b"PE\0\0"
        struct.pack_into("<H", raw, 68, 0x8664)
        struct.pack_into("<H", raw, 86, 0x2000)
        struct.pack_into("<H", raw, 88, 0x20B)
    return raw


class NativeBuildTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        # macOS /var and /tmp may be symlinks; tests start at their real root.
        self.root = Path(self.tmp.name).resolve()
        self.expected = N.contract()["bridges"]

    def tearDown(self):
        self.tmp.cleanup()

    def denied(self, call):
        with self.assertRaises(N.BuildError):
            call()

    def pack(self, rid="osx-x64"):
        path = self.root / "pack"
        path.mkdir()
        data = {"schema": 1, "purpose": "deception-native-build-pack", "runtimeAdmitted": False,
                "rid": rid, "artifacts": {}, "nativeTests": {k: self.expected[k]["testSummary"] for k in N.KINDS}}
        for kind in N.KINDS:
            relative = f"runtimes/{rid}/native/{N.filename(kind, rid)}"
            file = path / relative
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(header(rid))
            checksum = N.digest(file)
            Path(str(file) + ".sha256").write_text(checksum + "\n")
            data["artifacts"][kind] = {"path": relative, "bytes": file.stat().st_size, "sha256": checksum,
                "format": N.binary_header(file.read_bytes(), rid),
                "exports": sorted(self.expected[kind]["prefix"] + x for x in self.expected[kind]["exports"])}
        self.write_manifest(path, data)
        return path, data

    @staticmethod
    def write_manifest(path, data):
        (path / "native-pack.json").write_text(json.dumps(data))

    def junit(self):
        root = ET.Element("testsuite")
        for kind in N.KINDS:
            c = ET.SubElement(root, "testcase", name=kind + "-bridge", status="run")
            ET.SubElement(c, "system-out").text = self.expected[kind]["testSummary"] + "\n"
        path = self.root / "ctest.xml"
        ET.ElementTree(root).write(path)
        return path, root

    def test_three_native_filenames(self):
        self.assertEqual(N.filename("state", "win-x64"), "deception-state-bridge.dll")
        self.assertEqual(N.filename("execution", "linux-x64"), "libdeception-execution-bridge.so")
        self.assertEqual(N.filename("command", "osx-x64"), "libdeception-command-bridge.dylib")

    def test_foreign_host_rejected(self):
        self.denied(lambda: N.target_host("win-x64", "Darwin", "arm64"))

    def test_unknown_target_rejected(self):
        self.denied(lambda: N.target_host("win-arm64", "Windows", "arm64"))

    def test_32bit_host_rejected(self):
        self.denied(lambda: N.target_host("linux-x64", "Linux", "i686"))

    def test_native_x64_hosts(self):
        for rid, system in N.SYSTEMS.items():
            N.target_host(rid, system, "AMD64")

    def test_mac_arm_host_requires_actual_x64_build_and_execution(self):
        N.target_host("osx-x64", "Darwin", "arm64")
        self.assertEqual(N.binary_header(header("osx-x64"), "osx-x64"), "Mach-O64/x86-64")

    def test_all_header_formats(self):
        for rid in N.SYSTEMS:
            self.assertTrue(N.binary_header(header(rid), rid))

    def test_wrong_os_header_rejected(self):
        self.denied(lambda: N.binary_header(header("linux-x64"), "win-x64"))

    def test_macho_arm_rejected(self):
        raw = header("osx-x64"); struct.pack_into("<I", raw, 4, 0x0100000C)
        self.denied(lambda: N.binary_header(raw, "osx-x64"))

    def test_elf_arm_rejected(self):
        raw = header("linux-x64"); struct.pack_into("<H", raw, 18, 183)
        self.denied(lambda: N.binary_header(raw, "linux-x64"))

    def test_pe_arm_rejected(self):
        raw = header("win-x64"); struct.pack_into("<H", raw, 68, 0xAA64)
        self.denied(lambda: N.binary_header(raw, "win-x64"))

    def test_pe_executable_not_library_rejected(self):
        raw = header("win-x64"); struct.pack_into("<H", raw, 86, 0)
        self.denied(lambda: N.binary_header(raw, "win-x64"))

    def test_pe_out_of_range_offset(self):
        raw = header("win-x64"); struct.pack_into("<I", raw, 0x3C, 0xFFFFFFFF)
        self.denied(lambda: N.binary_header(raw, "win-x64"))

    def test_empty_truncated_and_oversized_rejected(self):
        for raw in (b"", b"MZ", bytes(N.MAX_BINARY + 1)):
            self.denied(lambda: N.binary_header(raw, "win-x64"))

    def test_exports_macho(self):
        self.assertEqual(N.parse_exports("0000000000001234 T _dcp_foo\n", "osx-x64"), ["dcp_foo"])

    def test_exports_elf(self):
        self.assertEqual(N.parse_exports("0000000000001234 T dcp_foo\n", "linux-x64"), ["dcp_foo"])

    def test_exports_pe(self):
        self.assertEqual(N.parse_exports("  ordinal hint RVA      name\n        1    0 00001234 dcp_foo\n  Summary\n", "win-x64"), ["dcp_foo"])

    def test_forwarded_pe_export_rejected(self):
        self.denied(lambda: N.parse_exports(" 1 0 00001234 dcp_foo = another.foo\n", "win-x64"))

    def test_duplicate_exports_rejected(self):
        self.denied(lambda: N.parse_exports("0001 T dcp_a\n0002 T dcp_a\n", "linux-x64"))

    def test_windows_dynamic_crt_dependencies(self):
        self.assertEqual(len(N.parse_dependencies(" KERNEL32.dll\n MSVCP140.dll\n VCRUNTIME140.dll\n api-ms-win-crt-runtime-l1-1-0.dll\n", "win-x64", "x")), 4)

    def test_windows_debug_crt_rejected(self):
        self.denied(lambda: N.parse_dependencies(" VCRUNTIME140D.dll\n", "win-x64", "x"))

    def test_game_dependency_rejected(self):
        self.denied(lambda: N.parse_dependencies(" GameAssembly.dll\n", "win-x64", "x"))

    def test_linux_native_dependencies(self):
        self.assertEqual(N.parse_dependencies("0x1 (NEEDED) Shared library: [libstdc++.so.6]\n", "linux-x64", "x"), ["libstdc++.so.6"])

    def test_linux_absolute_search_path_rejected(self):
        self.denied(lambda: N.parse_dependencies("0x1 (NEEDED) Shared library: [libc.so.6]\n0x1 (RUNPATH) [/tmp/build]\n", "linux-x64", "x"))

    def test_mac_relocated_install_name(self):
        text = "x:\n @rpath/x (compatibility version 0)\n /usr/lib/libc++.1.dylib (compatibility version 1)\n /usr/lib/libSystem.B.dylib (compatibility version 1)\n"
        self.assertEqual(len(N.parse_dependencies(text, "osx-x64", "x")), 2)
        self.denied(lambda: N.parse_dependencies(text.replace("@rpath/x", "/tmp/build/x"), "osx-x64", "x"))

    def test_pack_all_platforms_remain_unadmitted(self):
        for rid in N.SYSTEMS:
            path, _ = self.pack(rid)
            self.assertIs(N.audit_pack(path)["runtimeAdmitted"], False)
            import shutil
            shutil.rmtree(path)

    def test_pack_rejects_claimed_runtime_admission(self):
        path, data = self.pack(); data["runtimeAdmitted"] = True; self.write_manifest(path, data)
        self.denied(lambda: N.audit_pack(path))

    def test_pack_tampered_binary_rejected(self):
        path, data = self.pack(); file = path / data["artifacts"]["state"]["path"]; raw = bytearray(file.read_bytes()); raw[-1] ^= 1; file.write_bytes(raw)
        self.denied(lambda: N.audit_pack(path))

    def test_pack_sidecar_tamper_rejected(self):
        path, data = self.pack(); (path / (data["artifacts"]["state"]["path"] + ".sha256")).write_text("0" * 64 + "\n")
        self.denied(lambda: N.audit_pack(path))

    def test_pack_extra_external_dll_rejected(self):
        path, _ = self.pack(); (path / "PolyMod.dll").write_bytes(b"not permitted")
        self.denied(lambda: N.audit_pack(path))

    def test_pack_path_escape_rejected(self):
        path, data = self.pack(); data["artifacts"]["state"]["path"] = "../other.dll"; self.write_manifest(path, data)
        self.denied(lambda: N.audit_pack(path))

    def test_pack_missing_harness_evidence_rejected(self):
        path, data = self.pack(); del data["nativeTests"]["state"]; self.write_manifest(path, data)
        self.denied(lambda: N.audit_pack(path))

    def test_existing_output_refused_preserved(self):
        path = self.root / "existing"; path.mkdir(); (path / "keep").write_text("unchanged")
        self.denied(lambda: N.fresh(path)); self.assertEqual((path / "keep").read_text(), "unchanged")

    def test_linked_file_refused(self):
        actual = self.root / "actual"; actual.write_bytes(b"x"); link = self.root / "linked"
        try: link.symlink_to(actual)
        except OSError: self.skipTest("Host does not permit creating symlinks")
        self.denied(lambda: N.regular(link))

    def test_dangling_output_refused(self):
        link = self.root / "dangling"
        try: link.symlink_to(self.root / "absent")
        except OSError: self.skipTest("Host does not permit creating symlinks")
        self.denied(lambda: N.fresh(link))

    def test_tool_preserves_compiler_symlink_name(self):
        # Model a clang++ -> clang driver alias; its name controls C++ linking.
        actual = self.root / "clang"
        actual.write_text("driver placeholder")
        alias = self.root / "clang++"
        try: alias.symlink_to(actual)
        except OSError: self.skipTest("Host does not permit creating symlinks")
        with mock.patch.object(N.shutil, "which", return_value=str(alias)):
            self.assertEqual(N.tool("clang++"), str(alias))

    def test_complete_ctest_evidence(self):
        path, _ = self.junit(); self.assertEqual(len(N.ctest_result(path, self.expected)), 3)

    def test_truncated_ctest_output_rejected(self):
        path, root = self.junit(); root[0].find("system-out").text = "[truncated]"; ET.ElementTree(root).write(path)
        self.denied(lambda: N.ctest_result(path, self.expected))

    def test_failed_ctest_rejected_even_with_summary(self):
        path, root = self.junit(); ET.SubElement(root[0], "failure"); ET.ElementTree(root).write(path)
        self.denied(lambda: N.ctest_result(path, self.expected))

    def test_skipped_ctest_rejected(self):
        path, root = self.junit(); ET.SubElement(root[0], "skipped"); ET.ElementTree(root).write(path)
        self.denied(lambda: N.ctest_result(path, self.expected))

    def test_duplicate_ctest_rejected(self):
        path, root = self.junit(); root.append(root[0]); ET.ElementTree(root).write(path)
        self.denied(lambda: N.ctest_result(path, self.expected))


if __name__ == "__main__":
    unittest.main()
