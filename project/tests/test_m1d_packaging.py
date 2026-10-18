"""API-file and build-gate checks; not managed C# or native gameplay execution."""
import json, os, shutil, sys, tempfile, unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from audit_native_api import check

class M1DReferenceChecks(unittest.TestCase):
    def setUp(self):
        supplied=os.environ.get('DECEPTION_API_REFERENCE_DIR')
        if not supplied:self.skipTest('Supplied pinned references are external to the source candidate.')
        self.references=Path(supplied)
        self.contract=json.loads((ROOT/'tools/native-wheel-api.json').read_text())

    def runtime_contract(self):
        name='Il2CppInterop.Runtime.dll'
        return {'reference_sha256':{name:self.contract['reference_sha256'][name]},
                'methods':[r for r in self.contract['methods'] if r['file']==name],
                'installed_reference_locations':self.contract['installed_reference_locations']}

    def test_export_and_existing_mac_layout_match(self):
        contract=self.runtime_contract()
        flat=check(self.references,contract)
        with tempfile.TemporaryDirectory() as t:
            base=Path(t)/'BepInEx';core=base/'core';interop=base/'interop'
            core.mkdir(parents=True);interop.mkdir()
            shutil.copyfile(self.references/'Il2CppInterop.Runtime.dll',core/'Il2CppInterop.Runtime.dll')
            self.assertEqual(check(interop,contract),flat)

    def test_wrong_present_reference_cannot_fall_back_to_good_copy(self):
        with tempfile.TemporaryDirectory() as t:
            base=Path(t)/'BepInEx';core=base/'core';interop=base/'interop'
            core.mkdir(parents=True);interop.mkdir()
            shutil.copyfile(self.references/'Il2CppInterop.Runtime.dll',core/'Il2CppInterop.Runtime.dll')
            (interop/'Il2CppInterop.Runtime.dll').write_bytes(b'wrong reference fixture')
            with self.assertRaisesRegex(ValueError,'API reference changed'):
                check(interop,self.runtime_contract())

    def test_missing_ui_reference_blocks_mac_build_mode(self):
        # A hosted partial metadata result may be recorded, but the Mac build
        # must not certify an incomplete dependency set as build-ready.
        with tempfile.TemporaryDirectory() as t:
            contract={'reference_sha256':{},'methods':[],
                      'runtime_methods':self.contract['runtime_methods']}
            partial=check(Path(t),contract)
            self.assertEqual(len(partial['runtime_ui_methods_not_checked']),2)
            self.assertEqual(partial['native_game_execution'],'NOT RUN')
            with self.assertRaisesRegex(ValueError,'Required local UI reference missing'):
                check(Path(t),contract,require_runtime=True)

if __name__=='__main__':unittest.main(verbosity=2)
