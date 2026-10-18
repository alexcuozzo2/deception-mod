"""Validate the metadata guard, not compilation of the new C# shim.

The positive fixture is the supplied, previously compiled Interop Runtime DLL.
Negative cases alter its in-memory metadata rows only; no DLL is rewritten.
"""
import hashlib, os, sys, unittest
from pathlib import Path
from unittest import mock

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import audit_compiler_metadata as audit
from cli_metadata import CLI


class CompilerMetadataGuardChecks(unittest.TestCase):
    def setUp(self):
        supplied=os.environ.get('DECEPTION_API_REFERENCE_DIR')
        if not supplied:self.skipTest('Supplied compiled metadata fixture is external to the source candidate.')
        self.path=Path(supplied)/'Il2CppInterop.Runtime.dll'
        if not self.path.is_file():self.skipTest('Supplied compiled Interop Runtime fixture is unavailable.')
        self.cli=CLI(self.path)

    def type_id(self,name):
        return next(i for i,n in self.cli.names.items() if n==name)

    def constructor(self,name,args):
        return next(m for m in self.cli.methods(name) if m['name']=='.ctor' and m['args']==args)

    def field(self,name):
        rid=self.type_id(name);field_name=audit.EXPECTED[name][1]
        return next(row for i,row in enumerate(self.cli.rows[4],1)
                    if self.cli.fieldowners[i]==rid and self.cli.s(row[1])==field_name)

    def reject(self,pattern):
        with mock.patch.object(audit,'CLI',return_value=self.cli):
            with self.assertRaisesRegex(ValueError,pattern):audit.check(self.path)

    def test_existing_managed_fixture_has_local_nullable_annotations(self):
        result=audit.check(self.path)
        self.assertEqual(result['status'],'MANAGED_NULLABLE_METADATA_MATCH')
        self.assertEqual(result['dll_sha256'],hashlib.sha256(self.path.read_bytes()).hexdigest())
        self.assertEqual(set(result['local_attribute_applications']),set(audit.EXPECTED))
        self.assertTrue(all(n>0 for n in result['local_attribute_applications'].values()))
        self.assertEqual(result['native_game_execution'],'NOT RUN')

    def test_missing_local_attribute_is_rejected(self):
        name=audit.PREFIX+'NullableAttribute'
        self.cli.names[self.type_id(name)]=name+'Missing'
        self.reject('Missing unique local compiler attribute')

    def test_missing_array_constructor_is_rejected(self):
        name=audit.PREFIX+'NullableAttribute'
        method=self.constructor(name,['System.Byte[]'])
        self.cli.rows[6][method['rid']-1][3]=self.field(name)[1]
        self.reject('Wrong compiler attribute constructors')

    def test_private_constructor_is_rejected(self):
        method=self.constructor(audit.PREFIX+'NullableContextAttribute',['System.Byte'])
        row=self.cli.rows[6][method['rid']-1]
        row[2]=(row[2]&~7)|1
        self.reject('constructor must be public instance')

    def test_static_constructor_is_rejected(self):
        method=self.constructor(audit.PREFIX+'NullableContextAttribute',['System.Byte'])
        self.cli.rows[6][method['rid']-1][2]|=16
        self.reject('constructor must be public instance')

    def test_noncore_attribute_base_is_rejected(self):
        rid=self.type_id(audit.PREFIX+'NullableAttribute')
        base=self.cli.row(1,self.cli.row(2,rid)[3]>>2)
        noncore=next(i for i,row in enumerate(self.cli.rows[35],1)
                    if self.cli.s(row[6]) not in ('System.Private.CoreLib','System.Runtime','mscorlib','netstandard'))
        base[0]=(noncore<<2)|2
        self.reject('base is not a managed core library reference')

    def test_mutable_flag_field_is_rejected(self):
        self.field(audit.PREFIX+'NullableAttribute')[0]&=~0x20
        self.reject('Wrong compiler attribute flag field')

    def test_wrong_flag_type_is_rejected(self):
        self.field(audit.PREFIX+'NullableAttribute')[2]=self.field(audit.PREFIX+'NullableContextAttribute')[2]
        self.reject('Wrong compiler attribute flag field')

    def test_local_definitions_without_annotation_uses_are_rejected(self):
        for name in audit.EXPECTED:
            with self.subTest(attribute=name):
                self.cli=CLI(self.path)
                constructors={m['rid'] for m in self.cli.methods(name) if m['name']=='.ctor'}
                self.cli.rows[12]=[row for row in self.cli.rows[12]
                                   if not(row[1]&7==2 and row[1]>>3 in constructors)]
                self.reject('present but not used')

    def test_external_nullable_constructor_reference_is_rejected(self):
        name=audit.PREFIX+'NullableAttribute'
        type_row=self.cli.row(2,self.type_id(name))
        ctor=self.constructor(name,['System.Byte'])
        ctor_row=self.cli.row(6,ctor['rid'])
        # Add a controlled external TypeRef/MemberRef and use it for one existing
        # nullable annotation. The remaining valid local uses must not hide it.
        self.cli.rows[1].append([(1<<2)|2,type_row[1],type_row[2]])
        external_type=len(self.cli.rows[1])
        self.cli.rows[10].append([(external_type<<3)|1,ctor_row[3],ctor_row[4]])
        external_ctor=len(self.cli.rows[10])
        annotation=next(row for row in self.cli.rows[12] if row[1]==(ctor['rid']<<3)|2)
        annotation[1]=(external_ctor<<3)|3
        self.reject('refers to an external constructor')


if __name__=='__main__':unittest.main(verbosity=2)
