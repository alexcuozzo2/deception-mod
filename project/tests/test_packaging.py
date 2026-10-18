"""Linux/macOS file-level tests. These do NOT compile or execute the C# mod."""
import importlib.util
import json
from pathlib import Path
import re
import tempfile
import unittest
from unittest import mock
import zipfile

PROJECT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('deception_manage',PROJECT/'tools/manage.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        self.source=self.root/'candidate.polymod';self.source.write_bytes(b'test candidate, not a real mod')
        self.target=self.root/'Mods'/m.MODNAME;self.receipts=self.root/'receipts'
    def tearDown(self):self.temp.cleanup()
    def test_01_install_only_own_file(self):
        self.assertEqual(m.install_archive(self.source,self.target,self.receipts),'INSTALLED')
        self.assertEqual(self.target.read_bytes(),self.source.read_bytes())
        self.assertEqual([p.name for p in self.target.parent.iterdir()],[m.MODNAME])
    def test_02_repeat_identical(self):
        m.install_archive(self.source,self.target,self.receipts);ino=self.target.stat().st_ino
        self.assertEqual(m.install_archive(self.source,self.target,self.receipts),'ALREADY INSTALLED')
        self.assertEqual(ino,self.target.stat().st_ino)
    def test_03_different_archive_refused(self):
        self.target.parent.mkdir();self.target.write_bytes(b'existing')
        with self.assertRaises(m.Stop):m.install_archive(self.source,self.target,self.receipts)
        self.assertEqual(self.target.read_bytes(),b'existing')
    def test_04_source_symlink_refused(self):
        link=self.root/'link';link.symlink_to(self.source)
        with self.assertRaises(m.Stop):m.install_archive(link,self.target,self.receipts)
        self.assertFalse(self.target.exists())
    def test_05_target_symlink_refused(self):
        self.target.parent.mkdir();self.target.symlink_to(self.source)
        with self.assertRaises(m.Stop):m.install_archive(self.source,self.target,self.receipts)
        self.assertTrue(self.target.is_symlink())
    def test_06_racing_destination_preserved(self):
        real_link=m.os.link
        def collide(a,b):
            Path(b).write_bytes(b'concurrent writer');real_link(a,b)
        with mock.patch.object(m.os,'link',collide):
            with self.assertRaises(FileExistsError):m.install_archive(self.source,self.target,self.receipts)
        self.assertEqual(self.target.read_bytes(),b'concurrent writer')
        self.assertFalse(any(p.name.startswith('.deception-install-') for p in self.target.parent.iterdir()))
    def test_07_active_mods_and_old_folder_rejected_by_inventory(self):
        mods=self.root/'Mods';mods.mkdir();(mods/'old').mkdir();(mods/'polibrary.polymod').touch();(mods/'.DS_Store').touch()
        self.assertEqual({p.name for p in m.active_entries(self.root)},{'old','polibrary.polymod'})
    def test_08_content_archive_whitelist(self):
        dll=self.root/'Deception.dll';dll.write_bytes(b'fixture DLL bytes NOT executable')
        output=self.root/'mod.zip';m.make_archive(dll,output)
        with zipfile.ZipFile(output) as z:
            self.assertIsNone(z.testzip());self.assertEqual(set(z.namelist()),{'manifest.json','localization.json','Deception.dll','DEVELOPMENT-STATUS.txt'} | set(m.content_entries()))
    def test_09_model_gate_rejects_summary_only(self):
        with self.assertRaises(m.Stop):m.validate_model_results({'total':100,'passed':100,'failed':0,'is64Bit':True})
    def test_10_model_gate_rejects_a_failure(self):
        r=self.results();r['tests'][3]['status']='FAIL'
        with self.assertRaises(m.Stop):m.validate_model_results(r)
    def test_11_model_gate_rejects_duplicates(self):
        r=self.results();r['tests'][1]=r['tests'][0]
        with self.assertRaises(m.Stop):m.validate_model_results(r)
    def test_12_model_gate_accepts_consistent_synthetic_records(self):
        # Only tests validator logic; it is NOT evidence that model tests ran.
        m.validate_model_results(self.results())
    def results(self):return {'total':len(m.expected_model_ids()),'passed':len(m.expected_model_ids()),'failed':0,'is64Bit':True,'tests':[{'id':i,'status':'PASS'} for i in m.expected_model_ids()]}
    def test_13_source_model_ids_and_case_references(self):
        source='\n'.join(p.read_text() for p in (PROJECT/'tests').glob('*.cs'))
        ids=re.findall(r'(?:Test|T|test)\("((?:M[012W]-\d{3}|A11-[0-9R]|A14-\d{3}|A40-\d{3}|A44-\d{3}|A50-\d{3}|A52-\d{3}|A61-\d{3}|GROW-\d{3}|NAV-\d{3}|NAV-UPGRADE-\d{3}|CLOAK-\d{3}|SPY-\d{3}|CITY-0361-\d{3}|REGISTRY-0361-\d{3}|SHED-\d{3}|ESP-\d{3}|BM-\d{3}|A31-\d{3}|AQUA-0371)[^"\n]*)"',source)
        self.assertEqual(len(ids),len(m.expected_model_ids()));self.assertEqual(sum(s.startswith('M0-') for s in ids),96);self.assertGreaterEqual(sum(s.startswith('M1-') for s in ids),20)
        self.assertEqual(set(ids),set(m.expected_model_ids()))
        cases=json.loads((PROJECT/'spec/ACCEPTANCE_TESTS_v1_6.json').read_text())['cases'];valid={c['id'] for c in cases}
        self.assertTrue(set(re.findall(r'DEC-\d{3}',source))<=valid)
    def test_14_frozen_cases_not_relabelled(self):
        cases=json.loads((PROJECT/'spec/ACCEPTANCE_TESTS.json').read_text())
        self.assertEqual([c['id'] for c in cases['cases']],[f'DEC-{i:03d}' for i in range(1,181)])
        results=json.loads((PROJECT/'spec/TEST_RESULTS_TEMPLATE.json').read_text())['records']
        self.assertTrue(all(r['status']=='NOT RUN' for r in results))
    def test_15_manifest_no_polibrary_dependency(self):
        text=(PROJECT/'content/manifest.json').read_text();data=json.loads(text)
        self.assertEqual(data['id'],'alex_deception');self.assertNotIn('polibrary',str(data.get('dependencies',{})).lower())
        self.assertIn('2.17.3.16375',text)
    def test_16_environment_injection_removed(self):
        with mock.patch.dict(m.os.environ,{'DYLD_INSERT_LIBRARIES':'bad','DOORSTOP_TARGET_ASSEMBLY':'bad','BEPINEX_GAME_ASSEMBLY_PATH':'bad','MONOMOD_DEBUG':'1','ARCHPREFERENCE':'arm64'}):
            env=m.clean_env();self.assertFalse(any(k.startswith(('DYLD_','DOORSTOP_','MONOMOD_')) for k in env));self.assertNotIn('ARCHPREFERENCE',env)
    def test_17_lock_not_overwritten(self):
        with mock.patch.object(m,'ROOT',self.root):
            with m.lock():
                with self.assertRaises(m.Stop):
                    with m.lock():pass
            self.assertFalse((self.root/'.operation-lock').exists())
    def test_18_explicit_report_whitelist_no_finder_no_saves(self):
        home=self.root/'home';(home/'Desktop').mkdir(parents=True)
        game=self.root/'game';(game/'BepInEx/config').mkdir(parents=True);(game/'BepInEx/config/BepInEx.cfg').write_text('fixture')
        (game/'save').write_text('do not collect');(game/'GameAssembly.dylib').write_text('do not collect')
        with mock.patch.object(m.Path,'home',return_value=home),mock.patch.object(m,'ROOT',self.root),mock.patch.object(m.subprocess,'run',side_effect=AssertionError('No process should run during report')):
            m.report(game)
        output=next((home/'Desktop').glob('deception-m0-report.*.zip'))
        with zipfile.ZipFile(output) as z:
            self.assertEqual(set(z.namelist()),{'game/BepInEx.cfg','collection.json'});self.assertIsNone(z.testzip())
    def test_19_no_host_changes_on_linux_preflight(self):
        with mock.patch.object(m.platform,'system',return_value='Linux'):
            with self.assertRaises(m.Stop):m.preflight(self.root)
        self.assertFalse(self.target.exists())
    def test_20_synthetic_tree_fixture_scope(self):
        f=json.loads((PROJECT/'tests/native-tree.synthetic.json').read_text())
        self.assertEqual(len(f['techData']['basic']['techUnlocks']),5)
        self.assertEqual(f['tribeData']['aquarion']['category'],'special')
        self.assertIn('basic',f['tribeData']['bardur']['startingTech'])
        self.assertEqual(f['tribeData']['aquarion']['techOverrides']['fishing'],'aquafishing')

    def original_for_upgrade(self):
        self.target.parent.mkdir();self.target.write_bytes(b'known original')
        return m.digest(b'known original')
    def test_21_known_upgrade_preserves_original(self):
        old=self.original_for_upgrade()
        with mock.patch.object(m,'PREVIOUS_MOD_SHA256',old):
            self.assertEqual(m.install_archive(self.source,self.target,self.receipts),'UPGRADED')
        copies=list(self.receipts.glob('*.polymod'));self.assertEqual(len(copies),1)
        self.assertEqual(copies[0].read_bytes(),b'known original');self.assertEqual(self.target.read_bytes(),self.source.read_bytes())
    def test_22_bad_backup_refuses_upgrade(self):
        old=self.original_for_upgrade();self.receipts.mkdir()
        (self.receipts/('alex_deception_m0-v0.1.0-'+old[:16]+'.polymod')).write_bytes(b'unrelated')
        with mock.patch.object(m,'PREVIOUS_MOD_SHA256',old):
            with self.assertRaises(m.Stop):m.install_archive(self.source,self.target,self.receipts)
        self.assertEqual(self.target.read_bytes(),b'known original')
    def test_23_backup_symlink_refused(self):
        old=self.original_for_upgrade();self.receipts.mkdir()
        (self.receipts/('alex_deception_m0-v0.1.0-'+old[:16]+'.polymod')).symlink_to(self.source)
        with mock.patch.object(m,'PREVIOUS_MOD_SHA256',old):
            with self.assertRaises(m.Stop):m.install_archive(self.source,self.target,self.receipts)
        self.assertEqual(self.target.read_bytes(),b'known original')
    def test_24_failed_staging_does_not_replace(self):
        old=self.original_for_upgrade();real=m.sha
        def faulty(path):return 'bad' if Path(path).name.startswith('.deception-install-') else real(path)
        with mock.patch.object(m,'PREVIOUS_MOD_SHA256',old),mock.patch.object(m,'sha',side_effect=faulty):
            with self.assertRaises(m.Stop):m.install_archive(self.source,self.target,self.receipts)
        self.assertEqual(self.target.read_bytes(),b'known original')
    def test_25_failed_post_replace_check_rolls_back(self):
        old=self.original_for_upgrade();real=m.sha;failed=False
        def fail_once(path):
            nonlocal failed
            if Path(path)==self.target and self.target.read_bytes()==self.source.read_bytes() and not failed:
                failed=True;raise OSError('injected read failure after replacement')
            return real(path)
        with mock.patch.object(m,'PREVIOUS_MOD_SHA256',old),mock.patch.object(m,'sha',side_effect=fail_once):
            with self.assertRaises(OSError):m.install_archive(self.source,self.target,self.receipts)
        self.assertTrue(failed);self.assertEqual(self.target.read_bytes(),b'known original')
        self.assertFalse(any(p.name.startswith('.deception-') for p in self.target.parent.iterdir()))
    def test_26_repeat_upgrade_no_extra_backup(self):
        old=self.original_for_upgrade()
        with mock.patch.object(m,'PREVIOUS_MOD_SHA256',old):m.install_archive(self.source,self.target,self.receipts)
        before={p.name:p.read_bytes() for p in self.receipts.iterdir()}
        self.assertEqual(m.install_archive(self.source,self.target,self.receipts),'ALREADY INSTALLED')
        self.assertEqual(before,{p.name:p.read_bytes() for p in self.receipts.iterdir()})
    def test_27_adapter_no_synthetic_roots_or_render_prefix(self):
        code=(PROJECT/'src/PolyMod/Entry.cs').read_text();tree=(PROJECT/'src/Core/TechTree.cs').read_text()
        self.assertNotIn('BeforeRender',code);self.assertNotIn('TechView_CreateNode',code)
        # The real four-section transaction is exercised by registry-order's
        # publication/rollback suite, including Aquarion tribe overrides.
        self.assertIn('publication=new NativeRegistryPublication(__1,candidate)',code)
        self.assertIn('publication.Apply()',code);self.assertIn('publication.Rollback()',code)
        self.assertIn('RecruitmentCatalog.ValidateReentry(NativeJson.Read(__1))',code)
        self.assertNotIn('indexer.SetValue(',code)
        self.assertNotIn('RootPrefix+name',tree);self.assertNotIn('overrides["basic"]=',tree)
        self.assertNotIn('p.OrdinaryTribe,',(PROJECT/'src/Core/Rules.cs').read_text())
    def test_28_supplied_registry_sixteen_and_starting_root(self):
        data=json.loads((PROJECT/'tests/native-tree.captured.json').read_text());ts=data['tribeData']
        active={k:v for k,v in ts.items() if v['category'] in [1,2]}
        self.assertEqual(len(active),16);self.assertEqual(sum(t['category']==2 for t in active.values()),4)
        self.assertTrue(all('basic' in t['startingTech'] for t in active.values()))
        self.assertTrue(all('basic' not in t.get('techOverrides',{}) for t in active.values()))
        self.assertEqual(data['techData']['basic']['idx'],0)
    def test_29_markdown_amendment_cases_not_native_passes(self):
        data=json.loads((PROJECT/'spec/ACCEPTANCE_TESTS_v1_6.json').read_text());cases=data['cases']
        self.assertEqual([c['id'] for c in cases],[f'DEC-{i:03d}' for i in range(1,201)])
        self.assertTrue(all(c['status']=='NOT RUN' for c in cases))
        self.assertIn('markdown',data['amendment_source_note'].lower())
        self.assertTrue(all('source_excerpt' in c for c in cases[180:]))
    def test_30_only_two_original_acceptance_cases_overridden(self):
        a=json.loads((PROJECT/'spec/ACCEPTANCE_TESTS.json').read_text())['cases']
        b=json.loads((PROJECT/'spec/ACCEPTANCE_TESTS_v1_6.json').read_text())['cases'][:180]
        self.assertEqual([x['id'] for x,y in zip(a,b) if x!=y],['DEC-001','DEC-091'])
    def test_31_unknown_test_id_rejected(self):
        r=self.results();r['tests'][0]['id']='M0-999-invented'
        with self.assertRaises(m.Stop):m.validate_model_results(r)
    def test_32_32bit_test_execution_rejected(self):
        r=self.results();r['is64Bit']=False
        with self.assertRaises(m.Stop):m.validate_model_results(r)
    def test_33_source_prerequisite_gate_not_free_research(self):
        code=(PROJECT/'src/Core/TechTree.cs').read_text()
        self.assertIn('starting.Contains("basic"',code)
        self.assertNotIn('tribe["startingTech"]=',code)
        self.assertIn('Array(basic["techUnlocks"],"native roots").Insert(deceptionIndex,DeceptionId)',code)
    def test_34_topology_uses_links_not_proximity(self):
        code=(PROJECT/'src/Core/CompoundBodies.cs').read_text()
        self.assertNotIn('.Position',code);self.assertNotIn('.Rogue',code);self.assertNotIn('.Commander',code)
        self.assertIn('SegmentParents',code)
    def test_35_roster_and_harvest_are_tribe_gated(self):
        code=(PROJECT/'src/Core/Rules.cs').read_text()
        self.assertIn('player.NativeRosterForms.Contains(profile.Id)',code)
        self.assertIn('native.CanHarvest(p,resource)',code)
    def test_36_current_suite_has_no_claimed_execution(self):
        status=json.loads((PROJECT/'evidence/model-execution-status.json').read_text())
        self.assertEqual(status['csharp_model_tests'],'NOT RUN IN ASSISTANT ENVIRONMENT')
        self.assertEqual(status['native_game_test'],'NOT RUN')

if __name__=='__main__':unittest.main(verbosity=2)
