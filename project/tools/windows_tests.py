"""Run existing model and online fixtures using the Windows bundled CoreCLR."""
import json
from pathlib import Path
import tempfile
import windows_build


def run(api,game,dotnet,host):
    if host is None: raise api.Stop('Supply --test-host built from coreclr-probe-windows.cpp.')
    host=api.regular(host.resolve())
    survey=windows_build.load_survey(api.ROOT)
    if survey.inspect_file(game/'GameAssembly.dll',native=True)['target_rid']!='win-x64':
        raise api.Stop('Expected a Windows x64 game.')
    dotnet,compiler=windows_build.find_compiler(api,dotnet)
    runtime=game/'dotnet'
    refs=[p for p in sorted(runtime.glob('*.dll')) if windows_build.managed_reference(survey,p)]
    build=api.ROOT/'build';build.mkdir(exist_ok=True)
    work=Path(tempfile.mkdtemp(prefix='windows-tests-',dir=build))
    print('Windows tests: '+str(work),flush=True)
    original_call=api.call
    def call(args,log=None,env=None,cwd=None):
        args=list(args)
        if str(args[0])=='/usr/bin/arch':
            if args[1]!='-x86_64' or Path(args[2])!=host:raise api.Stop('Unexpected test-host command.')
            args=args[2:]
        if Path(args[0]) not in (dotnet,host):raise api.Stop('Unexpected Windows test command.')
        return windows_build.compiler_command(api,args,log,env=env,cwd=cwd)
    before=api.same_tree_sources()
    runtime_before={p.name:api.sha(p) for p in refs}
    api.call=call
    try:
        with (work/'tests.log').open('w',encoding='utf-8') as log:
            sources=sorted((api.ROOT/'src/Core').glob('*.cs'))+sorted((api.ROOT/'tests').glob('*.cs'))
            api.csc(dotnet,compiler,sources,work/'Deception.ModelTests.dll',refs,log)
            env=api.clean_env()
            env['DECEPTION_TEST_OUTPUT']=str(work/'model-test-results.json')
            env['DECEPTION_TREE_FIXTURE']=str(api.ROOT/'tests/native-tree.synthetic.json')
            env['DECEPTION_CAPTURED_TREE_FIXTURE']=str(api.ROOT/'tests/native-tree.captured.json')
            call([host,runtime,work,'Deception.ModelTests','Deception.Tests.KernelTests','Run'],log=log,env=env)
            api.validate_model_results(json.loads((work/'model-test-results.json').read_text()))
            api.run_online_fixtures(dotnet,compiler,work,refs,log,host,runtime)
            sources=[api.ROOT/'src/PolyMod/NativePlatformContract.cs',api.ROOT/'src/PolyMod/CompilerMetadata.cs',
                     api.ROOT/'tests/windows-port/CandidateGateTests.cs']
            api.csc(dotnet,compiler,sources,work/'Deception.WindowsGateTests.dll',refs,log,
                    defines=('DECEPTION_WINDOWS_PORT_CANDIDATE',))
            call([host,runtime,work,'Deception.WindowsGateTests','Deception.WindowsPortTests.CandidateGateTests','Run'],log=log)
            sources=[api.ROOT/'src/PolyMod/NativeWindowsApproval.cs',api.ROOT/'src/PolyMod/CompilerMetadata.cs',
                     api.ROOT/'tests/windows-port/WindowsApprovalTests.cs']
            api.csc(dotnet,compiler,sources,work/'Deception.WindowsApprovalTests.dll',refs,log,
                    defines=('DECEPTION_WINDOWS_PORT_CANDIDATE',))
            call([host,runtime,work,'Deception.WindowsApprovalTests','Deception.WindowsPortTests.WindowsApprovalTests','Run'],log=log)
        if before!=api.same_tree_sources() or runtime_before!={p.name:api.sha(p) for p in refs}:
            raise api.Stop('Test inputs changed during execution.')
        (work/'receipt.json').write_text(json.dumps({'status':'MODEL_AND_ONLINE_FIXTURES_PASSED',
            'sources':before,'runtime':runtime_before,'host_sha256':api.sha(host),
            'native_gameplay':'NOT RUN','multiplayer_match':'NOT RUN'},indent=2)+'\n')
        print('Existing model and online fixture checks passed under Windows bundled CoreCLR.')
    finally:api.call=original_call
