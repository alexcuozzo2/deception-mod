"""Focused offline Core protocol + actual planner harness; no game/backend."""
from pathlib import Path
import json
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import manage as M

OUT = ROOT.parent / 'run-output/online-sync-0383/protocol-audit'
OUT.mkdir(parents=True, exist_ok=True)
work = Path(tempfile.mkdtemp(prefix='fixture-', dir=OUT))
print(work, flush=True)
sources = sorted((ROOT / 'src/Core').glob('*.cs')) + [ROOT / 'src/PolyMod' / name for name in (
    'NativeExchangePlan.cs', 'NativeExchangePlanning.cs', 'NativeLedgerState.cs',
    'NativeDisguiseConcealmentEffects.cs', 'NativeAquarionUnits.cs', 'CompilerMetadata.cs')]
sources += [ROOT / 'tests/regroup-planning' / name for name in ('PlanningFixturePorts.cs', 'NewCommandPorts.cs')]
sources += sorted((ROOT / 'tests/online-sync').glob('*.cs'))
host = ROOT / 'build/m0-ik6oedr4/model-test-host'
runtime = M.GAME / 'dotnet'
inputs = {str(p): M.sha(p) for p in sources + [host]}
with (work / 'run.log').open('w') as log:
    dotnet, compiler = M.sdk()
    M.csc(dotnet, compiler, sources, work / 'Deception.OnlineProtocolTests.dll', sorted(runtime.glob('*.dll')), log)
    result = subprocess.run(['/usr/bin/arch', '-x86_64', str(host), str(runtime), str(work),
        'Deception.OnlineProtocolTests', 'Deception.Tests.OnlineProtocolTests', 'Run'],
        env=M.clean_env(), stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    log.write(result.stdout)
    parsed = json.loads(next(line for line in result.stdout.splitlines() if line.startswith('{')))
    (work / 'results.json').write_text(json.dumps(parsed, indent=2) + '\n')
    print(json.dumps({k: parsed[k] for k in ('status', 'cases', 'assertions')}), flush=True)
    if result.returncode:
        print(result.stdout, flush=True)
(work / 'receipt.json').write_text(json.dumps({'inputs': inputs, 'inputs_unchanged': all(M.sha(Path(p)) == h for p, h in inputs.items()),
    'result': parsed, 'nativeGame': 'NOT RUN', 'backend': 'NOT RUN', 'nativeProjection': 'synthetic scalar publication of actual production planner output'}, indent=2) + '\n')
sys.exit(result.returncode)
