#!/usr/bin/env python3
"""Read-only guard for the emitted adapter's managed nullable metadata."""
import argparse, hashlib, json
from pathlib import Path
from cli_metadata import CLI

PREFIX='System.Runtime.CompilerServices.'
EXPECTED={
    PREFIX+'NullableAttribute':([['System.Byte'],['System.Byte[]']],'NullableFlags','System.Byte[]'),
    PREFIX+'NullableContextAttribute':([['System.Byte']],'Flag','System.Byte'),
}

def check(path):
    cli=CLI(path)
    local_constructors={}
    uses={name:0 for name in EXPECTED}
    for name,(arguments,field_name,field_type) in EXPECTED.items():
        definitions=[i for i,n in cli.names.items() if n==name]
        if len(definitions)!=1:raise ValueError('Missing unique local compiler attribute: '+name)
        rid=definitions[0];row=cli.row(2,rid)
        if row[0]&7 or not row[0]&0x100 or cli.typeref(row[3])!='System.Attribute':
            raise ValueError('Compiler attribute must be internal sealed System.Attribute: '+name)
        if row[3]&3!=1:raise ValueError('Compiler attribute base must resolve to managed core library: '+name)
        base=cli.row(1,row[3]>>2);scope=base[0]
        if scope&3!=2 or cli.s(cli.row(35,scope>>2)[6]) not in ('System.Private.CoreLib','System.Runtime','mscorlib','netstandard'):
            raise ValueError('Compiler attribute base is not a managed core library reference: '+name)
        constructors=[m for m in cli.methods(name) if m['name']=='.ctor']
        if sorted(m['args'] for m in constructors)!=sorted(arguments):
            raise ValueError('Wrong compiler attribute constructors: '+name)
        for method in constructors:
            if method['flags']&7!=6 or method['flags']&16 or method['ret']!='System.Void':
                raise ValueError('Compiler attribute constructor must be public instance: '+name)
            local_constructors[method['rid']]=name
        fields=[r for i,r in enumerate(cli.rows.get(4,[]),1)
                if cli.fieldowners.get(i)==rid and cli.s(r[1])==field_name]
        if (len(fields)!=1 or cli.type(cli.blob(fields[0][2]),1)[0]!=field_type
                or not fields[0][0]&0x20 or fields[0][0]&7!=6 or fields[0][0]&16):
            raise ValueError('Wrong compiler attribute flag field: '+name)

    for _,constructor,_ in cli.rows.get(12,[]):
        tag=constructor&7;rid=constructor>>3
        if tag==2 and rid in local_constructors:
            uses[local_constructors[rid]]+=1
        elif tag==3:
            target=cli.token((10<<24)|rid)
            if any(target==name+'::.ctor' for name in EXPECTED):
                raise ValueError('Nullable annotation refers to an external constructor: '+target)
    if any(n==0 for n in uses.values()):
        raise ValueError('Local compiler metadata is present but not used for nullable annotations.')
    return {'status':'MANAGED_NULLABLE_METADATA_MATCH','dll_sha256':hashlib.sha256(Path(path).read_bytes()).hexdigest(),
            'local_attribute_applications':uses,'native_game_execution':'NOT RUN'}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('assembly',type=Path);parser.add_argument('output',type=Path)
    args=parser.parse_args();result=check(args.assembly)
    args.output.write_text(json.dumps(result,indent=2)+'\n')
    print('COMPILER METADATA CHECK PASSED: emitted nullable annotations use local managed attributes; no native code executed.')

if __name__=='__main__':main()
