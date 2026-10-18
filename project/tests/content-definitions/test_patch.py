"""Compare the shipped PolyMod definitions to the captured pre-refactor contract."""
import json
from pathlib import Path
import unittest

PROJECT = Path(__file__).resolve().parents[2]


class ContentPatchTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.patch = json.loads((PROJECT / 'content/patch.json').read_text())
        cls.native = json.loads((PROJECT / 'tests/native-tree.captured.json').read_text())

    def test_ordinary_gld_sections_only(self):
        self.assertEqual(set(self.patch), {'techData', 'unitData', 'tribeData'})
        self.assertEqual(len(self.patch['unitData']), 64)
        self.assertEqual(len(self.patch['techData']), 5)
        for section in ('techData', 'unitData'):
            for name, row in self.patch[section].items():
                self.assertTrue(name.startswith('dcp_'))
                self.assertEqual(row['idx'], -1)

    def test_units_exactly_preserve_captured_definitions(self):
        sources = dict(self.native['unitData'])
        for name, native, hp, reach, skills in [
            ('dcp_spy', 'warrior', 50, 0, ['scout']),
            ('dcp_spymaid', 'mermaidwarrior', 50, 0, ['scout', 'swim', 'amphibious']),
            ('dcp_motherfogger', 'warrior', 100, 2, ['fly', 'dash', 'scout', 'doubleattack']),
        ]:
            expected = dict(sources[native])
            expected.update(idx=-1, prefab=native, cost=5, health=hp, attack=0,
                            defence=10, range=reach, movement=2, promotionLimit=0,
                            hidden=False, independent=False, unitAbilities=skills)
            for key in ('upgradesFrom', 'promotesTo', 'growthRate', 'attractsTo'):
                expected.pop(key, None)
            self.assertEqual(self.patch['unitData'][name], expected, name)
            sources[name] = expected
        naval = {'transportship', 'scoutship', 'rammership', 'bombership',
                 'juggernaut', 'pirate', 'cloak_boat'}
        safe = {'escape', 'fortify', 'stiff', 'static', 'skate', 'sneak', 'creep',
                'swim', 'fly', 'land', 'amphibious', 'carry', 'water'}
        for name, row in self.patch['unitData'].items():
            if name in ('dcp_spy', 'dcp_spymaid', 'dcp_motherfogger'):
                continue
            native = row['prefab']
            expected = dict(sources[native])
            expected.update(idx=-1, prefab=native, cost=0 if native in naval else 1,
                            health=10, attack=0, defence=0, range=0, promotionLimit=0,
                            independent=False, hidden=False)
            for key in ('upgradesFrom', 'promotesTo', 'growthRate', 'embarksTo', 'attractsTo'):
                expected.pop(key, None)
            expected['unitAbilities'] = list(dict.fromkeys(
                [a for a in sources[native].get('unitAbilities', []) if a in safe] + ['dash']))
            self.assertEqual(row, expected, name)

    def test_unlocks_and_aquarion_references_resolve(self):
        techs = self.patch['techData']
        units = self.patch['unitData']
        for row in techs.values():
            self.assertTrue(set(row['techUnlocks']) <= techs.keys())
            self.assertTrue(set(row['unitUnlocks']) <= units.keys())
        self.assertEqual(techs['dcp_deception']['unitUnlocks'], ['dcp_mimic'] +
                         sorted(n for n in units if n.startswith('dcp_mimic_form_')))
        self.assertEqual(self.patch['tribeData'], {'aquarion': {'unitOverrides': {
            'dcp_mimic': 'dcp_mermimic', 'dcp_spy': 'dcp_spymaid'}}})


if __name__ == '__main__':
    unittest.main()
