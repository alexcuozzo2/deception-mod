"""Exact prior-content guard regressions; synthetic replacement is not a game test."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('m1e_manage',ROOT/'tools/manage.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

class M1EReplacementTests(unittest.TestCase):
    def test_03101_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-lighthouse-removal-03100.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_LIGHTHOUSE_REMOVAL_03100_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_LIGHTHOUSE_REMOVAL_03100_SHA256',inspect.getsource(m.install))

    def test_03101_known_no_backup_upgrade_and_unknown_preservation(self):
        e=json.loads((ROOT/'evidence/observed-lighthouse-removal-03100.json').read_text())
        with tempfile.TemporaryDirectory() as temporary:
            base=Path(temporary);candidate=base/'candidate';candidate.write_bytes(b'03101 synthetic candidate')
            destination=base/'Mods'/m.MODNAME;destination.parent.mkdir()
            destination.write_bytes(Path(e['existing_build_archive']).read_bytes());backups=base/'backups'
            self.assertEqual(m.install_archive(candidate,destination,backups,{m.OBSERVED_LIGHTHOUSE_REMOVAL_03100_SHA256},backup_previous=False),'UPGRADED')
            self.assertEqual(destination.read_bytes(),candidate.read_bytes());self.assertFalse(backups.exists())
            destination.write_bytes(b'unrecognized current archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(candidate,destination,backups,{m.OBSERVED_LIGHTHOUSE_REMOVAL_03100_SHA256},backup_previous=False)
            self.assertEqual(destination.read_bytes(),b'unrecognized current archive');self.assertFalse(backups.exists())

    def test_03100_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-spy-movement-0399.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_SPY_MOVEMENT_0399_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_SPY_MOVEMENT_0399_SHA256',inspect.getsource(m.install))

    def test_03100_known_no_backup_upgrade_and_unknown_preservation(self):
        e=json.loads((ROOT/'evidence/observed-spy-movement-0399.json').read_text())
        with tempfile.TemporaryDirectory() as temporary:
            base=Path(temporary);candidate=base/'candidate';candidate.write_bytes(b'03100 synthetic candidate')
            destination=base/'Mods'/m.MODNAME;destination.parent.mkdir()
            destination.write_bytes(Path(e['existing_build_archive']).read_bytes());backups=base/'backups'
            self.assertEqual(m.install_archive(candidate,destination,backups,{m.OBSERVED_SPY_MOVEMENT_0399_SHA256},backup_previous=False),'UPGRADED')
            self.assertEqual(destination.read_bytes(),candidate.read_bytes());self.assertFalse(backups.exists())
            destination.write_bytes(b'unrecognized current archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(candidate,destination,backups,{m.OBSERVED_SPY_MOVEMENT_0399_SHA256},backup_previous=False)
            self.assertEqual(destination.read_bytes(),b'unrecognized current archive');self.assertFalse(backups.exists())

    def test_0399_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-lighthouse-secret-0398.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_LIGHTHOUSE_SECRET_0398_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_LIGHTHOUSE_SECRET_0398_SHA256',inspect.getsource(m.install))

    def test_0399_known_no_backup_upgrade_and_unknown_preservation(self):
        e=json.loads((ROOT/'evidence/observed-lighthouse-secret-0398.json').read_text())
        with tempfile.TemporaryDirectory() as temporary:
            base=Path(temporary);candidate=base/'candidate';candidate.write_bytes(b'0399 synthetic candidate')
            destination=base/'Mods'/m.MODNAME;destination.parent.mkdir()
            destination.write_bytes(Path(e['existing_build_archive']).read_bytes());backups=base/'backups'
            self.assertEqual(m.install_archive(candidate,destination,backups,{m.OBSERVED_LIGHTHOUSE_SECRET_0398_SHA256},backup_previous=False),'UPGRADED')
            self.assertEqual(destination.read_bytes(),candidate.read_bytes());self.assertFalse(backups.exists())
            destination.write_bytes(b'unrecognized current archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(candidate,destination,backups,{m.OBSERVED_LIGHTHOUSE_SECRET_0398_SHA256},backup_previous=False)
            self.assertEqual(destination.read_bytes(),b'unrecognized current archive');self.assertFalse(backups.exists())

    def test_0398_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-blackout-discovery-0397.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_BLACKOUT_DISCOVERY_0397_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_BLACKOUT_DISCOVERY_0397_SHA256',inspect.getsource(m.install))

    def test_0398_known_no_backup_upgrade_and_unknown_preservation(self):
        e=json.loads((ROOT/'evidence/observed-blackout-discovery-0397.json').read_text())
        with tempfile.TemporaryDirectory() as temporary:
            base=Path(temporary);candidate=base/'candidate';candidate.write_bytes(b'0398 synthetic candidate')
            destination=base/'Mods'/m.MODNAME;destination.parent.mkdir()
            destination.write_bytes(Path(e['existing_build_archive']).read_bytes());backups=base/'backups'
            self.assertEqual(m.install_archive(candidate,destination,backups,{m.OBSERVED_BLACKOUT_DISCOVERY_0397_SHA256},backup_previous=False),'UPGRADED')
            self.assertEqual(destination.read_bytes(),candidate.read_bytes());self.assertFalse(backups.exists())
            destination.write_bytes(b'unrecognized current archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(candidate,destination,backups,{m.OBSERVED_BLACKOUT_DISCOVERY_0397_SHA256},backup_previous=False)
            self.assertEqual(destination.read_bytes(),b'unrecognized current archive');self.assertFalse(backups.exists())

    def test_0397_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-reload-safety-0396.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_RELOAD_SAFETY_0396_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_RELOAD_SAFETY_0396_SHA256',inspect.getsource(m.install))

    def test_0397_known_no_backup_upgrade_and_unknown_preservation(self):
        e=json.loads((ROOT/'evidence/observed-reload-safety-0396.json').read_text())
        with tempfile.TemporaryDirectory() as temporary:
            base=Path(temporary);candidate=base/'candidate';candidate.write_bytes(b'0397 synthetic candidate')
            destination=base/'Mods'/m.MODNAME;destination.parent.mkdir()
            destination.write_bytes(Path(e['existing_build_archive']).read_bytes());backups=base/'backups'
            self.assertEqual(m.install_archive(candidate,destination,backups,{m.OBSERVED_RELOAD_SAFETY_0396_SHA256},backup_previous=False),'UPGRADED')
            self.assertEqual(destination.read_bytes(),candidate.read_bytes());self.assertFalse(backups.exists())
            destination.write_bytes(b'unrecognized current archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(candidate,destination,backups,{m.OBSERVED_RELOAD_SAFETY_0396_SHA256},backup_previous=False)
            self.assertEqual(destination.read_bytes(),b'unrecognized current archive');self.assertFalse(backups.exists())

    def test_0396_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-resign-crash-0395.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_RESIGN_CRASH_0395_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_RESIGN_CRASH_0395_SHA256',inspect.getsource(m.install))

    def test_0395_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-fogger-badge-0394.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_FOGGER_BADGE_0394_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_FOGGER_BADGE_0394_SHA256',inspect.getsource(m.install))

    def test_0394_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-flat-blackmail-0393.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_FLAT_BLACKMAIL_0393_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_FLAT_BLACKMAIL_0393_SHA256',inspect.getsource(m.install))

    def test_0393_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-trade-secrets-0392.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_TRADE_SECRETS_0392_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_TRADE_SECRETS_0392_SHA256',inspect.getsource(m.install))

    def test_0392_exact_observed_predecessor_is_recognized(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-feedback-0391.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_FEEDBACK_0391_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_FEEDBACK_0391_SHA256',inspect.getsource(m.install))


    def test_0391_observed_current_matches_existing_release(self):
        import inspect
        e=json.loads((ROOT/'evidence/observed-art-updater-0390.json').read_text())
        self.assertEqual(m.sha(Path(e['existing_build_archive'])),e['archive_sha256'])
        self.assertEqual(m.OBSERVED_ART_UPDATER_0390_SHA256,e['archive_sha256'])
        self.assertIn('OBSERVED_ART_UPDATER_0390_SHA256',inspect.getsource(m.install))

    def test_0391_authorized_no_backup_install(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'new')
            dest=root/'Mods'/m.MODNAME;dest.parent.mkdir();dest.write_bytes(b'old')
            backups=root/'backups';oldhash=m.sha(dest)
            self.assertEqual(m.install_archive(artifact,dest,backups,{oldhash},backup_previous=False),'UPGRADED')
            self.assertEqual(dest.read_bytes(),b'new');self.assertFalse(backups.exists())
            self.assertEqual(m.install_archive(artifact,dest,backups,{oldhash},backup_previous=False),'ALREADY INSTALLED')
            dest.write_bytes(b'unrecognized')
            with self.assertRaises(m.Stop):m.install_archive(artifact,dest,backups,{oldhash},backup_previous=False)
            self.assertEqual(dest.read_bytes(),b'unrecognized');self.assertFalse(backups.exists())

    def test_0391_no_backup_commit_failure_preserves_original(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'new')
            dest=root/'Mods'/m.MODNAME;dest.parent.mkdir();dest.write_bytes(b'old');backups=root/'backups'
            with mock.patch.object(m.os,'replace',side_effect=OSError('commit failure')):
                with self.assertRaises(OSError):m.install_archive(artifact,dest,backups,{m.sha(dest)},backup_previous=False)
            self.assertEqual(dest.read_bytes(),b'old');self.assertFalse(backups.exists())
            self.assertEqual(list(dest.parent.iterdir()),[dest])

    def test_0390_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-counterintelligence-0389.json').read_text())
        self.assertEqual(m.OBSERVED_COUNTERINTELLIGENCE_0389_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_COUNTERINTELLIGENCE_0389_SHA256',inspect.getsource(m.install))

    def test_0390_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/counterintelligence-0390/baseline-0389.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_COUNTERINTELLIGENCE_0389_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0390')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_COUNTERINTELLIGENCE_0389_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_COUNTERINTELLIGENCE_0389_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_COUNTERINTELLIGENCE_0389_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0389_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-spy-stealth-0388.json').read_text())
        self.assertEqual(m.OBSERVED_SPY_STEALTH_0388_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_SPY_STEALTH_0388_SHA256',inspect.getsource(m.install))

    def test_0389_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/spy-stealth-0389/baseline-0388.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_SPY_STEALTH_0388_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0389')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_STEALTH_0388_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_SPY_STEALTH_0388_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_STEALTH_0388_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0388_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-blackmail-cost-0387.json').read_text())
        self.assertEqual(m.OBSERVED_BLACKMAIL_COST_0387_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_BLACKMAIL_COST_0387_SHA256',inspect.getsource(m.install))

    def test_0388_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/blackmail-cost-0388/baseline-0387.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_BLACKMAIL_COST_0387_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0388')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_COST_0387_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_BLACKMAIL_COST_0387_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_COST_0387_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0387_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-smuggle-sanctuary-0386.json').read_text())
        self.assertEqual(m.OBSERVED_SMUGGLE_SANCTUARY_0386_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_SMUGGLE_SANCTUARY_0386_SHA256',inspect.getsource(m.install))

    def test_0387_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/smuggle-sanctuary-0387/baseline-0386.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_SMUGGLE_SANCTUARY_0386_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0387')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_SMUGGLE_SANCTUARY_0386_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_SMUGGLE_SANCTUARY_0386_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_SMUGGLE_SANCTUARY_0386_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0386_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-unit-blackmail-0385.json').read_text())
        self.assertEqual(m.OBSERVED_UNIT_BLACKMAIL_0385_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_UNIT_BLACKMAIL_0385_SHA256',inspect.getsource(m.install))

    def test_0386_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/unit-blackmail-0386/baseline-0385.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_UNIT_BLACKMAIL_0385_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0386')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_UNIT_BLACKMAIL_0385_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_UNIT_BLACKMAIL_0385_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_UNIT_BLACKMAIL_0385_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0385_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-blackmail-relay-0384.json').read_text())
        self.assertEqual(m.OBSERVED_BLACKMAIL_RELAY_0384_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_BLACKMAIL_RELAY_0384_SHA256',inspect.getsource(m.install))

    def test_0385_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/blackmail-relay-0385/baseline-0384.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_BLACKMAIL_RELAY_0384_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0385')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_RELAY_0384_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_BLACKMAIL_RELAY_0384_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_RELAY_0384_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0384_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-blackmail-layout-0383.json').read_text())
        self.assertEqual(m.OBSERVED_BLACKMAIL_LAYOUT_0383_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_BLACKMAIL_LAYOUT_0383_SHA256',inspect.getsource(m.install))

    def test_0384_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/blackmail-layout-0384/baseline-0383.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_BLACKMAIL_LAYOUT_0383_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0384')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_LAYOUT_0383_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_BLACKMAIL_LAYOUT_0383_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_LAYOUT_0383_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0383_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-online-baseline-0382.json').read_text())
        self.assertEqual(m.OBSERVED_ONLINE_BASELINE_0382_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ONLINE_BASELINE_0382_SHA256',inspect.getsource(m.install))

    def test_0383_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/online-sync-0383/baseline-0382.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_ONLINE_BASELINE_0382_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0383')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_ONLINE_BASELINE_0382_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_ONLINE_BASELINE_0382_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_ONLINE_BASELINE_0382_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0382_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-spy-balance-0381.json').read_text())
        self.assertEqual(m.OBSERVED_SPY_BALANCE_0381_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_SPY_BALANCE_0381_SHA256',inspect.getsource(m.install))

    def test_0382_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0382/baseline-0381.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_SPY_BALANCE_0381_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0382')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_BALANCE_0381_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_SPY_BALANCE_0381_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_BALANCE_0381_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0381_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-badge-0380.json').read_text())
        self.assertEqual(m.OBSERVED_BADGE_0380_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_BADGE_0380_SHA256',inspect.getsource(m.install))

    def test_0381_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0381/baseline-0380.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_BADGE_0380_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0381')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_BADGE_0380_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_BADGE_0380_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_BADGE_0380_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0380_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-cloak-0379.json').read_text())
        self.assertEqual(m.OBSERVED_CLOAK_0379_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_CLOAK_0379_SHA256',inspect.getsource(m.install))

    def test_0380_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0380/baseline-0379.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_CLOAK_0379_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0380')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_CLOAK_0379_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_CLOAK_0379_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_CLOAK_0379_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0379_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-native-focus-0378.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_FOCUS_0378_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_FOCUS_0378_SHA256',inspect.getsource(m.install))

    def test_0379_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0379/baseline-0378.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_NATIVE_FOCUS_0378_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0379')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_NATIVE_FOCUS_0378_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_NATIVE_FOCUS_0378_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_NATIVE_FOCUS_0378_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0378_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-rogue-crash-0377.json').read_text())
        self.assertEqual(m.OBSERVED_ROGUE_CRASH_0377_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_ROGUE_CRASH_0377_SHA256',inspect.getsource(m.install))

    def test_0378_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0378/baseline-0377.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_ROGUE_CRASH_0377_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0378')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_ROGUE_CRASH_0377_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_ROGUE_CRASH_0377_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_ROGUE_CRASH_0377_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0377_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-spy-actions-0376.json').read_text())
        self.assertEqual(m.OBSERVED_SPY_ACTIONS_0376_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_SPY_ACTIONS_0376_SHA256',inspect.getsource(m.install))

    def test_0377_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0377/baseline-0376.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_SPY_ACTIONS_0376_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0377')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_ACTIONS_0376_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_SPY_ACTIONS_0376_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_ACTIONS_0376_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0376_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-preview-loading-0375.json').read_text())
        self.assertEqual(m.OBSERVED_PREVIEW_LOADING_0375_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_PREVIEW_LOADING_0375_SHA256',inspect.getsource(m.install))

    def test_0376_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0376/baseline-0375.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_PREVIEW_LOADING_0375_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0376')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_PREVIEW_LOADING_0375_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_PREVIEW_LOADING_0375_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_PREVIEW_LOADING_0375_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0375_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-save-fog-0374.json').read_text())
        self.assertEqual(m.OBSERVED_SAVE_FOG_0374_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_SAVE_FOG_0374_SHA256',inspect.getsource(m.install))

    def test_0375_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0375/baseline-0374.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_SAVE_FOG_0374_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0375')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_SAVE_FOG_0374_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_SAVE_FOG_0374_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_SAVE_FOG_0374_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0374_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-spy-disguise-0373.json').read_text())
        self.assertEqual(m.OBSERVED_SPY_DISGUISE_0373_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_SPY_DISGUISE_0373_SHA256',inspect.getsource(m.install))

    def test_0374_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0374/baseline-0373.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_SPY_DISGUISE_0373_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0374')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_DISGUISE_0373_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_SPY_DISGUISE_0373_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_DISGUISE_0373_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0373_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-spymaid-fairy-0372.json').read_text())
        self.assertEqual(m.OBSERVED_SPYMAID_FAIRY_0372_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_SPYMAID_FAIRY_0372_SHA256',inspect.getsource(m.install))

    def test_0373_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0373/baseline-0372.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_SPYMAID_FAIRY_0372_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0373')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_SPYMAID_FAIRY_0372_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_SPYMAID_FAIRY_0372_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_SPYMAID_FAIRY_0372_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0372_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-aquarion-repair-0371.json').read_text())
        self.assertEqual(m.OBSERVED_AQUARION_REPAIR_0371_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_AQUARION_REPAIR_0371_SHA256',inspect.getsource(m.install))

    def test_0372_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0372/baseline-0371.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_AQUARION_REPAIR_0371_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0372')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_AQUARION_REPAIR_0371_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_AQUARION_REPAIR_0371_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_AQUARION_REPAIR_0371_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0369_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-aquarion-0370.json').read_text())
        self.assertEqual(m.OBSERVED_AQUARION_0370_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_AQUARION_0370_SHA256',inspect.getsource(m.install))

    def test_0369_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0371/baseline-0370.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_AQUARION_0370_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0369')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_AQUARION_0370_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_AQUARION_0370_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_AQUARION_0370_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')


    def test_0368_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-blackmail-ui-0368.json').read_text())
        self.assertEqual(m.OBSERVED_BLACKMAIL_UI_0368_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_BLACKMAIL_UI_0368_SHA256',inspect.getsource(m.install))

    def test_0368_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0369/baseline-0368.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_BLACKMAIL_UI_0368_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0368')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_UI_0368_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_BLACKMAIL_UI_0368_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_UI_0368_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0367_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-blackmail-base-0367.json').read_text())
        self.assertEqual(m.OBSERVED_BLACKMAIL_BASE_0367_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_BLACKMAIL_BASE_0367_SHA256',inspect.getsource(m.install))

    def test_0367_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0368/baseline-0367.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_BLACKMAIL_BASE_0367_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0367')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_BASE_0367_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_BLACKMAIL_BASE_0367_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_BLACKMAIL_BASE_0367_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0366_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-observer-0366.json').read_text())
        self.assertEqual(m.OBSERVED_OBSERVER_0366_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_OBSERVER_0366_SHA256',inspect.getsource(m.install))

    def test_0366_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0367/baseline-0366.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_OBSERVER_0366_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0366')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_OBSERVER_0366_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_OBSERVER_0366_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_OBSERVER_0366_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0365_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-support-0365.json').read_text())
        self.assertEqual(m.OBSERVED_SUPPORT_0365_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_SUPPORT_0365_SHA256',inspect.getsource(m.install))

    def test_0365_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0366/baseline-0365.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_SUPPORT_0365_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0366')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_SUPPORT_0365_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_SUPPORT_0365_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_SUPPORT_0365_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0364_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-startup-0364.json').read_text())
        self.assertEqual(m.OBSERVED_STARTUP_0364_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_STARTUP_0364_SHA256',inspect.getsource(m.install))

    def test_0364_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0365/baseline-0364.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_STARTUP_0364_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0365')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_STARTUP_0364_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_STARTUP_0364_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_STARTUP_0364_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0363_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-hotseat-0363.json').read_text())
        self.assertEqual(m.OBSERVED_HOTSEAT_0363_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_HOTSEAT_0363_SHA256',inspect.getsource(m.install))

    def test_0363_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0364/baseline-0363.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_HOTSEAT_0363_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0364')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_HOTSEAT_0363_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_HOTSEAT_0363_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_HOTSEAT_0363_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0362_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/"evidence/observed-espionage-0362.json").read_text())
        self.assertEqual(m.OBSERVED_ESPIONAGE_0362_SHA256,evidence["sha256"])
        self.assertIn("OBSERVED_ESPIONAGE_0362_SHA256",inspect.getsource(m.install))

    def test_0362_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0363/baseline-0362.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_ESPIONAGE_0362_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0363')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_ESPIONAGE_0362_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_ESPIONAGE_0362_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaises(m.Stop):m.install_archive(artifact,destination,backups,{m.OBSERVED_ESPIONAGE_0362_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')

    def test_0361_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/"evidence/observed-spy-0361.json").read_text())
        self.assertEqual(m.OBSERVED_SPY_0361_SHA256,evidence["sha256"])
        self.assertIn("OBSERVED_SPY_0361_SHA256",inspect.getsource(m.install))

    def test_0360_exact_observed_predecessor_is_recognized(self):
        import inspect
        evidence=json.loads((ROOT/"evidence/observed-repair-0360.json").read_text())
        self.assertEqual(m.OBSERVED_REPAIR_0360_SHA256,evidence["sha256"])
        self.assertIn("OBSERVED_REPAIR_0360_SHA256",inspect.getsource(m.install))

    def test_observed_hash_has_recorded_provenance_and_is_accepted_by_install(self):
        evidence=json.loads((ROOT/'evidence/observed-m1e-archive.json').read_text())
        self.assertEqual(m.OBSERVED_M1E_SHA256,evidence['installed_mod_sha256'])
        self.assertEqual(evidence['version'],'0.2.5')
        self.assertEqual(evidence['report_sha256'],'81d331110aa6deedfafb48f5cfcef37b322391c71fc7a126ea79a4f626d2c0a8')
        import inspect
        self.assertIn('OBSERVED_M1E_SHA256',inspect.getsource(m.install))
        diagnostic=json.loads((ROOT/'evidence/observed-m1e-diagnostic-026.json').read_text())
        self.assertEqual(m.OBSERVED_M1E_DIAGNOSTIC_026_SHA256,diagnostic['installed_mod_sha256'])
        self.assertIn('OBSERVED_M1E_DIAGNOSTIC_026_SHA256',inspect.getsource(m.install))
        repair=json.loads((ROOT/'evidence/observed-m1e-repair-027.json').read_text())
        self.assertEqual(m.OBSERVED_M1E_REPAIR_027_SHA256,repair['installed_mod_sha256'])
        self.assertIn('OBSERVED_M1E_REPAIR_027_SHA256',inspect.getsource(m.install))
        latest=json.loads((ROOT/'evidence/observed-m1e-repair-028.json').read_text())
        self.assertEqual(m.OBSERVED_M1E_REPAIR_028_SHA256,latest['installed_mod_sha256'])
        self.assertIn('OBSERVED_M1E_REPAIR_028_SHA256',inspect.getsource(m.install))
        framed=json.loads((ROOT/'evidence/observed-m1e-repair-029.json').read_text())
        self.assertEqual(m.OBSERVED_M1E_REPAIR_029_SHA256,framed['installed_mod_sha256'])
        self.assertIn('OBSERVED_M1E_REPAIR_029_SHA256',inspect.getsource(m.install))

        current=json.loads((ROOT/'evidence/observed-m1e-repair-0210.json').read_text())
        self.assertEqual(m.OBSERVED_M1E_REPAIR_0210_SHA256,current['installed_mod_sha256'])
        self.assertIn('OBSERVED_M1E_REPAIR_0210_SHA256',inspect.getsource(m.install))

        session=json.loads((ROOT/'evidence/observed-m1e-repair-0211.json').read_text())
        self.assertEqual(m.OBSERVED_M1E_REPAIR_0211_SHA256,session['installed_mod_sha256'])
        self.assertIn('OBSERVED_M1E_REPAIR_0211_SHA256',inspect.getsource(m.install))

        candidate=json.loads((ROOT/'evidence/observed-session-map-0212.json').read_text())
        self.assertEqual(m.OBSERVED_SESSION_MAP_0212_SHA256,candidate['installed_mod_sha256'])
        self.assertIn('OBSERVED_SESSION_MAP_0212_SHA256',inspect.getsource(m.install))
        badges=json.loads((ROOT/'evidence/observed-session-map-0213.json').read_text())
        self.assertEqual(m.OBSERVED_SESSION_MAP_0213_SHA256,badges['installed_mod_sha256'])
        self.assertIn('OBSERVED_SESSION_MAP_0213_SHA256',inspect.getsource(m.install))
        focused=json.loads((ROOT/'evidence/observed-session-map-0214.json').read_text())
        self.assertEqual(m.OBSERVED_SESSION_MAP_0214_SHA256,focused['installed_mod_sha256'])
        self.assertIn('OBSERVED_SESSION_MAP_0214_SHA256',inspect.getsource(m.install))
        fixed=json.loads((ROOT/'evidence/observed-session-map-0215.json').read_text())
        self.assertEqual(m.OBSERVED_SESSION_MAP_0215_SHA256,fixed['installed_mod_sha256'])
        self.assertIn('OBSERVED_SESSION_MAP_0215_SHA256',inspect.getsource(m.install))
        two_slots=json.loads((ROOT/'evidence/observed-session-map-0216.json').read_text())
        self.assertEqual(m.OBSERVED_SESSION_MAP_0216_SHA256,two_slots['installed_mod_sha256'])
        self.assertIn('OBSERVED_SESSION_MAP_0216_SHA256',inspect.getsource(m.install))
        smuggle=json.loads((ROOT/'evidence/observed-smuggle-030.json').read_text())
        self.assertEqual(m.OBSERVED_SMUGGLE_030_SHA256,smuggle['installed_archive_sha256'])
        self.assertIn('OBSERVED_SMUGGLE_030_SHA256',inspect.getsource(m.install))
        diagnostic=json.loads((ROOT/'evidence/observed-smuggle-031.json').read_text())
        self.assertEqual(m.OBSERVED_SMUGGLE_031_SHA256,diagnostic['installed_archive_sha256'])
        self.assertIn('OBSERVED_SMUGGLE_031_SHA256',inspect.getsource(m.install))
        candidate032=json.loads((ROOT/'evidence/observed-smuggle-032.json').read_text())
        self.assertEqual(m.OBSERVED_SMUGGLE_032_SHA256,candidate032['installed_archive_sha256'])
        self.assertIn('OBSERVED_SMUGGLE_032_SHA256',inspect.getsource(m.install))
        candidate033=json.loads((ROOT/'evidence/observed-smuggle-033.json').read_text())
        self.assertEqual(m.OBSERVED_SMUGGLE_033_SHA256,candidate033['installed_archive_sha256'])
        self.assertIn('OBSERVED_SMUGGLE_033_SHA256',inspect.getsource(m.install))
        candidate034=json.loads((ROOT/'evidence/observed-smuggle-034.json').read_text())
        self.assertEqual(m.OBSERVED_SMUGGLE_034_SHA256,candidate034['installed_archive_sha256'])
        self.assertIn('OBSERVED_SMUGGLE_034_SHA256',inspect.getsource(m.install))
        candidate035=json.loads((ROOT/'evidence/observed-native-probe-035.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_PROBE_035_SHA256,candidate035['installed_archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_PROBE_035_SHA256',inspect.getsource(m.install))
        candidate036=json.loads((ROOT/'evidence/observed-native-envelope-036.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_ENVELOPE_036_SHA256,candidate036['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_ENVELOPE_036_SHA256',inspect.getsource(m.install))
        candidate037=json.loads((ROOT/'evidence/observed-native-bridge-037.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_BRIDGE_037_SHA256,candidate037['installed_archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_BRIDGE_037_SHA256',inspect.getsource(m.install))

        candidate038=json.loads((ROOT/'evidence/observed-native-ledger-038.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_LEDGER_038_SHA256,candidate038['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_LEDGER_038_SHA256',inspect.getsource(m.install))

        candidate0311=json.loads((ROOT/'evidence/observed-exchange-probe-0311.json').read_text())
        self.assertEqual(m.OBSERVED_EXCHANGE_PROBE_0311_SHA256,candidate0311['archive_sha256'])
        self.assertIn('OBSERVED_EXCHANGE_PROBE_0311_SHA256',inspect.getsource(m.install))
        candidate0312=json.loads((ROOT/'evidence/observed-registry-exchange-0312.json').read_text())
        self.assertEqual(m.OBSERVED_REGISTRY_EXCHANGE_0312_SHA256,candidate0312['archive_sha256'])
        self.assertIn('OBSERVED_REGISTRY_EXCHANGE_0312_SHA256',inspect.getsource(m.install))
        candidate0313=json.loads((ROOT/'evidence/observed-exchange-sight-0313.json').read_text())
        self.assertEqual(m.OBSERVED_EXCHANGE_SIGHT_0313_SHA256,candidate0313['archive_sha256'])
        self.assertIn('OBSERVED_EXCHANGE_SIGHT_0313_SHA256',inspect.getsource(m.install))
        candidate0314=json.loads((ROOT/'evidence/observed-exchange-children-0314.json').read_text())
        self.assertEqual(m.OBSERVED_EXCHANGE_CHILDREN_0314_SHA256,candidate0314['archive_sha256'])
        self.assertIn('OBSERVED_EXCHANGE_CHILDREN_0314_SHA256',inspect.getsource(m.install))
        candidate0315=json.loads((ROOT/'evidence/observed-native-ownership-0315.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_OWNERSHIP_0315_SHA256,candidate0315['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_OWNERSHIP_0315_SHA256',inspect.getsource(m.install))
        candidate0316=json.loads((ROOT/'evidence/observed-native-session-0316.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_SESSION_0316_SHA256,candidate0316['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_SESSION_0316_SHA256',inspect.getsource(m.install))

        candidate0317=json.loads((ROOT/'evidence/observed-native-session-0317.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_SESSION_0317_SHA256,candidate0317['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_SESSION_0317_SHA256',inspect.getsource(m.install))

        candidate0318=json.loads((ROOT/'evidence/observed-native-execution-0318.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_EXECUTION_0318_SHA256,candidate0318['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_EXECUTION_0318_SHA256',inspect.getsource(m.install))

        candidate0319=json.loads((ROOT/'evidence/observed-native-execution-0319.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_EXECUTION_0319_SHA256,candidate0319['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_EXECUTION_0319_SHA256',inspect.getsource(m.install))

        candidate0320=json.loads((ROOT/'evidence/observed-native-execution-0320.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_EXECUTION_0320_SHA256,candidate0320['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_EXECUTION_0320_SHA256',inspect.getsource(m.install))

        candidate0321=json.loads((ROOT/'evidence/observed-native-execution-0321.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_EXECUTION_0321_SHA256,candidate0321['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_EXECUTION_0321_SHA256',inspect.getsource(m.install))

        candidate0322=json.loads((ROOT/'evidence/observed-native-execution-0322.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_EXECUTION_0322_SHA256,candidate0322['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_EXECUTION_0322_SHA256',inspect.getsource(m.install))

        candidate0323=json.loads((ROOT/'evidence/observed-native-execution-0323.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_EXECUTION_0323_SHA256,candidate0323['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_EXECUTION_0323_SHA256',inspect.getsource(m.install))
        candidate0324=json.loads((ROOT/'evidence/observed-native-paid-birth-0324.json').read_text())
        self.assertEqual(m.OBSERVED_NATIVE_PAID_BIRTH_0324_SHA256,candidate0324['archive_sha256'])
        self.assertIn('OBSERVED_NATIVE_PAID_BIRTH_0324_SHA256',inspect.getsource(m.install))
        candidate0325=json.loads((ROOT/'evidence/observed-a11-0325.json').read_text())
        self.assertEqual(m.OBSERVED_A11_0325_SHA256,candidate0325['archive_sha256'])
        self.assertIn('OBSERVED_A11_0325_SHA256',inspect.getsource(m.install))
        candidate0326=json.loads((ROOT/'evidence/observed-a11-0326.json').read_text())
        self.assertEqual(m.OBSERVED_A11_0326_SHA256,candidate0326['archive_sha256'])
        self.assertIn('OBSERVED_A11_0326_SHA256',inspect.getsource(m.install))
        candidate0327=json.loads((ROOT/'evidence/observed-a11-0327.json').read_text())
        self.assertEqual(m.OBSERVED_A11_0327_SHA256,candidate0327['archive_sha256'])
        self.assertIn('OBSERVED_A11_0327_SHA256',inspect.getsource(m.install))
        candidate0338=json.loads((ROOT/'evidence/observed-build-factory-0338.json').read_text())
        self.assertEqual(m.OBSERVED_BUILD_FACTORY_0338_SHA256,candidate0338['archive_sha256'])
        self.assertIn('OBSERVED_BUILD_FACTORY_0338_SHA256',inspect.getsource(m.install))
        candidate0337=json.loads((ROOT/'evidence/observed-build-0337.json').read_text())
        self.assertEqual(m.OBSERVED_BUILD_0337_SHA256,candidate0337['archive_sha256'])
        self.assertIn('OBSERVED_BUILD_0337_SHA256',inspect.getsource(m.install))
        candidate0336=json.loads((ROOT/'evidence/observed-command-0336.json').read_text())
        self.assertEqual(m.OBSERVED_COMMAND_0336_SHA256,candidate0336['archive_sha256'])
        self.assertIn('OBSERVED_COMMAND_0336_SHA256',inspect.getsource(m.install))
        candidate0335=json.loads((ROOT/'evidence/observed-command-0335.json').read_text())
        self.assertEqual(m.OBSERVED_COMMAND_0335_SHA256,candidate0335['archive_sha256'])
        self.assertIn('OBSERVED_COMMAND_0335_SHA256',inspect.getsource(m.install))
        candidate0334=json.loads((ROOT/'evidence/observed-command-0334.json').read_text())
        self.assertEqual(m.OBSERVED_COMMAND_0334_SHA256,candidate0334['archive_sha256'])
        self.assertIn('OBSERVED_COMMAND_0334_SHA256',inspect.getsource(m.install))
        candidate0333=json.loads((ROOT/'evidence/observed-train-0333.json').read_text())
        self.assertEqual(m.OBSERVED_TRAIN_0333_SHA256,candidate0333['archive_sha256'])
        self.assertIn('OBSERVED_TRAIN_0333_SHA256',inspect.getsource(m.install))
        candidate0332=json.loads((ROOT/'evidence/observed-train-0332.json').read_text())
        self.assertEqual(m.OBSERVED_TRAIN_0332_SHA256,candidate0332['archive_sha256'])
        self.assertIn('OBSERVED_TRAIN_0332_SHA256',inspect.getsource(m.install))
        candidate0331=json.loads((ROOT/'evidence/observed-train-0331.json').read_text())
        self.assertEqual(m.OBSERVED_TRAIN_0331_SHA256,candidate0331['archive_sha256'])
        self.assertIn('OBSERVED_TRAIN_0331_SHA256',inspect.getsource(m.install))
        candidate0330=json.loads((ROOT/'evidence/observed-train-0330.json').read_text())
        self.assertEqual(m.OBSERVED_TRAIN_0330_SHA256,candidate0330['archive_sha256'])
        self.assertIn('OBSERVED_TRAIN_0330_SHA256',inspect.getsource(m.install))
        candidate0329=json.loads((ROOT/'evidence/observed-a11-0329.json').read_text())
        self.assertEqual(m.OBSERVED_A11_0329_SHA256,candidate0329['archive_sha256'])
        self.assertIn('OBSERVED_A11_0329_SHA256',inspect.getsource(m.install))
        candidate0328=json.loads((ROOT/'evidence/observed-a11-0328.json').read_text())
        self.assertEqual(m.OBSERVED_A11_0328_SHA256,candidate0328['archive_sha256'])
        self.assertIn('OBSERVED_A11_0328_SHA256',inspect.getsource(m.install))

        candidate0310=json.loads((ROOT/'evidence/observed-command-probe-0310.json').read_text())
        self.assertEqual(m.OBSERVED_COMMAND_PROBE_0310_SHA256,candidate0310['archive_sha256'])
        self.assertIn('OBSERVED_COMMAND_PROBE_0310_SHA256',inspect.getsource(m.install))

        candidate039=json.loads((ROOT/'evidence/observed-command-probe-039.json').read_text())
        self.assertEqual(m.OBSERVED_COMMAND_PROBE_039_SHA256,candidate039['archive_sha256'])
        self.assertIn('OBSERVED_COMMAND_PROBE_039_SHA256',inspect.getsource(m.install))

    def test_recognized_prior_bytes_have_exact_backup(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'synthetic observed M1E';destination.write_bytes(prior)
            backups=root/'backups'
            with mock.patch.object(m,'OBSERVED_BUILD_FACTORY_0338_SHA256',m.digest(prior)):
                self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_BUILD_FACTORY_0338_SHA256}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual([p.read_bytes() for p in backups.iterdir()],[prior])

    def test_art_baseline_0339_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-baseline-0339.json').read_text())
        self.assertEqual(m.OBSERVED_ART_BASELINE_0339_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_BASELINE_0339_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0340_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0340.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0340_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0340_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0341_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0341.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0341_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0341_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0342_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0342.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0342_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0342_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0343_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0343.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0343_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0343_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0344_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0344.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0344_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0344_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0345_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0345.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0345_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0345_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0346_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0346.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0346_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0346_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0347_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0347.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0347_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0347_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0348_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0348.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0348_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0348_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_art_0349_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-art-0349.json').read_text())
        self.assertEqual(m.OBSERVED_ART_0349_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_ART_0349_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known art baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_regroup_0350_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-regroup-0350.json').read_text())
        self.assertEqual(m.OBSERVED_REGROUP_0350_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_REGROUP_0350_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new regroup build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known regroup baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_regroup_0351_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-regroup-0351.json').read_text())
        self.assertEqual(m.OBSERVED_REGROUP_0351_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_REGROUP_0351_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new regroup build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known regroup baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_regroup_0352_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-regroup-0352.json').read_text())
        self.assertEqual(m.OBSERVED_REGROUP_0352_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_REGROUP_0352_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new regroup build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known regroup baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_regroup_0353_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-regroup-0353.json').read_text())
        self.assertEqual(m.OBSERVED_REGROUP_0353_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_REGROUP_0353_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new regroup build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known regroup baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_regroup_0354_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-regroup-0354.json').read_text())
        self.assertEqual(m.OBSERVED_REGROUP_0354_SHA256,evidence['archive_sha256'])
        self.assertIn('OBSERVED_REGROUP_0354_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new regroup build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known regroup baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)

    def test_growing_0355_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-growing-0355.json').read_text())
        self.assertEqual(m.OBSERVED_GROWING_0355_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_GROWING_0355_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new branch art build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known growing disguise baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)
            destination.write_bytes(b'unknown modified archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(artifact,destination,backups,{m.digest(prior)})
            self.assertEqual(destination.read_bytes(),b'unknown modified archive')
            self.assertEqual(len(list(backups.glob('*.polymod'))),1)

    def test_icons_0356_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-icons-0356.json').read_text())
        self.assertEqual(m.OBSERVED_ICONS_0356_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_ICONS_0356_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new naval and disguise build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known icons baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)
            destination.write_bytes(b'unknown modified archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(artifact,destination,backups,{m.digest(prior)})
            self.assertEqual(destination.read_bytes(),b'unknown modified archive')
            self.assertEqual(len(list(backups.glob('*.polymod'))),1)

    def test_naval_0357_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-naval-0357.json').read_text())
        self.assertEqual(m.OBSERVED_NAVAL_0357_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_NAVAL_0357_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new naval feedback build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known naval baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)
            destination.write_bytes(b'unknown modified archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(artifact,destination,backups,{m.digest(prior)})
            self.assertEqual(destination.read_bytes(),b'unknown modified archive')
            self.assertEqual(len(list(backups.glob('*.polymod'))),1)

    def test_feedback_0358_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-feedback-0358.json').read_text())
        self.assertEqual(m.OBSERVED_FEEDBACK_0358_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_FEEDBACK_0358_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new naval feedback build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known naval baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)
            destination.write_bytes(b'unknown modified archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(artifact,destination,backups,{m.digest(prior)})
            self.assertEqual(destination.read_bytes(),b'unknown modified archive')
            self.assertEqual(len(list(backups.glob('*.polymod'))),1)

    def test_crash_0359_is_recognized_and_backed_up(self):
        import inspect
        evidence=json.loads((ROOT/'evidence/observed-crash-0359.json').read_text())
        self.assertEqual(m.OBSERVED_CRASH_0359_SHA256,evidence['sha256'])
        self.assertIn('OBSERVED_CRASH_0359_SHA256',inspect.getsource(m.install))
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);artifact=root/'candidate';artifact.write_bytes(b'new movement repair build')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'known crashed baseline';destination.write_bytes(prior);backups=root/'backup'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.digest(prior)}),'UPGRADED')
            self.assertEqual(destination.read_bytes(),artifact.read_bytes())
            self.assertEqual(next(backups.glob('*.polymod')).read_bytes(),prior)
            destination.write_bytes(b'unknown modified archive')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(artifact,destination,backups,{m.digest(prior)})
            self.assertEqual(destination.read_bytes(),b'unknown modified archive')
            self.assertEqual(len(list(backups.glob('*.polymod'))),1)

    def test_0361_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0362/baseline-0361.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_SPY_0361_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0361')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_0361_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_SPY_0361_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(artifact,destination,backups,{m.OBSERVED_SPY_0361_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')
            self.assertEqual(len(list(backups.glob('*.polymod'))),1)

    def test_0360_known_backup_and_unknown_rejection(self):
        baseline=ROOT.parent/'run-output/full-branch-implementation/candidate-0361/baseline-0360.polymod'
        self.assertEqual(m.sha(baseline),m.OBSERVED_REPAIR_0360_SHA256)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate0361')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir();destination.write_bytes(baseline.read_bytes())
            backups=root/'backups'
            self.assertEqual(m.install_archive(artifact,destination,backups,{m.OBSERVED_REPAIR_0360_SHA256}),'UPGRADED')
            self.assertEqual(m.sha(next(backups.glob('*.polymod'))),m.OBSERVED_REPAIR_0360_SHA256)
            destination.write_bytes(b'unknown replacement')
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(artifact,destination,backups,{m.OBSERVED_REPAIR_0360_SHA256})
            self.assertEqual(destination.read_bytes(),b'unknown replacement')
            self.assertEqual(len(list(backups.glob('*.polymod'))),1)

    def test_unknown_prior_stays_in_place_without_backup_or_stage(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);artifact=root/'candidate';artifact.write_bytes(b'synthetic candidate')
            destination=root/'Mods'/m.MODNAME;destination.parent.mkdir()
            prior=b'unrecognized archive';destination.write_bytes(prior)
            with self.assertRaisesRegex(m.Stop,'Preserve it in place'):
                m.install_archive(artifact,destination,root/'backups',{m.OBSERVED_M1E_SHA256})
            self.assertEqual(destination.read_bytes(),prior)
            self.assertFalse((root/'backups').exists())
            self.assertEqual(list(destination.parent.iterdir()),[destination])

if __name__=='__main__':unittest.main()
