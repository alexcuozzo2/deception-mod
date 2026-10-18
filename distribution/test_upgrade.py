"""Exercise the Finder wrapper against a staged updater, never a real install."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


class UpgradeWrapperTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.mac = self.root / 'Extracted package with spaces' / 'Mac'
        self.mac.mkdir(parents=True)
        self.wrapper = self.mac / 'Upgrade Deception.command'
        shutil.copyfile(Path(__file__).with_name('MAC-UPGRADE.command'), self.wrapper)
        self.wrapper.chmod(0o755)
        self.setup = self.mac / 'Deception-Mac-Setup' / 'setup.sh'
        self.setup.parent.mkdir()

    def stage(self, status):
        self.setup.write_text('#!/bin/bash\n'
                              'printf "ARG=<%s>\\n" "$@"\n'
                              'printf "Updater result remains visible\\n"\n'
                              f'exit {status}\n')

    def run_wrapper(self, *args):
        return subprocess.run([str(self.wrapper), *args], cwd=self.root,
                              stdin=subprocess.DEVNULL, capture_output=True,
                              text=True, timeout=5)

    def test_success_from_unrelated_directory_and_forwarded_path(self):
        self.stage(0)
        result = self.run_wrapper('--game', '/Volumes/Friend Games/Polytopia')
        self.assertEqual(result.returncode, 0)
        self.assertIn('ARG=<update-deception>\nARG=<--game>\n'
                      'ARG=</Volumes/Friend Games/Polytopia>', result.stdout)
        self.assertIn('Upgrade complete.', result.stdout)
        self.assertNotIn('ARG=<launch>', result.stdout)
        self.assertNotIn('Press Return', result.stdout)

    def test_failure_status_and_message_survive(self):
        self.stage(23)
        result = self.run_wrapper()
        self.assertEqual(result.returncode, 23)
        self.assertIn('Updater result remains visible', result.stdout)
        self.assertIn('Upgrade did not complete.', result.stderr)
        self.assertNotIn('Upgrade complete.', result.stdout)

    def test_missing_updater_cannot_report_success(self):
        result = self.run_wrapper()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Upgrade did not complete.', result.stderr)
        self.assertNotIn('Upgrade complete.', result.stdout)

    def terminal_result(self, status, reply):
        self.stage(status)
        master, slave = os.openpty()
        try:
            child = subprocess.Popen([str(self.wrapper)], cwd=self.root,
                                     stdin=slave, stdout=subprocess.PIPE,
                                     stderr=subprocess.PIPE, text=True)
            os.write(master, reply)
            try:
                stdout, stderr = child.communicate(timeout=5)
            except BaseException:
                child.kill()
                child.communicate()
                raise
            self.assertIn('Press Return', stdout)
            return child.returncode, stdout, stderr
        finally:
            os.close(master)
            os.close(slave)

    def test_terminal_return_keeps_success(self):
        code, _, _ = self.terminal_result(0, b'\n')
        self.assertEqual(code, 0)

    def test_terminal_eof_keeps_failure(self):
        code, _, stderr = self.terminal_result(23, b'\x04')
        self.assertEqual(code, 23)
        self.assertIn('Upgrade did not complete.', stderr)


if __name__ == '__main__':
    unittest.main(verbosity=2)
