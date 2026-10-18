"""File-level checks only. None of these assertions runs Unity or the C# fixtures."""
import hashlib,json,re,sys,tempfile,unittest
from pathlib import Path
from unittest import mock
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import manage as m

class M1CChecks(unittest.TestCase):
    def code(self,name):return (ROOT/'src/PolyMod'/name).read_text()
    def records(self):
        ids=m.expected_ui_adapter_ids()
        return dict(total=len(ids),passed=len(ids),failed=0,is64Bit=True,tests=[{'id':i,'status':'PASS'} for i in ids])
    def test_01_ui_target_setter_no_longer_used(self):
        s=self.code('RecruitmentWheelUi.cs')
        self.assertNotIn('Native.Set(p.Wheel,"Target"',s)
        self.assertIn('Native.Set(p.Wheel,"Position",p.Position)',s)
        self.assertIn('Native.Set(p.Wheel,"KeepWorldPosition",true)',s)
    def test_02_position_captured_during_click(self):
        s=self.code('RecruitmentWheelUi.cs');start=s.index('public static bool InterceptSpawn');end=s.index('public static void Tick')
        self.assertIn('Native.Get(wheel,"Position")',s[start:end]);self.assertIn('Position=position',s[start:end])
    def test_03_all_three_display_entry_points(self):
        s=self.code('TechPresentation.cs')
        for x in ['"TechPopupContent","CreateTechDataContent"','"TechPopup","SetTechData"','"TechItem","GetUnlockItems"']:
            self.assertIn(x,s)
    def test_04_clone_native_not_managed_wrapper(self):
        s=self.code('TechPresentation.cs')
        self.assertIn('Native.T("Il2CppSystem.Object")',s);self.assertIn('BindingFlags.DeclaredOnly',s)
        self.assertIn('m.ReturnType==owner',s);self.assertIn('Native.Same(display,data)',s)
    def test_05_m1d_preserves_working_m1c_mechanisms(self):
        # M1C's blanket source-equality gate is superseded for explicitly edited
        # presentation/selection/action adapters. Keep exact tree/audio/roster/
        # payment baseline evidence while checking the unmodified mechanisms.
        old=json.loads((ROOT/'evidence/m1c-baseline.json').read_text())['source_sha256']
        for n in ['src/Core/TechTree.cs','src/Core/RecruitmentCatalog.cs',
                  'src/PolyMod/ResearchAudio.cs','src/PolyMod/TechPresentation.cs']:
            if n in ('src/Core/TechTree.cs','src/PolyMod/ResearchAudio.cs'):
                from a11_source_contract import assert_a11_source
                assert_a11_source(self,ROOT,n,'evidence/m1c-baseline.json')
            elif n=='src/Core/RecruitmentCatalog.cs':
                amendment=json.loads((ROOT/'evidence/a14-source-amendment.json').read_text())
                self.assertEqual(old[n],amendment['historical_sha256'])
                self.assertEqual(m.sha(ROOT/amendment['authority']),amendment['authority_sha256'])
                current=json.loads((ROOT/'evidence/a18-source-amendment.json').read_text())
                self.assertEqual(current['previous_sha256'],amendment['source_sha256'])
                self.assertEqual(m.sha(ROOT/current['authority']),current['authority_sha256'])
                naval=json.loads((ROOT/'evidence/a20-source-amendment.json').read_text())
                self.assertEqual(naval['previous_sha256'],current['source_sha256'])
                self.assertEqual(m.sha(ROOT/naval['authority']),naval['authority_sha256'])
                cloaks=json.loads((ROOT/'evidence/a22-source-amendment.json').read_text())
                self.assertEqual(cloaks['previous_sha256'],naval['source_sha256'])
                self.assertEqual(m.sha(ROOT/cloaks['authority']),cloaks['authority_sha256'])
                current=json.loads((ROOT/'evidence/a23-source-amendment.json').read_text())
                self.assertEqual(current['previous_sha256'],cloaks['source_sha256'])
                self.assertEqual(m.sha(ROOT/current['authority']),current['authority_sha256'])
                full=json.loads((ROOT/'evidence/a24-source-amendment.json').read_text())
                self.assertEqual(full['previous_sha256'],current['source_sha256'])
                self.assertEqual(m.sha(ROOT/full['authority']),full['authority_sha256'])
                aquarion=json.loads((ROOT/'evidence/a31-source-amendment.json').read_text())
                self.assertEqual(aquarion['previous_sha256'],full['source_sha256'])
                self.assertEqual(m.sha(ROOT/aquarion['authority']),aquarion['authority_sha256'])
                balance=json.loads((ROOT/'evidence/a44-source-amendment.json').read_text())
                self.assertEqual(balance['previous_sha256'],aquarion['source_sha256'])
                self.assertEqual(m.sha(ROOT/balance['authority']),balance['authority_sha256'])
                stealth=json.loads((ROOT/'evidence/a50-source-amendment.json').read_text())
                self.assertEqual(stealth['previous_sha256'],balance['source_sha256'])
                self.assertEqual(m.sha(ROOT/stealth['authority']),stealth['authority_sha256'])
                current=json.loads((ROOT/'evidence/a52-source-amendment.json').read_text())
                self.assertEqual(m.sha(ROOT/current['authority']),current['authority_sha256'])
                self.assertEqual(current['sources'][n]['previous_sha256'],stealth['source_sha256'])
                rename=json.loads((ROOT/'evidence/a56-source-amendment.json').read_text())
                self.assertEqual(rename['source'],n)
                self.assertEqual(m.sha(ROOT/rename['authority']),rename['authority_sha256'])
                self.assertEqual(rename['previous_sha256'],current['sources'][n]['source_sha256'])
                price=json.loads((ROOT/'evidence/a61-source-amendment.json').read_text())
                self.assertEqual(price['source'],n)
                self.assertEqual(m.sha(ROOT/price['authority']),price['authority_sha256'])
                self.assertEqual(price['previous_sha256'],rename['source_sha256'])
                self.assertEqual(m.sha(ROOT/n),price['source_sha256'])
            elif n=='src/PolyMod/TechPresentation.cs':
                aquatic=json.loads((ROOT/'evidence/a31-source-amendment.json').read_text())
                self.assertEqual(m.sha(ROOT/aquatic['authority']),aquatic['authority_sha256'])
                self.assertEqual(aquatic['tech_preview']['historical_sha256'],old[n])
                repair=json.loads((ROOT/'evidence/a32-source-amendment.json').read_text())
                self.assertEqual(repair['previous_sha256'],aquatic['tech_preview']['source_sha256'])
                self.assertEqual(m.sha(ROOT/repair['authority']),repair['authority_sha256'])
                current=json.loads((ROOT/'evidence/a52-source-amendment.json').read_text())
                self.assertEqual(m.sha(ROOT/current['authority']),current['authority_sha256'])
                self.assertEqual(current['sources'][n]['previous_sha256'],repair['source_sha256'])
                self.assertEqual(m.sha(ROOT/n),current['sources'][n]['source_sha256'])
            else:self.assertEqual(m.sha(ROOT/n),old[n],n)
    def test_06_prior_archive_observed_not_guessed(self):
        old=json.loads((ROOT/'evidence/prior-m1b-inputs.json').read_text())
        self.assertEqual(m.OBSERVED_M1B_SHA256,old['installed_mod_sha256'])
    def test_07_ui_results_accept_complete_synthetic_records(self):
        # Validation logic only; these synthetic PASS records are NOT test evidence.
        m.validate_ui_adapter_results(self.records())
    def test_08_ui_results_summary_only_refused(self):
        r=self.records();r.pop('tests')
        with self.assertRaises(m.Stop):m.validate_ui_adapter_results(r)
    def test_09_ui_results_failed_case_refused(self):
        r=self.records();r['tests'][0]['status']='FAIL'
        with self.assertRaises(m.Stop):m.validate_ui_adapter_results(r)
    def test_10_ui_results_duplicate_refused(self):
        r=self.records();r['tests'][1]=r['tests'][0]
        with self.assertRaises(m.Stop):m.validate_ui_adapter_results(r)
    def test_11_ui_results_unknown_id_refused(self):
        r=self.records();r['tests'][0]['id']='bogus'
        with self.assertRaises(m.Stop):m.validate_ui_adapter_results(r)
    def test_12_ui_results_32bit_refused(self):
        r=self.records();r['is64Bit']=False
        with self.assertRaises(m.Stop):m.validate_ui_adapter_results(r)
    def test_13_expected_ids_match_actual_fixture(self):
        source='\n'.join(p.read_text() for p in (ROOT/'tests/ui-adapter').glob('*.cs'))
        ids=re.findall(r'(?:T|Test|test|Case)\(\s*"(UI-[^"\n]+)"',source)
        self.assertEqual(len(ids),len(set(ids)))
        self.assertEqual(set(ids),set(m.expected_ui_adapter_ids()))
    def test_14_build_compiles_production_ui_not_duplicate_implementation(self):
        s=(ROOT/'tools/manage.py').read_text()
        for name in ['NativeApi.cs','RecruitmentWheelUi.cs','TechPresentation.cs',
                     'MimicPresentationState.cs','MimicPresentationUi.cs','MimicControls.cs','OrdinaryActionEligibility.cs']:
            self.assertIn(name,m.UI_ADAPTER_SOURCES)
        self.assertIn('for n in UI_ADAPTER_SOURCES',s)
        self.assertLess(s.index('validate_ui_adapter_results(ui_results)'),s.index("work/'Deception.dll'"))
    def test_15_install_requires_ui_evidence(self):
        s=(ROOT/'tools/manage.py').read_text().split('def install(game,backup_previous=True):')[1].split('def report')[0]
        self.assertIn("receipt.get('ui_adapter_results_sha256')",s)
        self.assertLess(s.index('validate_ui_adapter_results'),s.index('install_archive('))
    def test_16_report_includes_ui_records_explicitly(self):
        with tempfile.TemporaryDirectory() as t:
            home=Path(t);(home/'Desktop').mkdir();proj=home/'project';work=proj/'build/m0-fixture';work.mkdir(parents=True)
            payload='{"scope":"FAKE TEST RECORD, NOT EXECUTION"}'
            (work/'ui-adapter-results.json').write_text(payload)
            with mock.patch.object(m.Path,'home',return_value=home),mock.patch.object(m,'ROOT',proj),mock.patch.object(m.subprocess,'run',side_effect=AssertionError('Report must not launch anything')):
                m.report(home/'game')
            import zipfile
            with zipfile.ZipFile(next((home/'Desktop').glob('*.zip'))) as z:self.assertEqual(z.read('build/ui-adapter-results.json').decode(),payload)
    def test_17_reported_setter_failure_preserved(self):
        events=[json.loads(s) for s in (ROOT/'evidence/input-m1b-events.jsonl').read_text().splitlines()]
        errors=[e for e in events if e['kind']=='mimic-wheel-error']
        self.assertEqual(len(errors),3);self.assertTrue(all('QuickActions.set_Target' in e['message'] for e in errors))
    def test_18_no_silent_model_test_count_inflation(self):
        self.assertGreaterEqual(len(m.expected_model_ids()),150);self.assertGreaterEqual(len(m.expected_ui_adapter_ids()),24)

if __name__=='__main__':unittest.main(verbosity=2)
