"""File-level checks, NOT C# compilation or native gameplay execution."""
import importlib.util,json,re,sys,tempfile,unittest,os,hashlib
from pathlib import Path
from unittest import mock
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import manage as m
from audit_native_api import check

class M1BChecks(unittest.TestCase):
    def test_01_old_popup_picker_removed(self):
        code=(ROOT/'src/PolyMod/RecruitmentWheelUi.cs').read_text()+(ROOT/'src/PolyMod/RecruitmentHooks.cs').read_text()
        self.assertNotIn('Next appearance',code);self.assertNotIn('Choose appearance',code);self.assertNotIn('DrawPicker',code)
    def test_02_native_radial_controls_used(self):
        code=(ROOT/'src/PolyMod/RecruitmentWheelUi.cs').read_text()
        for x in ['"QuickActions","Show"','"InteractionBar","CreateRoundBottomBarButton"','"UIRoundButton","SetUnitRenderer"','"QuickActions","AddButton"','"Mimic\\n"','"Back"']:
            self.assertIn(x,code)
    def test_03_no_ui_star_debit_or_unit_creation(self):
        code=(ROOT/'src/PolyMod/RecruitmentWheelUi.cs').read_text()
        for x in ['"set_Currency"','"CreateUnit"','"TrainCommand","Execute"','"TrainAction","Execute"']:
            self.assertNotIn(x,code)
        self.assertIn('RecruitmentHooks.NativeValid',code);self.assertIn('Native.Invoke(RecruitmentHooks.ClickedSpawn',code)
    def test_04_concrete_data_registrations_not_positive_validation_override(self):
        code=(ROOT/'src/PolyMod/RecruitmentHooks.cs').read_text()
        body=code[code.index('public static bool GuardTrainValidity'):code.index('internal static bool NativeValid')]
        self.assertNotIn('__result=true',body);self.assertIn('return true; // Native city/funds/support checks STILL RUN.',body)
        self.assertIn('CheckEligibility',body)
    def test_05_typed_byref_game_wrappers(self):
        a=(ROOT/'src/PolyMod/RecruitmentHooks.cs').read_text();b=(ROOT/'src/PolyMod/TechPresentation.cs').read_text()
        self.assertIn('ref Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData> __result',a)
        self.assertIn('UnlockIcons(ref TechData __0,ref bool __2)',b)
        self.assertNotIn('ref object __result',a);self.assertNotIn('ref object __0',b)
    def test_06_owned_roster_not_global_all_forms(self):
        code=(ROOT/'src/PolyMod/RecruitmentHooks.cs').read_text()
        self.assertIn('RosterFrom',code);self.assertIn('GetOverride',code);self.assertIn('ReadNativeRoster',code)
        self.assertIn('RecruitmentEligibility.Permitted',code)
    def test_07_confirm_revalidates_and_deferred_clicks(self):
        code=(ROOT/'src/PolyMod/RecruitmentWheelUi.cs').read_text()
        self.assertIn('TickNumber+2',code);self.assertIn('RecruitmentHooks.CheckEligibility(p.State,command',code)
        self.assertIn('RecruitmentHooks.NativeValid(p.State,command',code)
        self.assertIn('ChangingUi',code);self.assertIn('CallbackRoots',code)
    def test_08_csharp_test_manifest_matches_sources(self):
        source='\n'.join(p.read_text() for p in (ROOT/'tests').glob('*.cs'))
        ids=set(re.findall(r'(?:Test|T|test)\("((?:M0|M1|M2|MW|A11|A14|A40|A44|A50|A52|A61|GROW|NAV|CLOAK|SPY|CITY|REGISTRY|SHED|ESP|BM|A31|AQUA)-[^"\n]+)"',source))
        self.assertEqual(ids,set(m.expected_model_ids()));self.assertEqual(len(ids),len(m.expected_model_ids()))
    def test_09_sfx_and_tree_explicit_a11_revision(self):
        baseline=json.loads((ROOT/'evidence/prior-m1a-inputs.json').read_text())
        for name in ['src/PolyMod/ResearchAudio.cs','src/Core/TechTree.cs']:
            from a11_source_contract import assert_a11_source
            assert_a11_source(self,ROOT,name,'evidence/prior-m1a-inputs.json')
    def test_10_observed_m1a_upgrade_hash(self):
        baseline=json.loads((ROOT/'evidence/prior-m1a-inputs.json').read_text())
        self.assertEqual(m.OBSERVED_M1A_SHA256,baseline['installed_mod_sha256'])
        import ast, inspect
        calls=[n for n in ast.walk(ast.parse(inspect.getsource(m.install)))
               if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id=="install_archive"]
        self.assertEqual(len(calls),1)
        accepted={n.id for n in ast.walk(calls[0].args[3]) if isinstance(n,ast.Name)}
        self.assertIn("previous_receipt_hashes",accepted)
        self.assertIn("OBSERVED_M1A_SHA256",accepted)
    def test_11_only_mod_installed_no_finder(self):
        code=(ROOT/'tools/manage.py').read_text()
        report=code[code.index('def report('):code.index('@contextlib.contextmanager')]
        self.assertNotIn('subprocess',report);self.assertNotIn('open -R',report)
        self.assertIn("only Mods/",code);self.assertIn("'native-api-results.json'",report)
    def test_12_preview_does_not_mutate_original_registry(self):
        code=(ROOT/'src/PolyMod/TechPresentation.cs').read_text()
        self.assertIn('Native.Set(display,"unitUnlocks",',code)
        self.assertIn('Native.Same(display,data)',code)
        self.assertNotIn('Native.Set(data,',code)
    def test_13_self_contained_mod_only_dependency(self):
        manifest=json.loads((ROOT/'content/manifest.json').read_text())
        self.assertEqual(manifest['id'],'alex_deception');self.assertEqual(manifest['version'],'0.3.101')
        self.assertIn(manifest['version'],manifest['name']);self.assertNotIn('M1E',manifest['name'])
        assembly=(ROOT/'src/Version.cs').read_text()
        self.assertIn('AssemblyVersion("'+manifest['version']+'.0")',assembly)
        self.assertEqual([d['id'] for d in manifest['dependencies']],['polytopia'])
    def test_14_no_cpp2il_or_framework_patcher(self):
        self.assertFalse(any((ROOT/'src').rglob('*.c')))
        self.assertNotIn('libfunchook', (ROOT/'src/PolyMod/RecruitmentWheelUi.cs').read_text())
    def test_15_actual_api_metadata_matches(self):
        path=os.environ.get('DECEPTION_API_REFERENCE_DIR')
        if not path:self.skipTest('Actual wrapper fixture not present in this source-only package.')
        result=check(Path(path),json.loads((ROOT/'tools/native-wheel-api.json').read_text()))
        self.assertEqual(result['status'],'METADATA_SIGNATURES_MATCH');self.assertGreaterEqual(result['verified_count'],140)
    def test_16_changed_native_signature_refused(self):
        path=os.environ.get('DECEPTION_API_REFERENCE_DIR')
        if not path:self.skipTest('Actual wrapper fixture not present.')
        contract=json.loads((ROOT/'tools/native-wheel-api.json').read_text());contract['methods'][0]['args']=['No.Such.Argument']
        with self.assertRaises(ValueError):check(Path(path),contract)
    def test_17_wrong_api_hash_refused(self):
        path=os.environ.get('DECEPTION_API_REFERENCE_DIR')
        if not path:self.skipTest('Actual wrapper fixture not present.')
        contract=json.loads((ROOT/'tools/native-wheel-api.json').read_text());contract['reference_sha256'][next(iter(contract['reference_sha256']))]='0'*64
        with self.assertRaises(ValueError):check(Path(path),contract)
    def test_18_versioned_ui_spec_preserved(self):
        s=(ROOT/'spec/UI_A02_RECRUITMENT_WHEEL.md').read_text()
        self.assertIn('Back',s);self.assertIn('3 stars',s);self.assertIn('own native roster',s)

if __name__=='__main__':unittest.main(verbosity=2)
