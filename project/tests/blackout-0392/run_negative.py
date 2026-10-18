#!/usr/bin/env python3
"""Offline disposable negative controls; no game build, install or launch."""
import json,sys,tempfile
from pathlib import Path
R=Path(__file__).resolve().parents[2];sys.path.insert(0,str(R/'tools'));import manage as M
root=R.parent/'run-output/blackout-fixes-0392/fixtures';root.mkdir(parents=True,exist_ok=True)
out=Path(tempfile.mkdtemp(prefix='controls-',dir=root))
runtime=M.GAME/'dotnet';refs=sorted(runtime.glob('*.dll'));dotnet,compiler=M.sdk();host=R/'build/m0-46q609_t/model-test-host'
base=['NativeDisguiseConcealmentEffects.cs','NativeFoggerActions.cs','NativeSpyNetwork.cs','NativeBlackout.cs','NativeBlackoutDiscovery.cs','NativeBlackoutLighthouse.cs','NativeBlackoutAccess.cs','NativeSubvert.cs','NativeBlackoutUi.cs','NativeExchangePlan.cs','NativeRogueSight.cs']
access=(R/'src/PolyMod/NativeBlackoutAccess.cs').read_text();discovery=(R/'src/PolyMod/NativeBlackoutDiscovery.cs').read_text();growth=(R/'src/PolyMod/NativeGrowthSight.cs').read_text()
mutations=[
 ('old-city-denial','NativeBlackoutAccess.cs',access.replace('if(type.Name!="CityRewardCommand"','if(true||type.Name!="CityRewardCommand"'),'blackout'),
 ('enemy-city-permission','NativeBlackoutAccess.cs',access.replace('Native.Int(Native.Get(tile,"owner"))==actor','true'),'blackout'),
 ('missing-upgrade-hook','NativeBlackoutDiscovery.cs',discovery.replace('"TrainAction","UpgradeAction","ConvertAction"','"TrainAction","ConvertAction"'),'blackout'),
 ('growth-counts-denied-children','NativeGrowthSight.cs',growth.replace('if(!explored&&!NativeBlackout.Covered(State,OwnerId,new(targetAt.X,targetAt.Y)))','if(!explored)'),'growth'),
 ('covered-growth-source-refused','NativeGrowthSight.cs',growth.replace('Require(covered||Explored(tile,OwnerId)','Require(Explored(tile,OwnerId)'),'growth')]
results=[]
for name,file,source,kind in mutations:
 original=(R/'src/PolyMod'/file).read_text();assert source!=original,name
 work=out/name;work.mkdir();mutant=work/file;mutant.write_text(source)
 names=base if kind=='blackout' else ['NativeGrowthSight.cs'];folder='blackout-0362' if kind=='blackout' else 'growth-sight-0355';assembly='Deception.BlackoutTests' if kind=='blackout' else 'Deception.GrowthSightTests';entry='Deception.Tests.BlackoutTests' if kind=='blackout' else 'Deception.Tests.SightTests'
 sources=sorted((R/'src/Core').glob('*.cs'))+[R/'src/PolyMod/NativeApi.cs']+[mutant if n==file else R/'src/PolyMod'/n for n in names]+sorted((R/'tests'/folder).glob('*.cs'))
 with (work/'compile-run.log').open('w') as log:
  M.csc(dotnet,compiler,sources,work/(assembly+'.dll'),refs,log)
  try:M.call(['/usr/bin/arch','-x86_64',host,runtime,work,assembly,entry,'Run'],log=log,env=M.clean_env())
  except M.Stop:results.append({'mutation':name,'rejected':True});print(name,'rejected',flush=True)
  else:raise RuntimeError(name+' unexpectedly passed')
(out/'results.json').write_text(json.dumps(results,indent=2)+'\n');print(out)
