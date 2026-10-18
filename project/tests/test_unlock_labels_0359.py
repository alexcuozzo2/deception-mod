"""Player-visible unlock names are distinct from tech names and unit tags."""
import json
from pathlib import Path
import unittest


class UnlockLabelTests(unittest.TestCase):
    def test_technology_unlock_and_stat_labels_keep_their_requested_case(self):
        root = Path(__file__).resolve().parents[1]
        archive = json.loads((root / 'content/localization.json').read_text())
        # PolyMod encodes literal underscores twice and term separators once.
        english = {key.replace('__', '\0').replace('_', '.').replace('\0', '_'): value['English']
                   for key, value in archive.items()}
        self.assertEqual(english['technology.names.dcp_propaganda'], 'Propaganda')
        self.assertEqual(english['ability.names.dcp_disinformation'], 'Disinformation')
        self.assertEqual(english['technology.names.dcp_camouflage'], 'Counter Intelligence')
        self.assertEqual(english['ui.labels.dcp_disguise_unlock'], 'Disguise')
        self.assertEqual(english['ability.names.dcp_disguise'], 'DISGUISE')
        self.assertEqual(english['ability.names.dcp_regroup'], 'REGROUP')
        self.assertIn('Disinformation', english['technology.descriptions.dcp_propaganda'])
        self.assertIn('Disguise', english['technology.descriptions.dcp_propaganda'])
        self.assertNotEqual(english['ability.descriptions.dcp_disinformation'], english['technology.descriptions.dcp_propaganda'])


if __name__ == '__main__':
    unittest.main()
