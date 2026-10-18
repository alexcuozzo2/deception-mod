#!/usr/bin/env python3
"""Offline actual-source fixture and deliberate unsafe controls; no game/build/install."""
import json,sys,tempfile,hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(R/'tools'));import manage as M
root=R.parent/'run-output/blackout-connections-0393/fixtures';root.mkdir(parents=True,exist_ok=True)
work=Path(tempfile.mkdtemp(prefix='run-',dir=root))
names=['NativeApi.cs','NativeDisguiseConcealmentEffects.cs','NativeFoggerActions.cs','NativeSpyNetwork.cs','NativeBlackout.cs','NativeBlackoutDiscovery.cs','NativeBlackoutLighthouse.cs','NativeBlackoutAccess.cs','NativeSubvert.cs','NativeBlackoutUi.cs','NativeExchangePlan.cs','NativeRogueSight.cs']
runtime=M.GAME/'dotnet';refs=sorted(runtime.glob('*.dll'));dotnet,compiler=M.sdk();host=R/'build/m0-46q609_t/model-test-host'
source=R/'src/PolyMod/NativeBlackout.cs';original=source.read_text();controls={'positive':original}
if '--controls' in sys.argv:
 controls.update({'old-city-denial':original.replace('if(OwnedCityRoute(state,__instance,__0)){__result=true;return;}',''),
  'owner-check-removed':original.replace('Convert.ToByte(Native.Get(tile,"owner"))==player&&',''),
  'missing-unwind':original.replace('if(ReferenceEquals(Routing,__state))Routing=__state.Generation==RouteGeneration?__state.Previous:null;',''),
  'unscoped-city-discovery':original.replace('if(OwnedCityRoute(state,__instance,__0))','if(Convert.ToByte(Native.Get(__instance,"owner"))==__0)')})
results=[]
with (work/'compile-run.log').open('w') as log:
 for key,text in controls.items():
  dest=work/key;dest.mkdir();body=dest/'NativeBlackout.cs';body.write_text(text)
  sources=sorted((R/'src/Core').glob('*.cs'))+[R/'src/PolyMod'/n for n in names if n!='NativeBlackout.cs']+[body]+sorted((R/'tests/blackout-0362').glob('*.cs'))+sorted((R/'tests/blackout-connections-0393').glob('*.cs'))
  assembly='Deception.BlackoutConnectionTests';M.csc(dotnet,compiler,sources,dest/(assembly+'.dll'),refs,log)
  try:
   output=M.call(['/usr/bin/arch','-x86_64',host,runtime,dest,assembly,'Deception.Tests.BlackoutTests','RunConnections'],log=log,env={**M.clean_env(),'DECEPTION_CAPTURED_TREE_FIXTURE':str(R/'tests/native-tree.captured.json')})
   result=json.loads(next(x for x in output.splitlines() if x.startswith('{')));(dest/'results.json').write_text(json.dumps(result,indent=2)+'\n')
   failed=False;print(key,result['cases'],result['assertions'],flush=True)
  except M.Stop:
   failed=True;print(key,'rejected',flush=True)
   if key=='positive':print((work/'compile-run.log').read_text()[-12000:]);raise
  if failed!=(key!='positive'):raise RuntimeError('Control outcome incorrect: '+key)
  results.append({'name':key,'expectedFailure':key!='positive','observedFailure':failed})
(work/'controls.json').write_text(json.dumps({'passed':True,'controls':results,'source_sha256':hashlib.sha256(original.encode()).hexdigest()},indent=2)+'\n')
print(work)
