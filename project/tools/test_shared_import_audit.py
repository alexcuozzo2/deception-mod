"""Focused import canonicalization and rejection tests; no native execution."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from shared_import_audit import Imports, audit


class SignatureTests(unittest.TestCase):
    def parser(self, mapping=None):
        reader = Imports.__new__(Imports)
        reader.token_type = lambda token: (mapping or {}).get(token, ('type', token))
        return reader

    def test_generic_signature_resolves_shifted_tokens(self):
        # instance generic method<T>(List<T>, ref X) -> X
        left = bytes([0x30, 1, 2, 0x12, 5, 0x15, 0x12, 9, 1, 0x1e, 0, 0x10, 0x12, 5])
        right = bytes([0x30, 1, 2, 0x12, 13, 0x15, 0x12, 17, 1, 0x1e, 0, 0x10, 0x12, 13])
        self.assertEqual(self.parser({5: 'X', 9: 'List'}).signature(left),
                         self.parser({13: 'X', 17: 'List'}).signature(right))

    def test_modifiers_and_calling_convention_retained(self):
        p = self.parser()
        self.assertNotEqual(p.signature(bytes([0, 0, 0x1f, 5, 1])),
                            p.signature(bytes([0, 0, 0x20, 5, 1])))
        self.assertNotEqual(p.signature(bytes([0, 0, 1])), p.signature(bytes([0x20, 0, 1])))

    def test_function_pointer_full_signature_retained(self):
        p = self.parser()
        self.assertNotEqual(p.signature(bytes([6, 0x1b, 0, 1, 1, 8])),
                            p.signature(bytes([6, 0x1b, 0, 1, 1, 9])))

    def test_array_dimensions_and_lower_bounds_retained(self):
        p = self.parser()
        self.assertNotEqual(p.signature(bytes([6, 0x14, 8, 1, 1, 5, 1, 0])),
                            p.signature(bytes([6, 0x14, 8, 1, 1, 5, 1, 2])))

    def test_field_and_byref_retained(self):
        p = self.parser()
        self.assertNotEqual(p.signature(bytes([6, 8])), p.signature(bytes([6, 0x10, 8])))

    def test_invalid_signatures_fail_closed(self):
        for blob in (b'', b'\x06', b'\x06\xff', b'\x06\x08\x00', b'\x00\x00\x12\xff'):
            with self.subTest(blob=blob), self.assertRaises(ValueError):
                self.parser().signature(blob)

    def test_scope_identity_retained(self):
        class FakeCLI:
            def row(self, table, rid):
                if table == 1:
                    return [rid * 4 + 2, 'Type', 'Namespace']
                return [1, 0, 0, 0, 0, rid, 'Assembly' + str(rid), '']
            def s(self, value): return value
            def blob(self, value): return bytes([value])
        p = Imports.__new__(Imports); p.cli = FakeCLI()
        self.assertNotEqual(p.typeref(1), p.typeref(2))

    def test_pinvoke_declaration_signature_retained(self):
        class FakeCLI:
            signature = bytes([0, 1, 1, 8])
            def row(self, table, rid):
                return [0, 0, 0, 0, self.signature, 0] if table == 6 else ['kernel32.dll']
            def blob(self, value): return value
            def s(self, value): return value
            def token(self, value): return 'Type::NativeMethod'
        p = Imports.__new__(Imports); p.cli = FakeCLI()
        original = p.pinvoke([256, 3, 'NativeEntry', 1])
        p.cli.signature = bytes([0, 1, 1, 9])
        self.assertNotEqual(original, p.pinvoke([256, 3, 'NativeEntry', 1]))

    def test_subset_and_novel_rejection(self):
        with tempfile.TemporaryDirectory() as temporary:
            candidate = Path(temporary) / 'candidate'; candidate.write_bytes(b'candidate')
            donor = Path(temporary) / 'donor'; donor.write_bytes(b'donor')
            baseline = {k: {'known'} for k in ('assemblies', 'types', 'members', 'pinvoke')}
            subset = {k: set() for k in baseline}
            with patch.object(Imports, '__init__', return_value=None), patch.object(Imports, 'collect', side_effect=[subset, baseline]):
                self.assertEqual(audit(candidate, donor)['status'], 'EXTERNAL_IMPORT_SUBSET_MATCH')
            for kind in baseline:
                changed = {k: set(v) for k, v in baseline.items()}; changed[kind].add('unknown')
                with self.subTest(kind=kind), patch.object(Imports, '__init__', return_value=None), patch.object(Imports, 'collect', side_effect=[changed, baseline]):
                    with self.assertRaisesRegex(ValueError, 'Novel emitted external imports'):
                        audit(candidate, donor)


class ReviewedImportsTests(unittest.TestCase):
    assembly = '["assembly","System.Diagnostics.Process",[6,0,0,0],0,"token",""]'

    def check(self, candidate, review):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'dll'; path.write_bytes(b'dll')
            baseline = {k: set() for k in ('assemblies', 'types', 'members', 'pinvoke')}
            with patch.object(Imports, '__init__', return_value=None), patch.object(Imports, 'collect', side_effect=[candidate, baseline]):
                return audit(path, path, review)

    def test_exact_review_passes(self):
        candidate = {k: set() for k in ('assemblies', 'types', 'members', 'pinvoke')}
        candidate['assemblies'].add(self.assembly)
        review = {'assemblies': [self.assembly], 'types': [], 'members': []}
        self.assertEqual(self.check(candidate, review)['status'], 'EXTERNAL_IMPORT_REVIEWED_MATCH')
        candidate['members'].add('unreviewed')
        with self.assertRaisesRegex(ValueError, 'Novel emitted'):
            self.check(candidate, review)

    def test_unused_review_rejected(self):
        candidate = {k: set() for k in ('assemblies', 'types', 'members', 'pinvoke')}
        with self.assertRaisesRegex(ValueError, 'Unused reviewed'):
            self.check(candidate, {'assemblies': [self.assembly], 'types': [], 'members': []})

    def test_native_import_cannot_be_reviewed(self):
        candidate = {k: set() for k in ('assemblies', 'types', 'members', 'pinvoke')}
        candidate['pinvoke'].add('native')
        review = {'assemblies': [], 'types': [], 'members': []}
        with self.assertRaisesRegex(ValueError, 'Novel emitted'):
            self.check(candidate, review)
        review['pinvoke'] = ['native']
        with self.assertRaisesRegex(ValueError, 'P/Invoke'):
            self.check(candidate, review)

    def test_malformed_or_broad_reviews_rejected(self):
        candidate = {k: set() for k in ('assemblies', 'types', 'members', 'pinvoke')}
        for entries in ('System.*', ['System.*'], ['[]'], [self.assembly, self.assembly], ['[ "assembly" ]']):
            with self.subTest(entries=entries), self.assertRaises(ValueError):
                self.check(candidate, {'assemblies': entries, 'types': [], 'members': []})


if __name__ == '__main__':
    unittest.main()
