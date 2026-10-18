#!/usr/bin/env python3
"""Offline exact-input PolyMod preview adapter; workspace artifacts only, never installs."""
from pathlib import Path
import argparse, hashlib, json, re, shutil, subprocess, sys, tempfile

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
WORKSPACE=PROJECT.parent
sys.path.insert(0,str(PROJECT/'tools'))
import manage as M
ACCEPTED='a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016'
CANDIDATE=WORKSPACE/'run-output/full-branch-implementation/candidate-0378/polymod-preview'

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def workspace(p):
    p=p.expanduser().resolve()
    if not p.is_relative_to(WORKSPACE): raise ValueError('All adapter input/output files must be inside the shared workspace.')
    return p

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--input',type=Path,default=CANDIDATE/'preview.dll')
    p.add_argument('--output',type=Path,default=CANDIDATE/'adapted/PolyMod.dll')
    p.add_argument('--receipt',type=Path,default=CANDIDATE/'adapted/receipt.json')
    args=p.parse_args()
    src,out,receipt=map(workspace,(args.input,args.output,args.receipt))
    if not src.is_file() or sha(src)!=ACCEPTED: raise ValueError('Unknown PolyMod input hash; refusing before compilation.')
    if out.exists() or receipt.exists(): raise ValueError('Output/receipt already exists; refusing replacement.')
    dotnet,compiler=M.sdk()
    runtimes=[]
    for line in M.call([dotnet,'--list-runtimes']).splitlines():
        m=re.fullmatch(r'Microsoft.NETCore.App (\d+\.\d+\.\d+) \[(.+)\]',line.strip())
        if m:runtimes.append((tuple(map(int,m.group(1).split('.'))),m.group(1),Path(m.group(2))/m.group(1)))
    if not runtimes:raise ValueError('No existing .NET runtime; no downloads attempted.')
    _,version,runtime=sorted(runtimes)[-1]
    cecil=M.GAME/'BepInEx/core/Mono.Cecil.dll'
    if not cecil.is_file():raise ValueError('Existing local Mono.Cecil.dll unavailable; no downloads attempted.')
    draft=WORKSPACE/'run-output/full-branch-implementation/drafts/polymod-review-0378'
    draft.mkdir(parents=True,exist_ok=True)
    work=Path(tempfile.mkdtemp(prefix='adapter-',dir=draft)); print(work,flush=True)
    tool=work/'Adapt.dll';refs=sorted(runtime.glob('*.dll'))+[cecil]
    def q(path):
        s=str(path)
        if any(c in s for c in '\r\n"'):raise ValueError('Unsupported compiler path characters')
        return '"'+s+'"'
    lines=['-nologo','-nostdlib+','-target:exe','-langversion:10','-nullable:enable','-deterministic+','-optimize+','-warn:4','-out:'+q(tool)]
    lines.extend('-r:'+q(x) for x in refs);lines.append(q(HERE/'Adapt.cs'))
    rsp=tool.with_suffix('.rsp');rsp.write_text('\n'.join(lines)+'\n')
    with (work/'run.log').open('w') as log:
        M.call([dotnet,'exec',compiler,'-noconfig','@'+str(rsp)],log=log)
        shutil.copy2(cecil,work/'Mono.Cecil.dll')
        tool.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net'+'.'.join(version.split('.')[:2]),'framework':{'name':'Microsoft.NETCore.App','version':version}}})+'\n')
        # Exercise the editor's independent input guard, not only the runner's guard.
        bad=work/'unknown.dll';raw=bytearray(src.read_bytes());raw[-1]^=1;bad.write_bytes(raw)
        denied=work/'must-not-exist.dll';denied_receipt=work/'must-not-exist.json'
        result=subprocess.run([str(dotnet),'exec',str(tool),str(bad),str(denied),str(denied_receipt)],capture_output=True,text=True,env=M.clean_env())
        log.write(result.stdout+result.stderr)
        if result.returncode==0 or denied.exists() or denied_receipt.exists() or 'Unknown PolyMod input hash' not in result.stderr:raise ValueError('Unknown-input rejection self-test failed.')
        staging=work/'candidate.dll';staging_receipt=work/'candidate-receipt.json'
        M.call([dotnet,'exec',tool,src,staging,staging_receipt],log=log)
        data=json.loads(staging_receipt.read_text())
        # An adapted or future DLL is not accepted as another editable input.
        second=subprocess.run([str(dotnet),'exec',str(tool),str(staging),str(denied),str(denied_receipt)],capture_output=True,text=True,env=M.clean_env())
        log.write(second.stdout+second.stderr)
        if second.returncode==0 or denied.exists() or denied_receipt.exists() or 'Unknown PolyMod input hash' not in second.stderr:raise ValueError('Already-adapted input rejection self-test failed.')
        if not data['passed'] or sha(src)!=ACCEPTED or sha(staging)!=data['outputSha256']:raise ValueError('Candidate result/input changed.')
        data['toolSources']={str(x.relative_to(WORKSPACE)):sha(x) for x in (HERE/'Adapt.cs',HERE/'adapt.py')}
        data['toolchain']={'compiler':str(compiler),'runtime':str(runtime),'cecil':str(cecil),'cecilSha256':sha(cecil)}
        data['selfTests']={'unknownInputRejected':True,'alreadyAdaptedInputRejected':True,'noRejectedOutput':True}
        data['input']=str(src);data['output']=str(out);data['log']=str(work/'run.log')
        # Independent output inspection uses ECMA metadata, not Cecil's in-memory graph.
        from cli_metadata import CLI
        before=CLI(src);after=CLI(staging)
        def attrs(c):
            rows=[]
            for parent,ctor,blob in c.rows[12]:
                if parent&31!=0:continue
                name=c.token((6 if ctor&7==2 else 10)<<24 | ctor>>3)
                if not name.startswith('HarmonyLib.'):continue
                method=c.token(6<<24 | parent>>5)
                rows.append((method,name,c.blob(blob).hex()))
            return sorted(rows)
        a,b=attrs(before),attrs(after)
        selected='PolyMod.Multiplayer.Client::Deserialize_Postfix'
        removed=[x for x in a if x[0]==selected and x[1] in ('HarmonyLib.HarmonyPatch::.ctor','HarmonyLib.HarmonyPostfix::.ctor')]
        if len(removed)!=2 or b!=[x for x in a if x not in removed]:raise ValueError('Independent attribute inventory differs beyond approved pair.')
        # Unedited Init and all other method IL are already normalized/pinned by C#.
        data['independentInspection']={'harmonyAttributesBefore':len(a),'harmonyAttributesAfter':len(b),'onlyExpectedTwoRemoved':True}
        out.parent.mkdir(parents=True,exist_ok=True);receipt.parent.mkdir(parents=True,exist_ok=True)
        with out.open('xb') as f:f.write(staging.read_bytes())
        with receipt.open('x') as f:json.dump(data,f,indent=2);f.write('\n')
        print(json.dumps(data,indent=2),flush=True)
    return 0

if __name__=='__main__':
    try:sys.exit(main())
    except Exception as e:print(str(e),file=sys.stderr);sys.exit(1)
