"""Fail-closed, dependency-free emitted external import subset audit."""
import hashlib
import json
from pathlib import Path
from cli_metadata import CLI


class Imports:
    def __init__(self, path):
        self.cli = CLI(path)

    def assembly(self, rid):
        c = self.cli
        r = c.row(35, rid)
        return ('assembly', c.s(r[6]), tuple(r[:4]), r[4], c.blob(r[5]).hex(), c.s(r[7]))

    def scope(self, coded, active=()):
        c = self.cli
        tag, rid = coded & 3, coded >> 2
        if tag == 2:
            return self.assembly(rid)
        if tag == 3:
            return self.typeref(rid, active)
        if tag == 0:
            return ('local-module',)
        if tag == 1:
            return ('module', c.s(c.row(26, rid)[0]))
        raise ValueError('Unsupported resolution scope')

    def typeref(self, rid, active=()):
        if rid in active:
            raise ValueError('Cyclic TypeRef')
        c = self.cli
        r = c.row(1, rid)
        return ('type', self.scope(r[0], active + (rid,)), c.s(r[2]), c.s(r[1]))

    def token_type(self, coded):
        tag, rid = coded & 3, coded >> 2
        if not rid:
            raise ValueError('Null signature type')
        if tag == 0:
            return ('local', self.cli.names[rid])
        if tag == 1:
            return self.typeref(rid)
        if tag == 2:
            return self.signature(self.cli.blob(self.cli.row(27, rid)[0]), type_only=True)
        raise ValueError('Invalid TypeDefOrRef')

    def signature(self, blob, type_only=False):
        p = 0
        def byte():
            nonlocal p
            if p >= len(blob):
                raise ValueError('Truncated signature')
            value = blob[p]; p += 1
            return value
        def number():
            first = byte()
            if first < 0x80:
                return first
            if first < 0xc0:
                return ((first & 0x3f) << 8) | byte()
            if first < 0xe0:
                return ((first & 0x1f) << 24) | (byte() << 16) | (byte() << 8) | byte()
            raise ValueError('Invalid compressed integer')
        def typ():
            el = byte()
            if el in set(range(1, 15)) | {0x16, 0x18, 0x19, 0x1c}:
                return ('primitive', el)
            if el in (0x11, 0x12):
                return ('named', el, self.token_type(number()))
            if el in (0x0f, 0x10, 0x1d, 0x45):
                return ('wrapper', el, typ())
            if el in (0x13, 0x1e):
                return ('generic-parameter', el, number())
            if el in (0x1f, 0x20):
                modifier = self.token_type(number())
                return ('modifier', el, modifier, typ())
            if el == 0x15:
                base = typ()
                return ('generic-instance', base, tuple(typ() for _ in range(number())))
            if el == 0x14:
                base = typ(); rank = number()
                sizes = tuple(number() for _ in range(number()))
                # Preserve encoded signed lower bounds exactly; metadata's encoding is canonical.
                lower = tuple(number() for _ in range(number()))
                return ('array', base, rank, sizes, lower)
            if el == 0x1b:
                return ('function-pointer', method())
            raise ValueError('Unsupported signature element: ' + hex(el))
        def method():
            flags = byte()
            if flags & 0x80 or flags & 0x0f not in (0, 1, 2, 3, 4, 5, 9, 11):
                raise ValueError('Unsupported method convention')
            arity = number() if flags & 0x10 else 0
            count = number(); result = typ(); args = []
            for _ in range(count):
                if p < len(blob) and blob[p] == 0x41:
                    byte(); args.append(('sentinel',))
                args.append(typ())
            return ('method', flags, arity, result, tuple(args))
        if type_only:
            result = typ()
        elif blob and blob[0] == 6:
            byte(); result = ('field', typ())
        else:
            result = method()
        if p != len(blob):
            raise ValueError('Unconsumed signature bytes')
        return result

    def parent(self, coded):
        tag, rid = coded & 7, coded >> 3
        if tag == 0:
            return ('local', self.cli.names[rid])
        if tag == 1:
            return self.typeref(rid)
        if tag == 2:
            return ('module', self.cli.s(self.cli.row(26, rid)[0]))
        if tag == 3:
            return ('local-method', self.cli.token((6 << 24) | rid))
        if tag == 4:
            return self.signature(self.cli.blob(self.cli.row(27, rid)[0]), type_only=True)
        raise ValueError('Unsupported MemberRef parent')

    @staticmethod
    def external(value):
        if not isinstance(value, tuple):
            return False
        if value and value[0] in ('assembly', 'module'):
            return True
        return any(Imports.external(item) for item in value)

    def pinvoke(self, row):
        flags, forwarded, name, module = row
        c = self.cli
        table = 6 if forwarded & 1 else 4
        rid = forwarded >> 1
        declaration = c.row(table, rid)
        signature = self.signature(c.blob(declaration[4 if table == 6 else 2]))
        return (flags, c.token((table << 24) | rid), signature,
                c.s(name), c.s(c.row(26, module)[0]))

    def collect(self):
        c = self.cli
        encode = lambda value: json.dumps(value, separators=(',', ':'))
        types = [self.typeref(i) for i in range(1, len(c.rows.get(1, [])) + 1)]
        members = []
        for parent, name, signature in c.rows.get(10, []):
            owner = self.parent(parent)
            sig = self.signature(c.blob(signature))
            if self.external(owner):
                members.append((owner, c.s(name), sig))
        return {
            'assemblies': {encode(self.assembly(i)) for i in range(1, len(c.rows.get(35, [])) + 1)},
            'types': {encode(t) for t in types if self.external(t)},
            'members': {encode(m) for m in members},
            'pinvoke': {encode(self.pinvoke(row)) for row in c.rows.get(28, [])},
        }


def audit(candidate_path, donor_path):
    try:
        candidate = Imports(candidate_path).collect()
        donor = Imports(donor_path).collect()
        novel = {kind: sorted(candidate[kind] - donor[kind]) for kind in candidate}
        if any(novel.values()):
            raise ValueError('Novel emitted external imports: ' + json.dumps({k: v for k, v in novel.items() if v}))
        return {'status': 'EXTERNAL_IMPORT_SUBSET_MATCH',
                'candidate_sha256': hashlib.sha256(Path(candidate_path).read_bytes()).hexdigest(),
                'donor_sha256': hashlib.sha256(Path(donor_path).read_bytes()).hexdigest(),
                'counts': {kind: {'candidate': len(candidate[kind]), 'donor': len(donor[kind])} for kind in candidate},
                'native_execution': 'NOT RUN'}
    except (IndexError, KeyError, TypeError, RecursionError) as error:
        raise ValueError('Unsupported or malformed emitted metadata') from error
