import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tools'))
import manage as api
import public_polymod as public

class PublicAdmissionTests(unittest.TestCase):
    def test_unknown_loader_refused_before_metadata(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'PolyMod.dll';p.write_bytes(b'not the official release')
            with patch.object(public,'CLI') as parser:
                with self.assertRaises(api.Stop):public.admit(api,p)
                parser.assert_not_called()
    def test_configuration_retains_other_framework_pins(self):
        original={'framework_checksums':{'other':'unchanged',public.REL:'old'},'reference_api_sha256':{'other':'unchanged','PolyMod.dll':'old'}}
        with patch.object(api,'CONTRACT',original),patch.object(public,'admit',return_value={'passed':True}):
            public.configure(api,Path('/test'))
            self.assertEqual(original['framework_checksums'],{'other':'unchanged',public.REL:public.SHA256})
            self.assertEqual(original['reference_api_sha256'],{'other':'unchanged','PolyMod.dll':public.SHA256})
if __name__=='__main__':unittest.main()
