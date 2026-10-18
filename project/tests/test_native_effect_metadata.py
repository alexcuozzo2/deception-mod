"""Pin effect names/numbers to real metadata, including ports used by support tests."""
import copy, json, os, sys, tempfile, unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import audit_native_api as audit

class NativeEffectMetadataTests(unittest.TestCase):
    def setUp(self):
        self.refs=Path(os.environ.get('DECEPTION_API_REFERENCE_DIR',str(Path.home()/'Library/Application Support/Steam/steamapps/common/The Battle of Polytopia/BepInEx/interop')))
        if not (self.refs/'GameLogicAssembly.dll').is_file():self.skipTest('Exact native reference unavailable')
        contract=json.loads((ROOT/'tools/native-wheel-api.json').read_text())
        self.contract={'reference_sha256':{name:contract['reference_sha256'][name] for name in {r['file'] for r in contract['enums']}},'methods':[],'enums':copy.deepcopy(contract['enums'])}

    def test_exact_native_effects_and_all_active_ports_agree(self):
        result=audit.check(self.refs,self.contract)
        values=result['native_enums_verified'][0]['values']
        self.assertEqual(values['Swift'],6)
        self.assertEqual(values['Petrified'],5)
        self.assertEqual(values['Charmed'],8)
        self.assertNotIn('Swarmed',values)
        self.assertGreaterEqual(len(result['fixture_enums_verified']),10)

    def test_invented_swarm_alias_is_rejected(self):
        values=self.contract['enums'][0]['values']
        values['Swarmed']=values.pop('Swift')
        with self.assertRaisesRegex(ValueError,'Native enum contract mismatch'):audit.check(self.refs,self.contract)

    def test_wrong_native_value_is_rejected(self):
        self.contract['enums'][0]['values']['Swift']=5
        with self.assertRaisesRegex(ValueError,'Native enum contract mismatch'):audit.check(self.refs,self.contract)

    def test_pending_move_witness_uses_native_action_numbers(self):
        result=audit.check(self.refs,self.contract)
        values=next(row['values'] for row in result['native_enums_verified'] if row['owner']=='ActionType')
        self.assertEqual((values['Move'],values['Hide'],values['Reveal']),(6,80,81))

    def test_synthetic_move_number_cannot_pass_native_guard(self):
        values=next(row['values'] for row in self.contract['enums'] if row['owner']=='ActionType')
        values['Move']=2
        with self.assertRaisesRegex(ValueError,'Native enum contract mismatch: ActionType'):audit.check(self.refs,self.contract)

    def test_permissive_fixture_alias_cannot_hide_native_mismatch(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'Fixture.cs'
            path.write_text('public enum UnitEffect { Frozen=0,Swarmed=6 }')
            self.contract['enums'][0]['fixtures']=[str(path)]
            with self.assertRaisesRegex(ValueError,'Fixture enum differs from native metadata'):audit.check(self.refs,self.contract)

if __name__=='__main__':unittest.main(verbosity=2)
