#!/usr/bin/env python3
"""Offline actual-source regression fixture; no game or backend is invoked."""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import manage as M


def main():
    output = ROOT.parent / 'run-output/feedback-0392/online-maintenance/fixture'
    output.mkdir(parents=True, exist_ok=True)
    runtime = M.GAME / 'dotnet'
    dotnet, compiler = M.sdk()
    hosts = sorted(ROOT.glob('build/*/model-test-host'), key=lambda p: p.stat().st_mtime)
    if not hosts:
        raise RuntimeError('An existing offline model-test-host is required.')
    source = ROOT / 'src/PolyMod/NativeOnlineMaintenance.cs'
    original = source.read_text()
    controls = {
        'positive': original,
        'ignore-server-rejection': original.replace('response is null||!(bool)Native.Invoke(Success,response)!', 'response is null'),
        'allow-active-game': original.replace('Native.Int(Native.Get(summary,"State"))==4', 'Native.Int(Native.Get(summary,"State"))>=3'),
        'ignore-summary-generation': original.replace('!Native.Same(summary,pending.Summary)||', ''),
    }
    results = []
    for label, content in controls.items():
        target = output / label
        target.mkdir(exist_ok=True)
        candidate = source if label == 'positive' else target / source.name
        if label != 'positive':
            if content == original:
                raise RuntimeError('Control did not alter source: ' + label)
            candidate.write_text(content)
        env = M.clean_env()
        env['DECEPTION_TEST_OUTPUT'] = str(target / 'results.json')
        failed = False
        with (target / 'run.log').open('w') as log:
            M.csc(dotnet, compiler, [candidate, *sorted((ROOT/'tests/online-maintenance').glob('*.cs'))],
                  target/'Deception.OnlineMaintenanceTests.dll', sorted(runtime.glob('*.dll')), log)
            try:
                M.call(['/usr/bin/arch', '-x86_64', hosts[-1], runtime, target,
                        'Deception.OnlineMaintenanceTests', 'Deception.Tests.OnlineMaintenanceTests', 'Run'], log=log, env=env)
            except M.Stop:
                failed = True
        if failed != (label != 'positive'):
            raise RuntimeError('Unexpected fixture/control outcome: ' + label)
        results.append({'name': label, 'expectedFailure': label != 'positive', 'observedFailure': failed})
    result = json.loads((output/'positive/results.json').read_text())
    M.validate_candidate_0359(result, 'online-maintenance')
    (output/'results.json').write_text(json.dumps(result, indent=2)+'\n')
    (output/'controls.json').write_text(json.dumps({'passed': True, 'controls': results}, indent=2)+'\n')
    print(json.dumps({'passed': True, 'cases': result['cases'], 'assertions': result['assertions'], 'controls': results}))


if __name__ == '__main__':
    main()
