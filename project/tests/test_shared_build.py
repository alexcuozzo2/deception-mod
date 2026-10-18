"""Fail-closed source/native reuse guards, using disposable inputs only."""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tools'))
import shared_build as builder


class SharedBuildTests(unittest.TestCase):
    def test_native_change_addition_and_removal_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve(); native=root/'src/Native';native.mkdir(parents=True)
            file=native/'bridge.cpp';file.write_text('audited')
            lock=root/'lock.json';lock.write_text(json.dumps({'guarded_files':[],'sources':{'src/Native/bridge.cpp':builder.api.sha(file)}}))
            with mock.patch.object(builder,'LOCK',lock):
                builder.validate_native_inputs(root)
                file.write_text('changed')
                with self.assertRaises(builder.api.Stop):builder.validate_native_inputs(root)
                file.write_text('audited');extra=native/'new.cpp';extra.write_text('new ABI')
                with self.assertRaises(builder.api.Stop):builder.validate_native_inputs(root)
                extra.unlink();file.unlink()
                with self.assertRaises(builder.api.Stop):builder.validate_native_inputs(root)

    def test_unknown_zip_stops_before_archive_read(self):
        with tempfile.TemporaryDirectory() as temp:
            path=Path(temp)/'candidate.zip';path.write_bytes(b'not a ZIP')
            with self.assertRaisesRegex(builder.api.Stop,'Unknown shared'):
                builder.donor_dll(path)

    def test_current_source_and_runtime_required(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();artifact=root/'mod';artifact.write_bytes(b'checked')
            result={'status':'MODEL_TESTS_PASSED_NATIVE_NOT_RUN','sources':{'source':'hash'},
                    'framework_and_config':{'runtime':'hash'},'artifact':str(artifact),'artifact_sha256':builder.api.sha(artifact)}
            path=root/'receipt.json';path.write_text(json.dumps(result))
            with mock.patch.object(builder.api,'same_tree_sources',return_value={'source':'hash'}),mock.patch.object(builder.api,'preflight',return_value={'runtime':'hash'}):
                builder.validate_receipt(path,root)
                artifact.write_bytes(b'tampered')
                with self.assertRaisesRegex(builder.api.Stop,'artifact differs'):builder.validate_receipt(path,root)
            with mock.patch.object(builder.api,'same_tree_sources',return_value={'source':'changed'}):
                with self.assertRaisesRegex(builder.api.Stop,'source is stale'):builder.validate_receipt(path,root)
            with mock.patch.object(builder.api,'same_tree_sources',return_value={'source':'hash'}),mock.patch.object(builder.api,'preflight',return_value={'runtime':'changed'}):
                with self.assertRaisesRegex(builder.api.Stop,'runtime/config differs'):builder.validate_receipt(path,root)


if __name__=='__main__':unittest.main()
