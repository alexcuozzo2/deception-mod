#!/usr/bin/env python3
"""Offline managed fixtures only; never builds or installs the mod."""
import json,sys,tempfile
from pathlib import Path
R=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(R/'tools'));import manage as M
root=R.parent/'run-output/blackout-fixes-0392/fixtures';root.mkdir(parents=True,exist_ok=True)
work=Path(tempfile.mkdtemp(prefix='fixture-',dir=root))
base=['NativeDisguiseConcealmentEffects.cs','NativeFoggerActions.cs','NativeSpyNetwork.cs','NativeBlackout.cs','NativeBlackoutDiscovery.cs','NativeBlackoutLighthouse.cs','NativeBlackoutAccess.cs','NativeSubvert.cs','NativeBlackoutUi.cs','NativeExchangePlan.cs','NativeRogueSight.cs']
recipes={
'fogger-actions':('fogger-actions-0390',base,['blackout-0362'],'Deception.FoggerActionTests','Deception.Tests.FoggerActionTests'),
'blackout':('blackout-0362',base,[],'Deception.BlackoutTests','Deception.Tests.BlackoutTests'),
'lighthouse':('blackout-lighthouse-0373',base,['blackout-0362'],'Deception.LighthouseTests','Deception.Tests.LighthouseTests'),
'spy-readiness':('spy-readiness-0367',['NativeFoggerActions.cs','NativeSpyActions.cs','NativeSpyNetwork.cs','NativeSpyReadinessPresentation.cs'],[],'Deception.SpyReadinessTests','Deception.Tests.SpyReadinessTests'),
'growth-sight':('growth-sight-0355',['NativeGrowthSight.cs'],[],'Deception.GrowthSightTests','Deception.Tests.SightTests'),
'ordinary-actions':('ordinary-actions-0377',['RecruitmentHooks.cs','OrdinaryActionEligibility.cs','NativeOrdinaryCommands.cs','NativeFoggerActions.cs','NativeSpyActions.cs','NativeInvestment.cs','NativeRogueRules.cs'],[],'Deception.OrdinaryActionTests','Deception.Tests.OrdinaryActionTests')}
runtime=M.GAME/'dotnet';refs=sorted(runtime.glob('*.dll'));dotnet,compiler=M.sdk();host=R/'build/m0-46q609_t/model-test-host'
with (work/'compile-run.log').open('w') as log:
 for key in sys.argv[1:] or ['blackout','lighthouse','fogger-actions','growth-sight']:
  folder,names,extra,assembly,entry=recipes[key]
  sources=sorted((R/'src/Core').glob('*.cs'))+[R/'src/PolyMod'/n for n in ['NativeApi.cs',*names]]+sorted((R/'tests'/folder).glob('*.cs'))
  for d in extra:sources+=sorted((R/'tests'/d).glob('*.cs'))
  try:
   M.csc(dotnet,compiler,sources,work/(assembly+'.dll'),refs,log)
   output=M.call(['/usr/bin/arch','-x86_64',host,runtime,work,assembly,entry,'Run'],log=log,env={**M.clean_env(),'DECEPTION_CAPTURED_TREE_FIXTURE':str(R/'tests/native-tree.captured.json')})
   result=json.loads(next(x for x in output.splitlines() if x.startswith('{')))
   (work/(key+'-results.json')).write_text(json.dumps(result,indent=2)+'\n')
   print(key,result['cases'],result['assertions'],flush=True)
  except M.Stop:
   print((work/'compile-run.log').read_text()[-18000:]);raise
print(work)
