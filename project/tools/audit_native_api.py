#!/usr/bin/env python3
"""Read-only API metadata guard. It never loads a native library or executes a game method."""
import argparse, hashlib, json, re
from pathlib import Path
from cli_metadata import CLI

def enum_values(cli, owner):
    ids=[rid for rid,name in cli.names.items() if name==owner]
    if len(ids)!=1 or cli.typeref(cli.row(2,ids[0])[3]) not in ('System.Enum','Il2CppSystem.Enum'):
        raise ValueError('Missing unique native enum: '+owner)
    values={}
    for kind,parent,blob in cli.rows.get(11,[]):
        if parent&3 or cli.fieldowners.get(parent>>2)!=ids[0]:continue
        field=cli.row(4,parent>>2);kind&=255
        sizes={4:(1,True),5:(1,False),6:(2,True),7:(2,False),8:(4,True),9:(4,False),10:(8,True),11:(8,False)}
        if not field[0]&0x40 or kind not in sizes:raise ValueError('Invalid native enum literal: '+owner)
        data=cli.blob(blob);size,signed=sizes[kind]
        if len(data)!=size:raise ValueError('Invalid native enum constant: '+owner)
        values[cli.s(field[1])]=int.from_bytes(data,'little',signed=signed)
    if not values:raise ValueError('Empty native enum: '+owner)
    return values

def fixture_enum_values(path, owner):
    matches=re.findall(r'\benum\s+'+re.escape(owner)+r'\s*\{([^}]+)\}',path.read_text())
    if len(matches)!=1:raise ValueError('Missing unique fixture enum: '+str(path))
    values={};number=-1
    for member in matches[0].split(','):
        parts=member.strip().split('=')
        if not parts[0]:continue
        if len(parts)==2:number=int(parts[1].strip(),0)
        elif len(parts)==1:number+=1
        else:raise ValueError('Invalid fixture enum: '+str(path))
        values[parts[0].strip()]=number
    return values

def value_layout(cli, owner):
    ids=[rid for rid,name in cli.names.items() if name==owner]
    if len(ids)!=1:raise ValueError('Missing unique native value layout: '+owner)
    rid=ids[0];row=cli.row(2,rid);fields=[]
    for field_id,field_owner in cli.fieldowners.items():
        if field_owner!=rid:continue
        flags,name,signature=cli.row(4,field_id)
        if flags&0x10:continue
        field_type,_=cli.type(cli.blob(signature),1)
        fields.append({'name':cli.s(name),'type':field_type,
                       'offsets':[offset for offset,parent in cli.rows.get(16,[]) if parent==field_id]})
    return {'layout':row[0]&0x18,'base':cli.typeref(row[3]),
            'class_layout':[{'packing':packing,'size':size} for packing,size,parent in cli.rows.get(15,[]) if parent==rid],
            'fields':fields}

def check(reference_dir,contract,require_runtime=False):
    cs={}
    for filename,wanted in contract['reference_sha256'].items():
        p=reference_dir/filename
        # Exported references are flat. On the existing Mac installation the
        # Interop runtime remains in BepInEx/core; never copy or move libraries.
        if not p.exists() and filename in contract.get('installed_reference_locations',{}):
            p=reference_dir/contract['installed_reference_locations'][filename]
        if not p.is_file() or p.is_symlink():raise ValueError('Missing ordinary API reference: '+str(p))
        if hashlib.sha256(p.read_bytes()).hexdigest()!=wanted:raise ValueError('API reference changed: '+filename)
        cs[filename]=CLI(p)
    enums_verified=[];fixtures_verified=[]
    for r in contract.get('enums',[]):
        observed=enum_values(cs[r['file']],r['owner'])
        if observed!=r['values']:raise ValueError('Native enum contract mismatch: '+r['owner'])
        enums_verified.append({'owner':r['owner'],'values':observed})
        for relative in r.get('fixtures',[]):
            if fixture_enum_values(Path(__file__).resolve().parents[1]/relative,r['owner'])!=observed:
                raise ValueError('Fixture enum differs from native metadata: '+relative)
            fixtures_verified.append(relative)
    verified=[]
    for r in contract['methods']:
        methods=cs[r['file']].methods(r['owner'])
        matches=[m for m in methods if m['name']==r['name'] and m['args']==r['args'] and m['ret']==r['returns'] and bool(m['flags']&16)==r['static']]
        if len(matches)!=1:raise ValueError('Native API contract mismatch: '+r['owner']+'::'+r['name'])
        verified.append(r['owner']+'::'+r['name'])
    runtime_verified=[];runtime_missing=[];runtime_hashes={}
    for r in contract.get('runtime_methods',[]):
        p=reference_dir/r['file']
        if not p.is_file() or p.is_symlink():
            if require_runtime:raise ValueError('Required local UI reference missing: '+str(p))
            runtime_missing.append(r['owner']+'::'+r['name']);continue
        if r['file'] not in cs:
            cs[r['file']]=CLI(p);runtime_hashes[r['file']]=hashlib.sha256(p.read_bytes()).hexdigest()
        matches=[m for m in cs[r['file']].methods(r['owner']) if m['name']==r['name'] and m['args']==r['args']
                 and m['ret']==r['returns'] and bool(m['flags']&16)==r['static']]
        if len(matches)!=1:raise ValueError('Local UI API contract mismatch: '+r['owner']+'::'+r['name'])
        runtime_verified.append(r['owner']+'::'+r['name'])
    layouts_verified=[];layouts_missing=[]
    for r in contract.get('runtime_layouts',[]):
        p=reference_dir/r['file']
        if not p.is_file() or p.is_symlink():
            if require_runtime:raise ValueError('Required native layout reference missing: '+str(p))
            layouts_missing.append(r['owner']);continue
        if r['file'] not in cs:
            cs[r['file']]=CLI(p);runtime_hashes[r['file']]=hashlib.sha256(p.read_bytes()).hexdigest()
        observed=value_layout(cs[r['file']],r['owner'])
        if observed!=r['value']:raise ValueError('Native value layout mismatch: '+r['owner'])
        layouts_verified.append({'owner':r['owner'],**observed})
    return {'status':'METADATA_SIGNATURES_MATCH','verified_count':len(verified),'methods':verified,
            'native_enums_verified':enums_verified,'fixture_enums_verified':fixtures_verified,
            'native_layouts_verified':layouts_verified,'native_layouts_not_checked':layouts_missing,
            'runtime_ui_methods_verified':runtime_verified,'runtime_ui_methods_not_checked':runtime_missing,
            'runtime_reference_observed_sha256':runtime_hashes,
            'native_game_execution':'NOT RUN','note':'Metadata validation is not a runtime/UI behavior test. Missing hosted references are listed explicitly; the Mac build requires them.'}

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('references',type=Path);p.add_argument('output',type=Path)
    p.add_argument('--public-polymod',action='store_true')
    p.add_argument('--clean-beta',action='store_true')
    a=p.parse_args();contract=json.loads(Path(__file__).with_name('native-wheel-api.json').read_text())
    if a.public_polymod:
        from public_polymod import SHA256
        contract['reference_sha256']['PolyMod.dll']=SHA256
    if a.clean_beta:
        from clean_beta import SHA256
        contract['reference_sha256']['PolyMod.dll']=SHA256
    result=check(a.references,contract,require_runtime=True);a.output.write_text(json.dumps(result,indent=2)+'\n')
    print('API METADATA CHECK PASSED: %d pinned method/accessor signatures and %d local UI dependency signatures; no native code executed.'%(result['verified_count'],len(result['runtime_ui_methods_verified'])))
if __name__=='__main__':main()
