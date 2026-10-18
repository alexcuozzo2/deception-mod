"""Build one Windows/Mac candidate on Mac using pinned, unchanged native helpers.

Never installs, downloads or launches the game. Native ABI changes fail closed.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import platform
import subprocess
import tempfile
import zipfile

import manage as api
from cli_metadata import CLI

BASE_ZIP_SHA = 'd423fe902fd4c4875383ada25116bd7c45b4e1b6953de8ecc784fa7015b0cf0c'
BASE_MOD_SHA = 'b1fd625b3988373aeaa7ed70b8bc2bcff811487a110560204e69a2b400dbe3a1'
BASE_MEMBER = 'Deception-Setup/Windows/payload/Mods/alex_deception_windows_candidate.polymod'
LOCK = Path(__file__).with_name('shared-native-lock.json')


def require(value, reason):
    if not value:
        raise api.Stop(reason)


def native_inputs(root):
    files = list((root/'src/Native').rglob('*'))
    files += list((root/'tests/native').glob('*-bridge-harness.cpp'))
    names = json.loads(LOCK.read_text())['guarded_files']
    files += [root/name for name in names]
    return {p.relative_to(root).as_posix():api.sha(api.regular(p)) for p in sorted(set(files)) if p.is_file()}


def validate_native_inputs(root=api.ROOT):
    expected = json.loads(LOCK.read_text())
    require(native_inputs(root) == expected['sources'],
            'Native/platform source changed: build and audit a new Windows native baseline before reusing its helpers.')
    return expected


def donor_dll(base_zip):
    require(api.sha(api.regular(base_zip)) == BASE_ZIP_SHA, 'Unknown shared dependency ZIP.')
    with zipfile.ZipFile(base_zip) as archive:
        require(len(archive.namelist()) == len(set(archive.namelist())), 'Duplicate dependency ZIP members.')
        data = archive.read(BASE_MEMBER)
    require(api.digest(data) == BASE_MOD_SHA, 'Shared native donor hash differs.')
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        require(archive.getinfo('Deception.dll').file_size < 16*1024*1024, 'Oversized donor DLL.')
        return archive.read('Deception.dll')


def standalone_donor(path):
    require(api.sha(api.regular(path))==BASE_MOD_SHA,'Unknown native donor mod.')
    with zipfile.ZipFile(path) as archive:
        require(archive.getinfo('Deception.dll').file_size<16*1024*1024,'Oversized donor DLL.')
        return archive.read('Deception.dll')


def review_package(work,dll,loader):
    manifest=json.loads((api.ROOT/'content/manifest.json').read_text())
    manifest['name']+=' — Official PolyMod local review'
    manifest['description']+=' Review target: official PolyMod 1.2.18. Local/pass-and-play only; online unavailable. Windows experimental.'
    note=('Official unmodified PolyMod 1.2.18 local/pass-and-play REVIEW CANDIDATE. '
          'No loader/runtime bundled. Native gameplay acceptance NOT RUN. '
          'Existing Windows runtime compatibility requirements remain; Windows requires DECEPTION_WINDOWS_PORT_TEST=1. '
          'Not a stock Windows runtime compatibility claim. Preview multiplayer is unavailable on this loader.\n')
    entries={'manifest.json':json.dumps(manifest,indent=2).encode(),
             'localization.json':(api.ROOT/'content/localization.json').read_bytes(),
             'Deception.dll':dll.read_bytes(),'DEVELOPMENT-STATUS.txt':note.encode(),
             'REVIEW-LOADER.json':json.dumps(loader,indent=2).encode()}
    output=work/'Deception-public-review.polymod'
    with zipfile.ZipFile(output,'x',zipfile.ZIP_DEFLATED) as archive:
        for name,data in entries.items():archive.writestr(name,data)
    return output


def embedded_resources(path):
    cli = CLI(path)
    pe = cli.u32(0x3c); optional = pe+24
    directories = optional+(112 if cli.u16(optional)==0x20b else 96)
    header = cli.off(cli.u32(directories+14*8))
    start = cli.off(cli.u32(header+24)); size = cli.u32(header+28)
    result = {}
    for offset, flags, name, implementation in cli.rows.get(40, []):
        name = cli.s(name)
        if name not in expected_resources():
            continue
        require(implementation == 0 and 0 <= offset <= size-4, 'Invalid native resource offset.')
        length = cli.u32(start+offset)
        require(0 < length < 1024*1024 and offset+4+length <= size and name not in result,
                'Invalid native resource length/duplicate.')
        result[name] = cli.b[start+offset+4:start+offset+4+length]
    require(set(result) == expected_resources(), 'Missing shared native resources.')
    for prefix, suffix in [('Deception.', 'dylib'), ('Deception.Win.', 'dll')]:
        for kind in ('State','Execution','Command'):
            stem=prefix+kind+'Bridge'
            require(api.digest(result[stem+'.'+suffix]) == result[stem+'.sha256'].decode().strip(),
                    'Embedded native digest differs: '+stem)
    return result


def expected_resources():
    return {prefix+kind+'Bridge.'+ext for prefix,lib in [('Deception.','dylib'),('Deception.Win.','dll')]
            for kind in ('State','Execution','Command') for ext in (lib,'sha256','seams.json')}


def validate_receipt(path, game):
    receipt=json.loads(api.regular(path).read_text())
    require(receipt.get('status')=='MODEL_TESTS_PASSED_NATIVE_NOT_RUN', 'Successful full Mac build required.')
    require(receipt.get('sources') == api.same_tree_sources(), 'Mac build source is stale; run project/run.sh build.')
    require(receipt.get('framework_and_config') == api.preflight(game), 'Mac build runtime/config differs.')
    require(api.sha(api.regular(Path(receipt['artifact']))) == receipt['artifact_sha256'], 'Mac artifact differs.')
    return receipt


def build(args):
    require(platform.system()=='Darwin', 'This shared build entry point requires the verified Mac environment.')
    game=args.game.expanduser().resolve()
    with api.lock():
        public_loader=None
        if args.public_loader:
            import public_polymod
            public_loader=public_polymod.configure(api,game)
        baseline=validate_native_inputs()
        receipt=validate_receipt(args.mac_receipt.resolve(),game)
        before=api.same_tree_sources(); runtime_before=api.preflight(game)
        work=Path(tempfile.mkdtemp(prefix='shared-',dir=api.ROOT/'build'))
        print('Shared build directory: '+str(work),flush=True)
        donor=work/'native-donor.dll';donor.write_bytes(donor_dll(args.base_zip.resolve()) if args.base_zip else standalone_donor(args.donor_mod.resolve()))
        payloads=embedded_resources(donor)
        expected=json.loads(LOCK.read_text())['resources']
        require({name:api.digest(data) for name,data in payloads.items()}==expected, 'Native resource baseline differs.')
        resources=[]
        for name,data in sorted(payloads.items()):
            path=work/name;path.write_bytes(data);resources.append((path,name))
        resources += [(api.ROOT/'content/mimic-portrait.png','Deception.MimicPortrait.png')]
        resources += [(p,'Deception.UnitArt.'+p.name) for p in sorted((api.ROOT/'content/unit-art').glob('*.png'))]
        resources += [(p,'Deception.SpyEffects.'+p.name) for p in sorted((api.ROOT/'content/spy-effects').glob('*')) if p.suffix in ('.png','.wav')]
        dotnet,compiler=api.sdk(); runtime=game/'dotnet'
        refs=sorted(runtime.glob('*.dll'))+[game/p for p in (
            'BepInEx/core/BepInEx.Core.dll','BepInEx/core/0Harmony.dll','BepInEx/core/Il2CppInterop.Runtime.dll',
            'BepInEx/interop/Il2Cppmscorlib.dll','BepInEx/interop/GameLogicAssembly.dll',
            'BepInEx/interop/PolytopiaAssembly.dll','BepInEx/interop/UnityEngine.CoreModule.dll')]
        ref_hashes={str(p.relative_to(game)):api.sha(api.regular(p)) for p in refs}
        resource_hashes={name:api.sha(p) for p,name in resources}
        sources=sorted((api.ROOT/'src/Core').glob('*.cs'))+sorted((api.ROOT/'src/PolyMod').glob('*.cs'))+[api.ROOT/'src/Version.cs']
        output=work/'Deception.dll'
        with (work/'build.log').open('w') as log:
            api.csc(dotnet,compiler,sources,output,refs,log,resources=resources,defines=('DECEPTION_WINDOWS_PORT_CANDIDATE',))
            import audit_compiler_metadata
            metadata=audit_compiler_metadata.check(output)
            import shared_import_audit
            imports=shared_import_audit.audit(output,donor)
            require(embedded_resources(output)==payloads,'Candidate native resource bytes changed.')
            host=Path(receipt['work'])/'model-test-host'
            # Conditional admission helpers execute under bundled Intel .NET;
            # Windows hooks themselves remain native-platform acceptance work.
            for name,source in [('CandidateGateTests','NativePlatformContract'),('WindowsApprovalTests','NativeWindowsApproval')]:
                api.csc(dotnet,compiler,[api.ROOT/'src/PolyMod'/f'{source}.cs',api.ROOT/'src/PolyMod/CompilerMetadata.cs',
                        api.ROOT/'tests/windows-port'/f'{name}.cs'],work/(name+'.dll'),sorted(runtime.glob('*.dll')),log,
                        defines=('DECEPTION_WINDOWS_PORT_CANDIDATE',))
                api.call(['/usr/bin/arch','-x86_64',host,runtime,work,name,'Deception.WindowsPortTests.'+name,'Run'],log=log)
        require(before==api.same_tree_sources() and runtime_before==api.preflight(game),'Build inputs changed.')
        require(ref_hashes=={str(p.relative_to(game)):api.sha(p) for p in refs},'Compiler references changed.')
        require(resource_hashes=={name:api.sha(p) for p,name in resources},'Embedded inputs changed.')
        validate_native_inputs()
        import windows_candidate
        artifact=windows_candidate.package(api,work,output)
        if public_loader:
            artifact=review_package(work,output,public_loader)
        version=json.loads((api.ROOT/'content/manifest.json').read_text())['version']
        commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=api.ROOT,text=True).strip()
        result={'status':'SHARED_CANDIDATE_BUILT','version':version,'source_commit':commit,'sources':before,
                'artifact':str(artifact),'artifact_sha256':api.sha(artifact),'dll_sha256':api.sha(output),
                'source_dirty':bool(subprocess.check_output(['git','status','--porcelain','--','project'],cwd=api.ROOT.parent,text=True)),
                'base_zip_sha256':BASE_ZIP_SHA,'native_donor_sha256':BASE_MOD_SHA,'native_lock':baseline,
                'references':ref_hashes,'compiler_sha256':api.sha(compiler),'resources':resource_hashes,
                'mac_build_receipt_sha256':api.sha(args.mac_receipt),'compiler_metadata':metadata,'external_imports':imports,
                'public_loader':public_loader,'native_gameplay':'NOT RUN','windows_execution':'NOT RUN','multiplayer':'NOT RUN'}
        (work/'shared-receipt.json').write_text(json.dumps(result,indent=2)+'\n')
        print('Shared candidate: '+str(artifact));print('Receipt: '+str(work/'shared-receipt.json'))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    donor_group=parser.add_mutually_exclusive_group(required=True)
    donor_group.add_argument('--base-zip',type=Path)
    donor_group.add_argument('--donor-mod',type=Path)
    parser.add_argument('--public-loader',action='store_true')
    parser.add_argument('--mac-receipt',type=Path,required=True)
    parser.add_argument('--game',type=Path,default=api.GAME)
    try: build(parser.parse_args())
    except (api.Stop,ValueError,OSError,KeyError,zipfile.BadZipFile) as error:
        raise SystemExit('STOP: '+str(error))
