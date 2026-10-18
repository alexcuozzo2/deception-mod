"""Paired preview installation file-conflict controls; no live installation."""
import importlib.util,tempfile,unittest
from pathlib import Path
S=importlib.util.spec_from_file_location('preview_install',Path(__file__).resolve().parents[1]/'tools/install_polymod_preview.py')
p=importlib.util.module_from_spec(S);S.loader.exec_module(p)
class PreviewInstallTests(unittest.TestCase):
    def test_exact_destination_replaced(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d).resolve();source=root/'new';target=root/'PolyMod.dll';source.write_bytes(b'new');target.write_bytes(b'old')
            p.publish(source,target,p.m.sha(target));self.assertEqual(target.read_bytes(),b'new');self.assertEqual(source.read_bytes(),b'new')
    def test_unknown_destination_untouched(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d).resolve();source=root/'new';target=root/'PolyMod.dll';source.write_bytes(b'new');target.write_bytes(b'unknown')
            with self.assertRaises(p.m.Stop):p.publish(source,target,p.OLD)
            self.assertEqual(target.read_bytes(),b'unknown')
    def test_linked_destination_and_parent_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d).resolve();real=root/'real';real.mkdir();target=real/'PolyMod.dll';target.write_bytes(b'old');source=root/'new';source.write_bytes(b'new');link=root/'link';link.symlink_to(real,target_is_directory=True)
            with self.assertRaises(p.m.Stop):p.publish(source,link/'PolyMod.dll',p.m.sha(target))
            direct=root/'direct.dll';direct.symlink_to(target)
            with self.assertRaises(p.m.Stop):p.publish(source,direct,p.m.sha(target))
            self.assertEqual(target.read_bytes(),b'old')
    def test_exact_rollback_restores_bytes(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d).resolve();backup=root/'backup';source=root/'new';target=root/'PolyMod.dll';backup.write_bytes(b'old');source.write_bytes(b'new');target.write_bytes(b'old')
            p.publish(source,target,p.m.sha(backup));p.publish(backup,target,p.m.sha(source));self.assertEqual(target.read_bytes(),backup.read_bytes())
