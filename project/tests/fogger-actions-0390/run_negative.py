#!/usr/bin/env python3
"""Compile disposable mutations outside source; each must fail a real fixture."""
import json,sys,tempfile
from pathlib import Path
R=Path(__file__).resolve().parents[2];sys.path.insert(0,str(R/'tools'));import manage as M
out=Path(tempfile.mkdtemp(prefix='controls-',dir=R.parent/'run-output/fogger-0390/mechanics'))
runtime=M.GAME/'dotnet';refs=sorted(runtime.glob('*.dll'));dotnet,compiler=M.sdk();host=R/'build/m0-46q609_t/model-test-host'
base=['NativeDisguiseConcealmentEffects.cs','NativeFoggerActions.cs','NativeSpyNetwork.cs','NativeBlackout.cs','NativeBlackoutDiscovery.cs','NativeBlackoutLighthouse.cs','NativeBlackoutAccess.cs','NativeSubvert.cs','NativeBlackoutUi.cs','NativeExchangePlan.cs','NativeRogueSight.cs']
original=(R/'src/PolyMod/NativeFoggerActions.cs').read_text()
mutations={
 'no-second-shot':original.replace('if(first)Native.Set(unit,"attacked",false);','if(false)Native.Set(unit,"attacked",false);'),
 'unlimited-refund':original.replace('if(first)Native.Set(unit,"attacked",false);','Native.Set(unit,"attacked",false);'),
 'Spy-Blackout-leak':original.replace('FoggerCatalog.IsFoggerId(Native.EnumName(Native.Get(unit,"type")!))','(FoggerCatalog.IsFoggerId(Native.EnumName(Native.Get(unit,"type")!))||SpyCatalog.IsSpyId(Native.EnumName(Native.Get(unit,"type")!)))'),
}
results=[]
for name,source in mutations.items():
 work=out/name;work.mkdir();mutant=work/'NativeFoggerActions.cs';mutant.write_text(source)
 sources=sorted((R/'src/Core').glob('*.cs'))+[R/'src/PolyMod/NativeApi.cs']+[mutant if n=='NativeFoggerActions.cs' else R/'src/PolyMod'/n for n in base]+sorted((R/'tests/blackout-0362').glob('*.cs'))+sorted((R/'tests/fogger-actions-0390').glob('*.cs'))
 if name=='Spy-Blackout-leak':
  n=work/'NativeSpyNetwork.cs';n.write_text((R/'src/PolyMod/NativeSpyNetwork.cs').read_text().replace('fogger?FoggerCatalog.IsFoggerId(id):SpyCatalog.IsSpyId(id)','fogger?(FoggerCatalog.IsFoggerId(id)||SpyCatalog.IsSpyId(id)):SpyCatalog.IsSpyId(id)'));sources=[n if p.name=='NativeSpyNetwork.cs' else p for p in sources]
 with (work/'compile-run.log').open('w') as log:
  M.csc(dotnet,compiler,sources,work/'Deception.FoggerActionTests.dll',refs,log)
  try:M.call(['/usr/bin/arch','-x86_64',host,runtime,work,'Deception.FoggerActionTests','Deception.Tests.FoggerActionTests','Run'],log=log,env=M.clean_env())
  except M.Stop:results.append({'mutation':name,'rejected':True});print(name,'rejected',flush=True)
  else:raise RuntimeError(name+' unexpectedly passed')
(out/'results.json').write_text(json.dumps(results,indent=2)+'\n');print(out)
