import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import struct
import tempfile
import unittest
from unittest import mock

TOOL = Path(__file__).resolve().parents[1] / "tools/native-platform-survey.py"
spec = importlib.util.spec_from_file_location("native_platform_survey", TOOL)
survey = importlib.util.module_from_spec(spec)
spec.loader.exec_module(survey)


def pe(managed=False, machine=0x8664):
    data = bytearray(0x800)
    data[:2] = b"MZ"
    struct.pack_into("<I", data, 0x3C, 0x80)
    data[0x80:0x84] = b"PE\0\0"
    struct.pack_into("<HHIIIHH", data, 0x84, machine, 1, 0, 0, 0, 240, 0x2000)
    struct.pack_into("<H", data, 0x98, 0x20B)
    struct.pack_into("<I", data, 0x98 + 108, 16)
    struct.pack_into("<8sIIIIIIHHI", data, 0x98 + 240, b".text", 0x600, 0x1000, 0x600, 0x200, 0, 0, 0, 0, 0)
    if managed:
        struct.pack_into("<II", data, 0x98 + 112 + 14 * 8, 0x1000, 72)
        struct.pack_into("<I", data, 0x200, 72)
        struct.pack_into("<II", data, 0x208, 0x1100, 0x100)
        at = 0x300
        data[at:at + 4] = b"BSJB"
        struct.pack_into("<I", data, at + 12, 4)
        data[at + 16:at + 20] = b"v4\0\0"
        struct.pack_into("<HH", data, at + 20, 0, 2)
        struct.pack_into("<II", data, at + 24, 0x40, 0x50)
        data[at + 32:at + 36] = b"#~\0\0"
        struct.pack_into("<II", data, at + 36, 0xA0, 0x20)
        data[at + 44:at + 56] = b"#Strings\0\0\0\0"
        struct.pack_into("<Q", data, at + 0x40 + 8, 1 << 32)
        struct.pack_into("<I", data, at + 0x40 + 24, 1)
        struct.pack_into("<IHHHHIHHH", data, at + 0x40 + 28, 0, 1, 3, 7, 9, 0, 0, 1, 0)
        data[at + 0xA0:at + 0xAB] = b"\0Example\0\0\0"
    return bytes(data)


def elf():
    data = bytearray(128)
    data[:7] = b"\x7fELF\x02\x01\x01"
    struct.pack_into("<HHIQQQIHHHHHH", data, 16, 3, 62, 1, 0, 64, 0, 0, 64, 56, 1, 0, 0, 0)
    struct.pack_into("<IIQQQQQQ", data, 64, 1, 5, 0, 0, 0, 128, 128, 4096)
    return bytes(data)


def macho():
    return struct.pack("<IIIIIIIIII", 0xFEEDFACF, 0x01000007, 3, 6, 1, 8, 0, 0, 0x1B, 8)


class NativePlatformSurveyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.base = Path(self.temp.name).resolve()
        self.root = self.base / "game"
        self.root.mkdir()
        self.output = self.base / "survey.json"

    def tearDown(self):
        self.temp.cleanup()

    def put(self, relative, data):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def complete(self):
        self.put("GameAssembly.dll", pe())
        for relative in survey.REQUIRED_ASSEMBLIES:
            self.put(relative, pe(managed=True))

    def run_cli(self, *extra):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return survey.main(["--game-root", str(self.root), "--output", str(self.output), *extra])

    def test_complete_windows_inventory_contains_versions_and_relative_paths_only(self):
        self.complete()
        with mock.patch.object(survey.platform, "system", return_value="Windows"), mock.patch.object(survey.platform, "machine", return_value="AMD64"):
            self.assertEqual(self.run_cli(), 0)
        result = json.loads(self.output.read_text())
        self.assertTrue(result["inventory_complete"])
        self.assertFalse(result["profile_readiness"])
        self.assertEqual(result["host"]["rid"], "win-x64")
        self.assertEqual(result["game_image"]["target_rid"], "win-x64")
        self.assertEqual(len(result["assemblies"]), len(survey.REQUIRED_ASSEMBLIES))
        self.assertTrue(all(row["assembly"] == {"name":"Example", "version":"1.3.7.9"} for row in result["assemblies"]))
        self.assertNotIn(str(self.base), self.output.read_text())
        self.assertEqual(result["native_game_execution"], "NOT RUN")
        self.assertGreater(len(result["missing_evidence"]), 3)

    def test_explicit_foreign_target_does_not_admit_host_or_image(self):
        self.complete()
        path = self.put("custom/libGameAssembly.so", elf())
        result = survey.survey(self.root, path)
        self.assertEqual(result["game_image"]["target_rid"], "linux-x64")
        self.assertEqual(result["game_image"]["path"], "custom/libGameAssembly.so")
        self.assertFalse(result["profile_readiness"])

    def test_known_linux_path_is_inspected(self):
        self.put("Polytopia_Data/Native/libGameAssembly.so", elf())
        self.assertEqual(survey.survey(self.root)["game_image"]["format"], "ELF64")

    def test_thin_and_universal_macho_x64_headers(self):
        path = self.put("image", macho())
        self.assertEqual(survey.inspect_file(path, native=True)["target_rid"], "osx-x64")
        data = bytearray(128)
        data[:28] = struct.pack(">IIIIIII", 0xCAFEBABE, 1, 0x01000007, 3, 64, len(macho()), 6)
        data[64:64 + len(macho())] = macho()
        path.write_bytes(data)
        result = survey.inspect_file(path, native=True)
        self.assertEqual(result["format"], "Mach-O universal")
        self.assertEqual(result["selected_slice"]["offset"], 64)

    def test_malformed_native_headers_are_rejected(self):
        malformed = [b"", b"MZ", b"untrusted", pe(managed=True)]
        for content, offset, replacement in ((pe(), 0x3C, b"\xff" * 4), (pe(), 0x84, b"\x4c\x01"),
            (elf(), 18, b"\xb7\0"), (elf(), 4, b"\x01"), (elf(), 32, b"\xff" * 8),
            (macho(), 4, b"\x0c\0\0\x01"), (macho(), 36, b"\0" * 4)):
            data = bytearray(content);data[offset:offset + len(replacement)] = replacement;malformed.append(bytes(data))
        for index, content in enumerate(malformed):
            with self.subTest(index=index):
                path = self.put("image", content)
                with self.assertRaises(survey.SurveyError):
                    survey.inspect_file(path, native=True)

    def test_universal_slice_outside_or_duplicate_is_rejected(self):
        data = bytearray(128)
        data[:28] = struct.pack(">IIIIIII", 0xCAFEBABE, 1, 0x01000007, 3, 64, 999, 6)
        path = self.put("image", data)
        with self.assertRaisesRegex(survey.SurveyError, "range"):
            survey.inspect_file(path, native=True)

    def test_missing_required_file_writes_incomplete_json_and_nonzero_status(self):
        self.put("GameAssembly.dll", pe())
        self.assertEqual(self.run_cli(), 2)
        result = json.loads(self.output.read_text())
        self.assertFalse(result["inventory_complete"])
        self.assertEqual(len(result["problems"]), len(survey.REQUIRED_ASSEMBLIES))
        self.assertFalse(result["profile_readiness"])

    def test_invalid_managed_metadata_is_explicit_not_silently_accepted(self):
        self.complete()
        self.put(survey.REQUIRED_ASSEMBLIES[0], b"not an assembly")
        result = survey.survey(self.root)
        self.assertFalse(result["inventory_complete"])
        self.assertEqual(result["problems"][0]["path"], survey.REQUIRED_ASSEMBLIES[0])

    def test_assembly_row_or_name_out_of_range_rejected(self):
        for offset, replacement in ((0x348, b"\0" * 8), (0x34A, b"\xff" * 6), (0x36E, b"\xff\xff")):
            data = bytearray(pe(managed=True));data[offset:offset + len(replacement)] = replacement
            with self.subTest(offset=offset), self.assertRaises(survey.SurveyError):
                survey.inspect_file(self.put("managed.dll", data))

    def test_out_of_root_image_is_rejected(self):
        outside = self.base / "outside.dll";outside.write_bytes(pe())
        for path in (outside, "../outside.dll"):
            with self.assertRaisesRegex(survey.SurveyError, "inside"):
                survey.survey(self.root, path)

    def create_link(self, link, target, directory=False):
        try:
            link.symlink_to(target, target_is_directory=directory)
        except OSError as error:
            if os.name == 'nt' and getattr(error, 'winerror', None) == 1314:
                self.skipTest('Windows account cannot create symbolic links (privilege 1314).')
            raise

    def test_windows_reparse_attribute_is_rejected(self):
        info = mock.Mock(st_mode=0o100644, st_file_attributes=0x400)
        self.assertTrue(survey.linked(info))
        info.st_file_attributes = 0
        self.assertFalse(survey.linked(info))

    def test_symlink_image_parent_and_root_are_rejected(self):
        path = self.put("GameAssembly.dll", pe())
        link = self.root / "linked.dll";self.create_link(link, path)
        with self.assertRaisesRegex(survey.SurveyError, "Symlink"):
            survey.survey(self.root, link)
        directory = self.root / "linked";self.create_link(directory, self.root, directory=True)
        with self.assertRaisesRegex(survey.SurveyError, "Symlink"):
            survey.survey(self.root, directory / "GameAssembly.dll")
        root_link = self.base / "rootlink";self.create_link(root_link, self.root, directory=True)
        with self.assertRaisesRegex(survey.SurveyError, "Symlink"):
            survey.survey(root_link)

    def test_symlink_required_assembly_does_not_read_target(self):
        self.complete()
        path = self.root / survey.REQUIRED_ASSEMBLIES[0]
        path.unlink();self.create_link(path, self.base / "private-account-file")
        result = survey.survey(self.root)
        self.assertFalse(result["inventory_complete"])
        self.assertIn("Symlink", result["problems"][0]["problem"])

    def test_ambiguous_or_unknown_default_image_requires_explicit_choice(self):
        with self.assertRaisesRegex(survey.SurveyError, "--game-image"):
            survey.survey(self.root)
        self.put("GameAssembly.dll", pe());self.put("libGameAssembly.so", elf())
        with self.assertRaisesRegex(survey.SurveyError, "Multiple"):
            survey.survey(self.root)
        self.assertEqual(survey.survey(self.root, "GameAssembly.dll")["game_image"]["target_rid"], "win-x64")

    def test_saves_logs_config_and_unlisted_mods_are_not_collected(self):
        self.complete()
        for relative in ("saves/private", "BepInEx/LogOutput.log", "BepInEx/config/BepInEx.cfg", "BepInEx/plugins/Other.dll"):
            self.put(relative, b"PRIVATE_SECRET")
        before = {str(p.relative_to(self.root)):p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        result = survey.survey(self.root)
        self.assertNotIn("PRIVATE_SECRET", json.dumps(result))
        self.assertTrue(all(row["path"] in survey.REQUIRED_ASSEMBLIES + survey.OPTIONAL_ASSEMBLIES for row in result["assemblies"]))
        self.assertEqual(before, {str(p.relative_to(self.root)):p.read_bytes() for p in self.root.rglob("*") if p.is_file()})

    def test_existing_output_and_game_root_output_are_never_written(self):
        self.complete();self.output.write_bytes(b"KEEP")
        self.assertEqual(self.run_cli(), 2);self.assertEqual(self.output.read_bytes(), b"KEEP")
        self.output = self.root / "output.json"
        self.assertEqual(self.run_cli(), 2);self.assertFalse(self.output.exists())

    def test_output_symlink_and_symlink_parent_are_rejected(self):
        self.complete();target = self.base / "keep";target.write_bytes(b"KEEP")
        self.create_link(self.output, target)
        self.assertEqual(self.run_cli(), 2);self.assertEqual(target.read_bytes(), b"KEEP")
        link = self.base / "outlink";self.create_link(link, self.base, directory=True)
        self.output = link / "fresh.json"
        self.assertEqual(self.run_cli(), 2);self.assertFalse((self.base / "fresh.json").exists())

    def test_actual_mac_inventory_when_explicitly_configured(self):
        game = os.environ.get("DECEPTION_SURVEY_TEST_GAME_ROOT")
        if not game:
            self.skipTest("Actual installation read requires DECEPTION_SURVEY_TEST_GAME_ROOT.")
        result = survey.survey(game)
        self.assertTrue(result["inventory_complete"], result["problems"])
        self.assertEqual(result["game_image"]["target_rid"], "osx-x64")
        self.assertFalse(result["profile_readiness"])
        self.assertTrue(any(row["path"] == "BepInEx/plugins/PolyMod.dll" and row["assembly"]["version"] == "1.3.0.0" for row in result["assemblies"]))


if __name__ == "__main__":
    unittest.main(verbosity=2)
