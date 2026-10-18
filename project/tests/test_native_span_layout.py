"""Check audio value-layout assumptions against installed native metadata."""
import copy,json,os,sys,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import audit_native_api as audit
class NativeSpanLayoutTests(unittest.TestCase):
    def setUp(self):
        self.refs=Path(os.environ.get('DECEPTION_API_REFERENCE_DIR',str(Path.home()/'Library/Application Support/Steam/steamapps/common/The Battle of Polytopia/BepInEx/interop')))
        if not (self.refs/'UnityEngine.CoreModule.dll').is_file():self.skipTest('Exact native audio layout reference unavailable')
        source=json.loads((ROOT/'tools/native-wheel-api.json').read_text())
        self.contract={'reference_sha256':{},'methods':[],'runtime_layouts':copy.deepcopy(source['runtime_layouts'])}
    def test_native_explicit_layout_matches_audio_contract(self):
        result=audit.check(self.refs,self.contract,require_runtime=True)
        row=result['native_layouts_verified'][0]
        self.assertEqual(row['layout'],16)
        self.assertEqual(row['fields'],[{'name':'begin','type':'System.IntPtr','offsets':[0]},{'name':'length','type':'System.Int32','offsets':[8]}])
    def test_old_sequential_assumption_is_rejected(self):
        self.contract['runtime_layouts'][0]['value']['layout']=8
        with self.assertRaisesRegex(ValueError,'Native value layout mismatch'):audit.check(self.refs,self.contract,require_runtime=True)
    def test_wrong_field_offset_is_rejected(self):
        self.contract['runtime_layouts'][0]['value']['fields'][1]['offsets']=[4]
        with self.assertRaisesRegex(ValueError,'Native value layout mismatch'):audit.check(self.refs,self.contract,require_runtime=True)
    def test_wrong_field_type_is_rejected(self):
        self.contract['runtime_layouts'][0]['value']['fields'][1]['type']='System.Int64'
        with self.assertRaisesRegex(ValueError,'Native value layout mismatch'):audit.check(self.refs,self.contract,require_runtime=True)
