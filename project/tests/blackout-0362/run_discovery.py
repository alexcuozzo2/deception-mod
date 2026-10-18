#!/usr/bin/env python3
"""Offline actual-source Blackout discovery fixture; no game, install, network or saves."""
import hashlib,json,sys,tempfile
from pathlib import Path
R=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(R/'tools'));import manage as M
root=R.parent/'run-output/blackout-discovery-0397/fixtures';root.mkdir(parents=True,exist_ok=True)
work=Path(tempfile.mkdtemp(prefix='run-',dir=root))
names=['NativeApi.cs','NativeDisguiseConcealmentEffects.cs','NativeFoggerActions.cs','NativeSpyNetwork.cs','NativeBlackout.cs','NativeBlackoutDiscovery.cs','NativeBlackoutLighthouse.cs','NativeBlackoutAccess.cs','NativeSubvert.cs','NativeBlackoutUi.cs','NativeExchangePlan.cs','NativeRogueSight.cs']
runtime=M.GAME/'dotnet';refs=sorted(runtime.glob('*.dll'));dotnet,compiler=M.sdk();host=R/'build/m0-xhkv17do/model-test-host'
source=R/'src/PolyMod/NativeBlackoutDiscovery.cs';original=source.read_text();controls={'positive':original}
if '--controls' in sys.argv:
 controls.update({
  'missing-border-direct-child-bridge':original.replace('if(__state.Border)CollectBorderChildren(__state);',''),
  'explorer-requires-real-body':original.replace('allowed=CurrentPlayer(__0,actor)&&Tile(__0,Native.Get(__instance,"From")!) is not null;','allowed=CurrentPlayer(__0,actor)&&NativeSpyNetwork.Unit(__0,Convert.ToUInt32(Native.Get(__instance,"UnitId"))) is not null;'),
  'explorer-occupant-controls-sight':original.replace('else if(unit is not null)','if(unit is not null)'),
  'border-permits-old-queue-entries':original.replace('now.Skip(source.Queue.Length).Any(a=>!old.Contains(Native.Pointer(a))&&IsExplore(a,source.Actor,at))','now.Any(a=>IsExplore(a,source.Actor,at))'),
 })
results=[]
with (work/'compile-run.log').open('w') as log:
 for key,text in controls.items():
  if key!='positive' and text==original:raise RuntimeError('Control did not mutate source: '+key)
  dest=work/key;dest.mkdir();body=dest/'NativeBlackoutDiscovery.cs';body.write_text(text)
  sources=sorted((R/'src/Core').glob('*.cs'))+[R/'src/PolyMod'/n for n in names if n!='NativeBlackoutDiscovery.cs']+[body]+sorted((R/'tests/blackout-0362').glob('*.cs'))
  assembly='Deception.BlackoutUiLifecycleTests';M.csc(dotnet,compiler,sources,dest/(assembly+'.dll'),refs,log)
  try:
   output=M.call(['/usr/bin/arch','-x86_64',host,runtime,dest,assembly,'Deception.Tests.BlackoutTests','Run'],log=log,env={**M.clean_env(),'DECEPTION_CAPTURED_TREE_FIXTURE':str(R/'tests/native-tree.captured.json')})
   result=json.loads(next(x for x in output.splitlines() if x.startswith('{')));(dest/'results.json').write_text(json.dumps(result,indent=2)+'\n')
   failed=False;print(key,result['cases'],result['assertions'],flush=True)
  except M.Stop:
   failed=True;print(key,'rejected',flush=True)
   if key=='positive':print((work/'compile-run.log').read_text()[-12000:]);raise
  if failed!=(key!='positive'):raise RuntimeError('Control outcome incorrect: '+key)
  results.append({'name':key,'expectedFailure':key!='positive','observedFailure':failed})
 if '--retained' in sys.argv:
  for folder,entry,method in [('blackout-connections-0393','Deception.Tests.BlackoutTests','RunConnections'),('fogger-actions-0390','Deception.Tests.FoggerActionTests','Run'),('blackout-lighthouse-0373','Deception.Tests.LighthouseTests','Run')]:
   dest=work/folder;dest.mkdir();assembly='Deception.BlackoutRetainedTests'
   sources=sorted((R/'src/Core').glob('*.cs'))+[R/'src/PolyMod'/n for n in names]+sorted((R/'tests/blackout-0362').glob('*.cs'))+sorted((R/'tests'/folder).glob('*.cs'))
   M.csc(dotnet,compiler,sources,dest/(assembly+'.dll'),refs,log)
   output=M.call(['/usr/bin/arch','-x86_64',host,runtime,dest,assembly,entry,method],log=log,env={**M.clean_env(),'DECEPTION_CAPTURED_TREE_FIXTURE':str(R/'tests/native-tree.captured.json')})
   result=json.loads(next(x for x in output.splitlines() if x.startswith('{')));M.validate_candidate_0359(result,folder);(dest/'results.json').write_text(json.dumps(result,indent=2)+'\n');print(folder,result['cases'],result['assertions'],flush=True)
(work/'controls.json').write_text(json.dumps({'passed':True,'controls':results,'source_sha256':hashlib.sha256(original.encode()).hexdigest()},indent=2)+'\n')
print(work)
