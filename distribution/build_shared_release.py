"""Build the private combined release from explicit, local dependency inputs.

No game bindings, Unity base libraries, saves or workstation configuration are copied.
The supplied Mac kit retains corresponding sources and third-party notices.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import stat
import subprocess
import zipfile

REPO = Path(__file__).resolve().parent.parent
MOD_SHA = 'b1fd625b3988373aeaa7ed70b8bc2bcff811487a110560204e69a2b400dbe3a1'
BETA_SHA = '61588813d4b62d133db06fad64a2c440869aaffaf7460ec8bc8e9d82b1be63d7'
DOBBY_SHA = 'd9c38d52ffb4a06a1f807705d855b0a0b4b6578a68820d247f4db290790e4bfe'

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def write(p, value):
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(value, encoding='utf-8', newline='\n')

def checksums(root):
    write(root/'SHA256SUMS.txt', ''.join(f'{digest(p)}  {p.relative_to(root).as_posix()}\n'
        for p in sorted(root.rglob('*')) if p.is_file() and p.name!='SHA256SUMS.txt'))

def build(args):
    if digest(args.mod)!=MOD_SHA or digest(args.dobby)!=DOBBY_SHA:
        raise ValueError('Release input hash differs from the reviewed candidate.')
    original=json.loads((args.mac_kit/'package-manifest.json').read_text())
    for name, expected in original['files'].items():
        if digest(args.mac_kit/name)!=expected: raise ValueError('Mac donor differs: '+name)
    beta=args.mac_kit/'payload/game/BepInEx/plugins/PolyMod.dll'
    if digest(beta)!=BETA_SHA: raise ValueError('Wrong multiplayer beta.')
    args.output.mkdir(parents=True, exist_ok=False)
    root=args.output/'Deception-Setup';mac=root/'Mac/Deception-Mac-Setup';win=root/'Windows'
    shutil.copytree(args.mac_kit,mac)
    shutil.copyfile(args.mod,mac/'mod/alex_deception_m0.polymod')
    for name in ('setup.py','setup.sh'):
        text=(REPO/'distribution/mac-template'/name).read_text(encoding='utf-8').replace("MOD_VERSION = '0.3.100'", "MOD_VERSION = '0.3.101'")
        write(mac/name,text)
    original['files']['mod/alex_deception_m0.polymod']=MOD_SHA
    write(mac/'package-manifest.json',json.dumps(original,indent=2)+'\n')
    write(mac/'START-HERE.md',(REPO/'distribution/SHARED-SETUP.md').read_text(encoding='utf-8'))
    shutil.copyfile(REPO/'distribution/MAC-UPGRADE.command',root/'Mac/Upgrade Deception.command')
    with zipfile.ZipFile(args.windows_loader) as z:
        for item in z.infolist():
            n=item.filename
            allowed=n.startswith(('BepInEx/core/','dotnet/')) or n in ('winhttp.dll','doorstop_config.ini','.doorstop_version')
            if item.is_dir() or not allowed: continue
            if '..' in Path(n).parts or n.startswith('/') or '\\' in n: raise ValueError('Unsafe archive member')
            dest=win/'payload'/n;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(z.read(item))
    for source,relative in [(beta,'BepInEx/plugins/PolyMod.dll'),(args.dobby,'BepInEx/core/dobby.dll'),
                            (args.mod,'Mods/alex_deception_windows_candidate.polymod'),
                            (args.mac_kit/'payload/game/PolyMod.json','PolyMod.json')]:
        dest=win/'payload'/relative;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,dest)
    shutil.copyfile(REPO/'distribution/Install-Windows.ps1',win/'Install-Windows.ps1')
    write(win/'Install or update.cmd','@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Windows.ps1" %*\r\npause\r\n')
    manifest={'gameSha256':'bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926','modSha256':MOD_SHA,
              'files':{p.relative_to(win/'payload').as_posix():digest(p) for p in sorted((win/'payload').rglob('*')) if p.is_file()}}
    write(win/'windows-manifest.json',json.dumps(manifest,indent=2)+'\n')
    shutil.copyfile(REPO/'distribution/SHARED-SETUP.md',root/'START-HERE.md')
    source=root/'Sources/Windows-Dobby';source.mkdir(parents=True)
    donor=root/'Sources/Native-resource-donor';donor.mkdir(parents=True)
    shutil.copyfile(args.mac_kit/'mod/alex_deception_m0.polymod',donor/'alex_deception_m0-0.3.100.polymod')
    write(donor/'README.txt','Build input only, not the playable release. The authoritative Windows candidate builder uses this pinned 0.3.100 archive for Mac native resources. Install the 0.3.101 archive in the platform folders instead.\n')
    subprocess.run(['git','-C',str(args.dobby_source),'archive','--format=zip','--output='+str((source/'Dobby-9d271803.zip').resolve()),'9d271803881f467aabfc24593c7dcaeb710fe861'],check=True)
    for name in ('dobby-short-branches.patch','dobby-branch-test.cpp','DOBBY-FIX.md'):
        shutil.copyfile(REPO/'distribution/windows-ci'/name,source/name)
    shutil.copyfile(args.dobby_source/'LICENSE',source/'LICENSE')
    write(root/'RELEASE.json',json.dumps({'version':'0.3.101','sourceCommit':'04329b6','modSha256':MOD_SHA,
          'betaSha256':BETA_SHA,'windowsDobbySha256':DOBBY_SHA,'status':'experimental; cross-platform match not verified'},indent=2)+'\n')
    checksums(mac);checksums(root)
    archive=args.output/'Deception-Setup-0.3.101-windows-mac.1.zip'
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(root.rglob('*')):
            if not p.is_file():continue
            name='Deception-Setup/'+p.relative_to(root).as_posix()
            info=zipfile.ZipInfo(name,(2026,10,17,0,0,0));info.create_system=3
            executable=p.suffix in ('.sh','.command','.dylib') or p.name=='dotnet'
            info.external_attr=(stat.S_IFREG|(0o755 if executable else 0o644))<<16
            info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,p.read_bytes())
    with zipfile.ZipFile(archive) as z:
        if z.testzip() is not None:raise ValueError('Archive verification failed')
    write(args.output/'SHA256SUMS.txt',digest(archive)+'  '+archive.name+'\n')
    print(archive)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('mac-kit','windows-loader','mod','dobby','dobby-source','output'):
        p.add_argument('--'+name,type=Path,required=True)
    build(p.parse_args())
