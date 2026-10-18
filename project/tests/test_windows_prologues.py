"""Synthetic instruction-boundary checks; no game/runtime inputs."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('audit', Path(__file__).parents[1] / 'tools/audit_windows_prologues.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class Prologues(unittest.TestCase):
    def test_known_hazards(self):
        for code in ('750490909090', '0f8500000000', 'eb0490909090', 'c39090909090'):
            with self.subTest(code=code):
                self.assertTrue(audit.inspect(bytes.fromhex(code), 0x1000)[1])

    def test_immediate_is_not_an_opcode(self):
        self.assertEqual(audit.inspect(bytes.fromhex('b87500000090'), 0x1000), (6, []))

    def test_whole_instructions_only(self):
        self.assertEqual(audit.inspect(bytes.fromhex('40534883ec20'), 0x1000), (6, []))
        self.assertEqual(audit.inspect(bytes.fromhex('48895c240857'), 0x1000), (6, []))

    def test_branch_after_window_excluded(self):
        self.assertEqual(audit.inspect(bytes.fromhex('48895c2408577504'), 0x1000), (6, []))

    def test_truncated_input(self):
        self.assertIn('incomplete-decode', audit.inspect(bytes.fromhex('4889'), 0x1000)[1])


if __name__ == '__main__':
    unittest.main()
