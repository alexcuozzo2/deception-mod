"""Synthetic release safety and payload preservation checks; no installs/downloads."""
import io
import json
from pathlib import Path
import stat
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import repackage_shared_release as r


def zipped(files, modes=None):
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, 'w') as z:
        for name, data in files.items():
            info = zipfile.ZipInfo(name)
            info.create_system = 3
            info.external_attr = (stat.S_IFREG | (modes or {}).get(name, 0o644)) << 16
            z.writestr(info, data)
    return stream.getvalue()


class RepackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.repo = self.root / 'repo'
        templates = self.repo / 'distribution/mac-template'
        templates.mkdir(parents=True)
        (templates / 'setup.py').write_text("MOD_VERSION = '0.3.100'\n")
        (templates / 'setup.sh').write_text('#!/bin/sh\n')
        for name in ('MAC-UPGRADE.command', 'Install-Windows.ps1', 'SHARED-SETUP.md'):
            (self.repo / 'distribution' / name).write_text('current ' + name)
        (self.repo / 'project/src').mkdir(parents=True)
        (self.repo / 'project/src/source.cs').write_text('new source')
        (self.repo / 'distribution/repackage_shared_release.py').write_text('test builder')
        oldmod = zipped({'manifest.json': r.encoded({'version': '0.3.101'})})
        self.mod = self.root / 'new.polymod'
        self.mod.write_bytes(zipped({'manifest.json': r.encoded({'version': '0.3.102'})}))
        self.files = {
            r.MAC_MOD: oldmod, r.WIN_MOD: oldmod,
            r.MAC+'payload/runtime': b'mac runtime', r.WIN+'payload/runtime': b'windows runtime',
            r.MAC+'setup.sh': b'old script', r.ROOT+'Mac/Upgrade Deception.command': b'old upgrade',
            r.ROOT+'Sources/Native-resource-donor/old.polymod': b'historical donor',
            r.ROOT+'RELEASE.json': r.encoded({'version': '0.3.101', 'modSha256': r.sha(oldmod)}),
            r.MAC+'SHA256SUMS.txt': b'', r.ROOT+'SHA256SUMS.txt': b'',
        }
        for key, prefix, paths in [(r.MAC+'package-manifest.json', r.MAC, ['mod/alex_deception_m0.polymod', 'payload/runtime']),
                                   (r.WIN+'windows-manifest.json', r.WIN+'payload/', ['Mods/alex_deception_windows_candidate.polymod', 'runtime'])]:
            self.files[key] = r.encoded({'files': {p: r.sha(self.files[prefix+p]) for p in paths}})
        r.refresh_sums(self.files)
        self.base = self.root / 'base.zip'
        self.base.write_bytes(zipped(self.files, {r.MAC+'setup.sh': 0o755, r.ROOT+'Mac/Upgrade Deception.command': 0o755}))
        self.receipt = self.root / 'receipt.json'
        self.receipt.write_bytes(r.encoded({'status': 'SHARED_CANDIDATE_BUILT', 'version': '0.3.102', 'source_commit': 'a'*40,
            'artifact': str(self.mod), 'artifact_sha256': r.sha(self.mod.read_bytes()), 'source_dirty': False, 'sources': {'src/source.cs': r.sha(b'new source')}}))
        self.output = self.root / 'output'

    def build(self):
        with patch.object(r, 'BASE_SHA', r.sha(self.base.read_bytes())):
            return r.build(self.base, self.receipt, self.output, self.repo)

    def test_roundtrip_preserves_dependencies_and_modes(self):
        result = self.build()
        files, modes = r.unpack(result.read_bytes())
        r.verify_sums(files)
        self.assertEqual(files[r.MAC_MOD], self.mod.read_bytes())
        self.assertEqual(files[r.MAC_MOD], files[r.WIN_MOD])
        for name in (r.MAC+'payload/runtime', r.WIN+'payload/runtime', r.ROOT+'Sources/Native-resource-donor/old.polymod'):
            self.assertEqual(files[name], self.files[name])
        self.assertEqual(modes[r.MAC+'setup.sh'], 0o755)
        self.assertEqual(modes[r.ROOT+'Mac/Upgrade Deception.command'], 0o755)
        self.assertIn(b"'0.3.102'", files[r.MAC+'setup.py'])
        self.assertEqual(json.loads(files[r.WIN+'windows-manifest.json'])['version'], '0.3.102')
        with self.assertRaises(FileExistsError):
            self.build()

    def test_unpinned_base_rejected(self):
        with self.assertRaisesRegex(ValueError, 'pinned release'):
            r.build(self.base, self.receipt, self.output, self.repo)
        self.assertFalse(self.output.exists())

    def test_stale_source_rejected(self):
        (self.repo / 'project/src/source.cs').write_text('changed')
        with self.assertRaisesRegex(ValueError, 'Stale source'):
            self.build()
        self.assertFalse(self.output.exists())


    def test_added_source_rejected(self):
        (self.repo / 'project/src/new.cs').write_text('new')
        with self.assertRaisesRegex(ValueError, 'Stale source'):
            self.build()

    def test_dirty_receipt_rejected(self):
        receipt = json.loads(self.receipt.read_bytes())
        receipt['source_dirty'] = True
        self.receipt.write_bytes(r.encoded(receipt))
        with self.assertRaisesRegex(ValueError, 'must be clean'):
            self.build()

    def test_candidate_hash_rejected(self):
        self.mod.write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'Candidate checksum'):
            self.build()

    def test_manifest_version_rejected(self):
        receipt = json.loads(self.receipt.read_bytes())
        receipt['version'] = '0.3.103'
        self.receipt.write_bytes(r.encoded(receipt))
        with self.assertRaisesRegex(ValueError, 'manifest version'):
            self.build()

    def test_missing_checksum_coverage_rejected(self):
        self.files[r.ROOT+'rogue.txt'] = b'unlisted'
        with self.assertRaisesRegex(ValueError, 'coverage'):
            r.verify_sums(self.files)

    def test_manifest_extra_payload_rejected(self):
        self.files[r.MAC+'payload/unlisted'] = b'extra'
        with self.assertRaisesRegex(ValueError, 'coverage'):
            r.verify_manifest(self.files, r.MAC+'package-manifest.json', r.MAC, ('mod/', 'payload/'))

    def test_traversal_duplicates_symlinks_rejected(self):
        for name in ('../evil', '/absolute', 'C:/evil', 'a\\b', 'a/./b'):
            with self.subTest(name=name), self.assertRaises(ValueError):
                r.unpack(zipped({name: b'x'}))
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            r.unpack(zipped({'A': b'x', 'a': b'y'}))
        with self.assertRaisesRegex(ValueError, 'collision'):
            r.unpack(zipped({'a': b'x', 'a/b': b'y'}))
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, 'w') as z:
            info = zipfile.ZipInfo('link'); info.external_attr = (stat.S_IFLNK | 0o777) << 16
            z.writestr(info, 'target')
        with self.assertRaisesRegex(ValueError, 'Non-regular'):
            r.unpack(stream.getvalue())


if __name__ == '__main__':
    unittest.main()
