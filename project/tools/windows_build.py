"""Windows inventory, compilation, diagnostic plugin and private candidate build.

Does not launch or install the game. Only windows-candidate embeds the explicitly
gated Windows profile and audited native payloads; gameplay acceptance is separate.
"""
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile


def load_survey(root):
    spec = importlib.util.spec_from_file_location('windows_target_survey', root/'tools/native-platform-survey.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def quote_path(path):
    if any(c in str(path) for c in '\r\n"'):
        raise ValueError('Unsupported path characters')
    return str(path)


def compiler_command(api, args, log=None, env=None, cwd=None):
    # Only synchronous dotnet --list-sdks and dotnet exec csc.dll are used here.
    # Popen owns the compiler; communicate drains both output streams. On timeout
    # or cancellation kill and reap this exact process, never unrelated apps.
    command = [quote_path(p) for p in args]
    if log:
        log.write(repr(command)+'\n'); log.flush()
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                               text=True, encoding='utf-8', errors='replace', env=env or api.clean_env(),cwd=cwd)
    try:
        output, _ = process.communicate(timeout=180)
    except BaseException:
        process.kill(); process.communicate()
        raise
    if log:
        log.write(output); log.flush()
    if process.returncode:
        raise api.Stop('Windows compiler failed:\n'+output[-12000:])
    return output


def find_compiler(api, dotnet):
    if dotnet is None:
        raise api.Stop('Supply --dotnet pointing to an existing SDK dotnet.exe; no SDK is downloaded by this tool.')
    dotnet = api.regular(dotnet.expanduser().resolve())
    candidates = []
    for line in compiler_command(api, [dotnet, '--list-sdks']).splitlines():
        match = re.fullmatch(r'(\S+)\s+\[(.+)\]', line.strip())
        if match:
            compiler = Path(match[2])/match[1]/'Roslyn/bincore/csc.dll'
            if compiler.is_file():
                candidates.append((tuple(map(int, re.findall(r'\d+', match[1]))), compiler))
    if not candidates:
        raise api.Stop('No Roslyn SDK compiler found at the supplied dotnet executable.')
    return dotnet, sorted(candidates)[-1][1]


def managed_reference(survey, path):
    reader = survey.Reader(path)
    try:
        _, magic, _, optional, size, directories, _ = survey.pe_header(reader)
        count = reader.unpack('<I', optional + (108 if magic == 0x20B else 92))[0]
        if count <= 14 or directories + 15*8 > optional + size: return False
        rva, length = reader.unpack('<II', directories + 14*8)
        return rva != 0 and length >= 72
    finally: reader.close()


def references(api, game):
    survey = load_survey(api.ROOT)
    # Unlike macOS .dylibs, Windows native runtime DLLs share the managed suffix.
    refs = [p for p in sorted((game/'dotnet').glob('*.dll')) if managed_reference(survey,p)]
    for name in ('System.Private.CoreLib.dll', 'System.Runtime.dll', 'System.Text.Json.dll'):
        api.regular(game/'dotnet'/name)
    refs += [game/p for p in (
        'BepInEx/core/BepInEx.Core.dll', 'BepInEx/core/0Harmony.dll',
        'BepInEx/core/Il2CppInterop.Runtime.dll', 'BepInEx/interop/Il2Cppmscorlib.dll',
        'BepInEx/interop/GameLogicAssembly.dll', 'BepInEx/interop/PolytopiaAssembly.dll',
        'BepInEx/interop/UnityEngine.CoreModule.dll')]
    for path in refs: api.regular(path)
    return refs


def run(api, game, mode, dotnet, exception_probe=None, native_pack=None, mac_mod=None, probe_report=None):
    if os.name != 'nt': raise api.Stop('Windows preparation must run on Windows.')
    if exception_probe is not None and mode != 'windows-probe':
        raise api.Stop('--exception-probe is only valid with windows-probe.')
    survey = load_survey(api.ROOT)
    game = survey.ordinary_path(game, directory=True)
    version = api.regular(game/'version.txt').read_text(encoding='utf-8-sig').strip()
    if version != api.CONTRACT['game_version']:
        raise api.Stop('Game version differs from the source baseline; inspect API changes first.')
    inventory = survey.survey(game)
    if inventory['game_image']['target_rid'] != 'win-x64':
        raise api.Stop('Expected a Windows x64 GameAssembly.dll.')
    build_root = api.ROOT/'build'
    build_root.mkdir(exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='windows-', dir=build_root))
    print('Windows preparation directory: '+str(work), flush=True)
    result = {'status':'INVENTORY_ONLY', 'game_version':version, 'inventory':inventory,
              'runtime_admitted':False, 'gameplay_verified':False,
              'note':'Compile-only DLL has no native bridge payload and must not be installed.'}
    report = work/'windows-build.json'
    def save(): report.write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    save()
    if mode == 'windows-check':
        if not inventory['inventory_complete']: raise api.Stop('Incomplete inventory; see '+str(report))
        print('Windows inventory complete. Runtime remains unadmitted.'); return
    try:
        if not inventory['inventory_complete']: raise api.Stop('Required Windows loader references are missing.')
        dotnet, compiler = find_compiler(api, dotnet)
        refs = references(api, game)
        before = {str(p.relative_to(game)):api.sha(p) for p in refs}
        sources_before = api.same_tree_sources()
        sources = sorted((api.ROOT/'src/Core').glob('*.cs')) + sorted((api.ROOT/'src/PolyMod').glob('*.cs')) + [api.ROOT/'src/Version.cs']
        resources = []
        output = work/'Deception.dll'
        defines = ()
        if mode == 'windows-candidate':
            import windows_candidate
            resources += windows_candidate.prepare(api,game,work,native_pack,mac_mod,probe_report)
            defines = ('DECEPTION_WINDOWS_PORT_CANDIDATE',)
        if mode == 'windows-probe':
            sources = [api.ROOT/'tools/WindowsPortProbe.cs',api.ROOT/'src/PolyMod/CompilerMetadata.cs']
            refs += [api.regular(game/'BepInEx/core/BepInEx.Unity.IL2CPP.dll')]
            before = {str(p.relative_to(game)):api.sha(p) for p in refs}
            methods = []
            for name in ('native-state-seams.json','native-execution-seams.json','native-command-outer-seams.json'):
                methods += [key for key in json.loads((api.ROOT/'tools'/name).read_text()) if key != 'provenance']
            request = work/'probe-request.json'
            request_data = {'game_sha256':inventory['game_image']['sha256'],'methods':methods}
            resources = [(request,'Deception.WindowsProbe.Request.json')]
            if exception_probe is not None:
                exception_probe = api.regular(exception_probe.resolve())
                if survey.inspect_file(exception_probe,native=True)['target_rid'] != 'win-x64':
                    raise api.Stop('Diagnostic helper must be Windows x64.')
                request_data['exception_probe_sha256'] = api.sha(exception_probe)
                resources += [(exception_probe,'Deception.WindowsExceptionProbe.dll')]
            request.write_text(json.dumps(request_data),encoding='utf-8')
            output = work/'Deception.WindowsPortProbe.dll'
        resource_hashes = {str(p):api.sha(p) for p,name in resources}
        with (work/'compile.log').open('w', encoding='utf-8') as log:
            api.csc(dotnet, compiler, sources, output, refs, log, resources=resources,
                    runner=lambda args, log: compiler_command(api,args,log),defines=defines)
        if before != {str(p.relative_to(game)):api.sha(p) for p in refs} or sources_before != api.same_tree_sources():
            raise api.Stop('Inputs changed during compilation; discard this probe.')
        if resource_hashes != {str(p):api.sha(p) for p,name in resources}:
            raise api.Stop('Embedded resource inputs changed during compilation.')
        if inventory != survey.survey(game):raise api.Stop('Game/loader inventory changed during compilation.')
        result.update(status='COMPILE_ONLY_PASSED', references=before, sources=sources_before,
                      compiler_sha256=api.sha(compiler), dll_sha256=api.sha(output))
        if mode == 'windows-probe':
            result.update(status='DIAGNOSTIC_PLUGIN_COMPILED',note='BepInEx diagnostic only; reads method pointers and prologues without installing hooks.')
        if mode == 'windows-candidate':
            artifact = windows_candidate.package(api,work,output)
            result.update(status='PRIVATE_CANDIDATE_BUILT',artifact=str(artifact),artifact_sha256=api.sha(artifact),
                          note='Opt-in gameplay candidate; neither Windows gameplay nor multiplayer acceptance established.')
        print('Private opt-in candidate built; gameplay and multiplayer acceptance pending.' if mode == 'windows-candidate' else
              'Diagnostic plugin compiled; no gameplay support admitted.' if mode == 'windows-probe'
              else 'Managed compilation passed. This is NOT an installable or runtime-admitted mod.')
    except (api.Stop, OSError, ValueError, subprocess.TimeoutExpired) as error:
        result.update(status='COMPILE_ONLY_FAILED', error=str(error))
        raise api.Stop(str(error)) from error
    finally: save()
