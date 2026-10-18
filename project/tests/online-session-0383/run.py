"""Offline production-source lifecycle gate; requires the existing local SDK/host."""
from pathlib import Path
import sys,tempfile,json,os
R=Path(__file__).resolve().parents[2];sys.path.insert(0,str(R/'tools'));import manage as M
D=R.parent/'run-output/online-sync-0383/lifecycle-fixture';D.mkdir(parents=True,exist_ok=True)
W=Path(tempfile.mkdtemp(prefix='fixture-',dir=D));S=W/'source';S.mkdir();inputs={}
dotnet,compiler=M.sdk();runtime=M.GAME/'dotnet';refs=sorted(runtime.glob('*.dll'));host=R/'build/m0-ik6oedr4/model-test-host'
adapters=('NativeApi.cs','NativeSavedMatchSummary.cs','NativeMenuPreviewScope.cs','NativeRegroupSession.cs','NativeRegroupPaidBirth.cs','NativeAquarionUnits.cs','NativeGameStateSnapshot.cs','NativeOnlineSession.cs')
originals=sorted((R/'src/Core').glob('*.cs'))+[R/'src/PolyMod'/n for n in adapters]+sorted((R/'tests/online-session-0383').glob('*.cs'))
local='--local' in sys.argv
if local: originals.append(R/'tests/session-0363/SessionTests.cs')
mutant=os.environ.get('DCP_ONLINE_MUTANT','')
with (W/'run.log').open('w') as log:
 try:
  sources=[]
  for p in originals:
   inputs[str(p)]=M.sha(p);q=S/p.name;code=p.read_text()
   if mutant=='pending-read' and p.name=='NativeRegroupSession.cs':
    old='ReadySnapshot(state,true)?.Read';new='(NativeOnlineSession.Snapshot(state,true)??ReadySnapshot(state))?.Read'
    if code.count(old)!=2: raise RuntimeError('Pending-read mutation source changed')
    code=code.replace(old,new)
   if mutant=='pending-fault' and p.name=='NativeOnlineSession.cs':
    old='Current?.Faulted==true&&Native.Same(ActiveClient(),Current.Client.Target())||\n            Pending?.Faulted==true&&Native.Same(ActiveClient(),Pending.Client.Target())'
    if code.count(old)!=1: raise RuntimeError('Pending-fault mutation source changed')
    code=code.replace(old,'Current?.Faulted==true||Pending?.Faulted==true')
   if mutant=='local-writer' and p.name=='NativeRegroupSession.cs':
    old='prior is null&&NativeOnlineSession.BeforeTransport(state,reading)'
    if code.count(old)!=1: raise RuntimeError('Local-writer mutation source changed')
    code=code.replace(old,'NativeOnlineSession.BeforeTransport(state,reading)')
   q.write_text(code);sources.append(q)
  assembly='Deception.OnlineSessionTests';M.csc(dotnet,compiler,sources,W/(assembly+'.dll'),refs,log)
  entry='Deception.Tests.SessionTests' if local else 'Deception.Tests.OnlineSessionTests'
  out=M.call(['/usr/bin/arch','-x86_64',host,runtime,W,assembly,entry,'Run'],log=log,env=M.clean_env())
  result=json.loads(next(line for line in out.splitlines() if line.startswith('{')));(W/'results.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
 finally:
  log.flush()
  lines=[x for x in (W/'run.log').read_text().splitlines() if x.startswith('{')]
  if lines:(W/'results.json').write_text(json.dumps(json.loads(lines[-1]),indent=2)+'\n')
  current={p:M.sha(Path(p)) for p in inputs};(W/'receipt.json').write_text(json.dumps({'sources_sha256':inputs,'sources_still_match':inputs==current,'local_regression':local,'isolated_mutant':mutant or None},indent=2)+'\n');print(W,flush=True)
