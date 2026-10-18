#!/usr/bin/env python3
"""Offline actual-source fixture and regression controls; never accesses games or the network."""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import manage as M


def main():
    output = ROOT.parent / 'run-output/resign-crash-0396/online-resign'
    output.mkdir(parents=True, exist_ok=True)
    runtime = M.GAME / 'dotnet'
    refs = sorted(runtime.glob('*.dll'))
    dotnet, compiler = M.sdk()
    source = ROOT / 'src/PolyMod/NativeOnlineResign.cs'
    text = source.read_text()
    controls = {
        'positive': text,
        'native-task-fault-regression': text.replace(
            'Native.Invoke(SetResult,completion.Source,response);',
            'if(error is not null)Native.Invoke(completion.Source.GetType().GetMethod("SetException")!,completion.Source,Activator.CreateInstance(Native.T("Il2CppSystem.Exception"),error));else Native.Invoke(SetResult,completion.Source,response);'),
        'no-detached-authority': text.replace('using(NativeOnlineSession.BeginDetached(request.Epoch,shadow,"preview-resign"))', 'using(new System.IO.MemoryStream())'),
        'no-main-dispatch': text.replace('NativeOnlineDispatch.Invoke(Leaves[name],args)', 'Native.Invoke(Leaves[name],null,args)'),
        'bad-tail-admitted': text.replace('Require((bool)Native.Invoke(DecodeCommand,null,follow)!&&follow[1] is not null&&(int)follow[2]! ==123,"Invalid resign follow-up command.");', 'Native.Invoke(DecodeCommand,null,follow);'),
    }
    results = []
    for label, content in controls.items():
        target = output / label
        target.mkdir(exist_ok=True)
        candidate = source if label == 'positive' else target / 'NativeOnlineResign.cs'
        if label != 'positive':
            if content == text:
                raise RuntimeError('Control did not alter source: ' + label)
            candidate.write_text(content)
        env = M.clean_env()
        env['DECEPTION_TEST_OUTPUT'] = str(target / 'results.json')
        sources = [candidate, ROOT/'src/PolyMod/NativeOnlineDispatch.cs', *sorted((ROOT/'tests/online-resign').glob('*.cs'))]
        failed = False
        with (target/'run.log').open('w') as log:
            M.csc(dotnet, compiler, sources, target/'Deception.OnlineResignTests.dll', refs, log)
            try:
                M.call(['/usr/bin/arch', '-x86_64', ROOT/'build/m0-winoufzf/model-test-host', runtime, target, 'Deception.OnlineResignTests', 'Deception.Tests.OnlineResignTests', 'Run'], log=log, env=env)
            except M.Stop:
                failed = True
        if failed != (label != 'positive'):
            raise RuntimeError('Unexpected fixture/control outcome: ' + label)
        results.append({'name': label, 'expectedFailure': label != 'positive', 'observedFailure': failed})
    (output/'controls.json').write_text(json.dumps({'passed': True, 'controls': results}, indent=2)+'\n')
    (output/'results.json').write_text((output/'positive/results.json').read_text())
    print((output/'results.json').read_text())
    print((output/'controls.json').read_text())

if __name__ == '__main__':
    main()
