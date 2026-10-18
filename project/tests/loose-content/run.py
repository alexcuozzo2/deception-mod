#!/usr/bin/env python3
"""Focused registry fixture; uses installed SDK/runtime, no restore or game."""
from pathlib import Path
import json
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root / 'tools'))
import manage

dotnet, compiler = manage.sdk()
runtime = Path('/usr/local/share/dotnet/shared/Microsoft.NETCore.App/6.0.36')
work = root / 'build' / 'loose-content-fixture'
work.mkdir(parents=True, exist_ok=True)
runner = work / 'Runner.cs'
runner.write_text('''using System; using System.Reflection;
namespace HarmonyLib {
public sealed class HarmonyMethod { public HarmonyMethod(MethodInfo m){} }
public sealed class Harmony { public void Patch(MethodInfo m,HarmonyMethod? a,HarmonyMethod? b,object? c,HarmonyMethod? d,object? e){} }
}
public static class Runner {
public static void Main(){ int count=0; Deception.Tests.LooseContentTests.Run((name,work)=>{work();count++;Console.WriteLine("PASS "+name);});Console.WriteLine("Passed "+count+" focused registry case."); }
}
''')
sources = [root/'src/PolyMod/NativeApi.cs', root/'src/PolyMod/ModContentAssets.cs',
           Path(__file__).with_name('RegistryFixture.cs'), Path(__file__).with_name('LooseContentTests.cs'), runner]
out = work / 'LooseContentTests.dll'
rsp = work / 'compile.rsp'
lines = ['-nologo','-nostdlib+','-target:exe','-langversion:10','-nullable:enable',f'-out:"{out}"']
lines += [f'-r:"{p}"' for p in sorted(runtime.glob('*.dll'))]
lines += [f'"{p}"' for p in sources]
rsp.write_text('\n'.join(lines)+'\n')
subprocess.run([str(dotnet),'exec',str(compiler),'-noconfig','@'+str(rsp)],check=True)
out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm':'net6.0','framework':{'name':'Microsoft.NETCore.App','version':'6.0.36'}}}))
subprocess.run([str(dotnet),str(out)],check=True)
