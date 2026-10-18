"""Named fixture receipts must retain every expected case and assertion."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('candidate_manage', ROOT / 'tools/manage.py')
manage = importlib.util.module_from_spec(spec)
spec.loader.exec_module(manage)


class CandidateReceiptTests(unittest.TestCase):
    def test_named_result_requires_exact_success_and_coverage(self):
        expected = json.loads((ROOT / 'tests/rogue-upgrade-0362/expected.json').read_text())
        result = dict(tests=len(expected['names']), assertions=expected['assertions'],
                      failed=0, names=expected['names'])
        manage.validate_candidate_0359(result, 'rogue-upgrade-0362')
        for field, value in (('failed', 1), ('tests', result['tests'] - 1),
                             ('assertions', result['assertions'] - 1),
                             ('names', result['names'][1:]), ('failed', None)):
            with self.subTest(field=field, value=value):
                with self.assertRaises(manage.Stop):
                    manage.validate_candidate_0359({**result, field: value}, 'rogue-upgrade-0362')
