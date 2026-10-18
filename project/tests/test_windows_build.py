"""Windows reference filtering and compiler process lifecycle regressions."""
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'tools'))
import windows_build

spec = importlib.util.spec_from_file_location('survey_fixtures', ROOT/'tests/test_native_platform_survey.py')
fixtures = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixtures)


class WindowsBuildTests(unittest.TestCase):
    def test_native_dll_is_not_a_managed_reference(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory).resolve()/'coreclr.dll'
            path.write_bytes(fixtures.pe(managed=False))
            self.assertFalse(windows_build.managed_reference(fixtures.survey,path))

    def test_managed_dll_is_a_reference(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory).resolve()/'System.Runtime.dll'
            path.write_bytes(fixtures.pe(managed=True))
            self.assertTrue(windows_build.managed_reference(fixtures.survey,path))

    def test_truncated_dll_stops_instead_of_being_silently_skipped(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory).resolve()/'broken.dll'
            path.write_bytes(b'MZ')
            with self.assertRaises(fixtures.survey.SurveyError):
                windows_build.managed_reference(fixtures.survey,path)

    def test_timeout_kills_and_reaps_owned_compiler(self):
        api = mock.Mock()
        process = mock.Mock()
        process.communicate.side_effect = [subprocess.TimeoutExpired('dotnet',180), ('',None)]
        with mock.patch.object(windows_build.subprocess,'Popen',return_value=process):
            with self.assertRaises(subprocess.TimeoutExpired):
                windows_build.compiler_command(api,['dotnet','--list-sdks'])
        process.kill.assert_called_once_with()
        self.assertEqual(process.communicate.call_count,2)


if __name__ == '__main__': unittest.main()
