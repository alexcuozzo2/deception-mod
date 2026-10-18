"""File/metadata guards only. All receipt/runtime records below are synthetic fixtures."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import zipfile

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('m1a_manage',ROOT/'tools/manage.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

class M1APackaging(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.home=self.root/'home';self.game=self.root/'game'
        self.contract=json.loads((ROOT/'tools/previous-source-contract.json').read_text());self.prior=self.home/'Downloads/deception-m0-2'
        self.work=self.prior/'build/m0-fixture';self.work.mkdir(parents=True)
        self.target=self.game/'Mods'/m.MODNAME;self.target.parent.mkdir(parents=True)
        with zipfile.ZipFile(self.target,'w') as z:
            z.writestr('manifest.json',json.dumps({'id':'alex_deception','version':'0.1.1'}));z.writestr('Deception.dll',b'fixture: not executable')
            z.writestr('localization.json','{}');z.writestr('DEVELOPMENT-STATUS.txt','old fixture')
        self.frames={}
        for key in list(m.CONTRACT['framework_checksums'])+['BepInEx/config/BepInEx.cfg']:
            p=self.game/key;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b'unchanged '+key.encode());self.frames[key]=m.sha(p)
        ids=self.contract['expected_tests'];self.result=self.work/'model-test-results.json'
        self.result.write_text(json.dumps({'total':len(ids),'passed':len(ids),'failed':0,'is64Bit':True,'tests':[{'id':x,'status':'PASS'} for x in ids]}))
        self.receipt={'status':'MODEL_TESTS_PASSED_NATIVE_NOT_RUN','sources':self.contract['sources'],'work':str(self.work),
                      'artifact_sha256':m.sha(self.target),'model_results_sha256':m.sha(self.result),'framework_and_config':self.frames,
                      'dll_sha256':m.digest(b'fixture: not executable')}
        self.pointer=self.prior/'build/latest.json';self.save()
    def save(self):self.pointer.write_text(json.dumps(self.receipt))
    def tearDown(self):self.temp.cleanup()
    def accepted(self):return m.previous_receipt_hashes(self.game,self.home)
    def test_01_valid_prior_receipt_accepted(self):self.assertEqual(self.accepted(),{m.sha(self.target)})
    def test_02_wrong_source_tree_refused(self):
        self.receipt['sources']['src/Version.cs']='wrong';self.save();self.assertEqual(self.accepted(),set())
    def test_03_modified_installed_mod_refused(self):
        self.target.write_bytes(b'changed');self.assertEqual(self.accepted(),set())
    def test_04_missing_prior_build_refused(self):
        self.pointer.unlink();self.assertEqual(self.accepted(),set())
    def test_05_bad_prior_test_record_refused(self):
        data=json.loads(self.result.read_text());data['tests'][0]['status']='FAIL';self.result.write_text(json.dumps(data));self.receipt['model_results_sha256']=m.sha(self.result);self.save()
        self.assertEqual(self.accepted(),set())
    def test_06_changed_config_refused(self):
        (self.game/'BepInEx/config/BepInEx.cfg').write_text('different');self.assertEqual(self.accepted(),set())
    def test_07_old_pointer_symlink_refused(self):
        original=self.pointer.with_suffix('.original');self.pointer.rename(original);self.pointer.symlink_to(original)
        self.assertEqual(self.accepted(),set())
    def test_08_accepted_upgrade_backed_up(self):
        accepted=self.accepted();original=self.target.read_bytes();candidate=self.root/'new.polymod';candidate.write_bytes(b'new fixture')
        backups=self.game/'Disabled Mods/Deception backups'
        self.assertEqual(m.install_archive(candidate,self.target,backups,accepted),'UPGRADED')
        self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),original)
        self.assertEqual(self.target.read_bytes(),b'new fixture')
    def test_09_localization_matches_internal_identifier(self):
        loc=json.loads((ROOT/'content/localization.json').read_text())
        decode=lambda x:x.replace('_','.').replace('..','_')
        decoded={decode(k):v for k,v in loc.items()}
        self.assertEqual(decoded['technology.names.dcp_deception']['English'],'Deception')
        self.assertNotIn('technology.names.dcp.deception',decoded)
        self.assertEqual(decoded['unit.names.dcp_mimic']['English'],'Mimic')
    def test_10_model_test_gate_increased(self):
        self.assertGreaterEqual(len(m.expected_model_ids()),150)
        self.assertEqual(len(set(m.expected_model_ids())),len(m.expected_model_ids()))
    def test_11_only_one_archive_and_no_game_art(self):
        mf=json.loads((ROOT/'content/manifest.json').read_text());self.assertEqual(mf['version'],'0.3.101');self.assertEqual(mf['id'],'alex_deception')
        self.assertNotIn('polibrary',str(mf['dependencies']).lower())
        self.assertIn('prototype',(ROOT/'content/DEVELOPMENT-STATUS.txt').read_text().lower())
    def test_12_no_native_star_or_unit_mutation_in_picker(self):
        s=(ROOT/'src/PolyMod/RecruitmentHooks.cs').read_text()
        self.assertIn('Native.Invoke(TrainValid,command,args)',s);self.assertIn('Native.Invoke(RecruitmentHooks.ClickedSpawn,p.Bar,command',(ROOT/'src/PolyMod/RecruitmentWheelUi.cs').read_text())
        self.assertNotIn('set_Currency',s);self.assertNotIn('"UnitState","Create"',s)
        self.assertIn('Native.As(__0,"TrainCommand")',(ROOT/'src/PolyMod/RecruitmentWheelUi.cs').read_text())
    def test_13_no_direct_bare_spawn(self):
        s=(ROOT/'src/PolyMod/RecruitmentHooks.cs').read_text()
        self.assertIn('nameof(GuardTrainCommand)',s);self.assertIn('menu anchor is never spawned',s)
    def test_14_explicit_a11_native_root_revision(self):
        prior=self.contract['sources']['src/Core/TechTree.cs']
        from a11_source_contract import assert_a11_source
        assert_a11_source(self,ROOT,'src/Core/TechTree.cs','evidence/m1c-baseline.json')
    def test_15_arbitrary_old_receipt_paths_rejected(self):
        self.receipt['framework_and_config']['../../unrelated']='x';self.save();self.assertEqual(self.accepted(),set())
    def test_16_report_does_not_open_finder_or_launch(self):
        s=(ROOT/'tools/manage.py').read_text();start=s.index('def report(');end=s.index('@contextlib.contextmanager',start);code=s[start:end]
        self.assertNotIn('subprocess',code);self.assertNotIn('open -R',code)
        self.assertIn('events.jsonl',code);self.assertIn('recruitment-registration.json',code)

if __name__=='__main__':unittest.main(verbosity=2)
