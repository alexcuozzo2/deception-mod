#!/bin/bash
# Daily PolyMod launcher, r1. No automatic report ZIPs or Finder windows.
# Usage: bash launch-polytopia.sh [launch|check|report] [--game PATH]
# Includes version-specific checks for the already installed compatibility fixes.
set -euo pipefail
umask 077
unset DYLD_INSERT_LIBRARIES DYLD_LIBRARY_PATH DYLD_FALLBACK_LIBRARY_PATH LD_PRELOAD LD_LIBRARY_PATH
unset ARCHPREFERENCE DOORSTOP_INITIALIZED DOORSTOP_ENABLED DOORSTOP_DISABLE BEPINEX_GAME_ASSEMBLY_PATH
PY="$(command -v python3 || true)"
if [ -z "$PY" ] || [ ! -x "$PY" ]; then
    PY="$(/usr/bin/xcrun --find python3 2>/dev/null || true)"
fi
[ -n "$PY" ] && [ -x "$PY" ] || { printf 'STOP: Python 3 from your existing tools was not found.
' >&2; exit 1; }
exec "$PY" - "$@" <<'POLYMOD_DAILY_PYTHON'
"""Daily launcher for Alex's version-pinned Intel PolyMod installation.
Launch/check: no DLL/config/mod edits. Report: explicitly requested local ZIP only.
"""
import argparse
import configparser
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import signal
import stat
import struct
import subprocess
import sys
import tempfile
import time
import zipfile

EXPECTED = {
 'BepInEx/core/BepInEx.Unity.IL2CPP.dll': '010ed8ba5e102e23f38c141e090cb94cc20c2dc6502fe1cc81f546f20b6d25ae',
 'BepInEx/core/LibCpp2IL.dll': '40ad2ec38371c4602ce8e6c1fb8e2dea506413755ed4568fc89f96c8f6b643b0',
 'BepInEx/core/BepInEx.Core.dll': '9619bb632cec6aa512b9ab6e587f901c34bfdcd50751e5be44d11918e477dd97',
 'BepInEx/core/Il2CppInterop.Runtime.dll': 'bb8630fe5a36b43cc166aa5b4ccd850d24002db5f887760c766f0c7ee8c74e1c',
 'BepInEx/core/Il2CppInterop.HarmonySupport.dll': 'f7612ea2e5a4d473b771f34ee3b20cf32dc0c4ecea0185416474999a17fbcc07',
 'BepInEx/core/MonoMod.RuntimeDetour.dll': '1b3d55f53cb9f9f2550daed4dfd300b247d71698a0ace82bc7b9ff38d563594f',
 'BepInEx/core/MonoMod.Utils.dll': '72a5b4249985873d7ecb160cbbed7a1e6540a38ee7618c72c64b9cb068c1438a',
 'BepInEx/core/libfunchook.dylib': 'd5d2f15700279f3bdd25fb3f000695ca771c16a66717681735b261e36b909d0b',
 'BepInEx/core/Polymod.MacModuleBridge.dll': 'c136e9c741e26ca4f91b151f1f485783a6a1bd191fec3b4a95ee953a941d590d',
 'BepInEx/core/Polymod.MacMetadataBridge.dll': 'a41c42735adc36dcbd883aa8ce65d18a15ba508ba9eb9e2e19b3def585f4bd58',
 'BepInEx/plugins/PolyMod.dll': 'a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016',
}
LOADER_PROFILES = {
 'beta': ('PolyMod multiplayer beta, unmodified user-supplied DLL', 'a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016'),
 'public': ('PolyMod 1.2.18, unmodified public release', 'db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d'),
}
LOADER_PROFILE = 'beta'
DOORSTOP_NAME = 'libdoorstop-pr110-ae0bb92-intel.dylib'
DOORSTOP_COMMIT = 'ae0bb9268a1eb68208b8e9288f0a3847230b0bbf'
LOG_NAMES = ['launch.log', 'session.json', 'near-memory.log', 'funchook-native.log',
             'bepinex.log', 'player.log']
NATIVE_SPEC = {'uuid': 'e3a8a19c1b663b1dba6f31c02f898bcf', 'text_size': 71032832, 'candidate_rva': 5693792, 'guards': [{'label': 'export_il2cpp_image_get_class', 'rva': 5268368, 'length': 10, 'sha256': '037d97a62e3294d20e9c6552fe36389d6eb1187b5719bb1ada977f7cb0d4b8bb'}, {'label': 'logged_Image_GetType', 'rva': 5744688, 'length': 18, 'sha256': '62a58eb9c2eecd40e8c53ea2043e57319b7743b025ea1642a32eba63a92bb19f'}, {'label': 'logged_getTypeInfoFromHandle', 'rva': 5812928, 'length': 10, 'sha256': 'f9f69482fa5fe5585003a76557ad7a3db0ddbb68bf79180a267759d7ba487518'}, {'label': 'suspected_MetadataCache_target', 'rva': 5693520, 'length': 63, 'sha256': '7e3f601e8a6cbfa99c1bc8708e282bf2cf1bb6d6d62aa1fb44d1828520c48a64'}, {'label': 'suspected_MetadataCache_target', 'rva': 5693792, 'length': 752, 'sha256': 'eea9b323b05e15ffde36fee474ea27a33f3ce73b4f375edb3b36aafa754364d0'}, {'rva': 5778272, 'length': 221, 'sha256': 'd2abc4ff848ecc94d6b2c23d8a4d7e95b37225245e98bf7d0e661331b2382735', 'label': 'static-field-two-stage-path'}, {'rva': 5810400, 'length': 10, 'sha256': '1c8a1a987316b4806f5f0e794b398958380c0741ecb511c71c9994fa153a06ec', 'label': 'two-argument-field-default-thunk'}, {'rva': 5551280, 'length': 33, 'sha256': '7f5dd7db21350fb20373fa26e59ced4d600643628b282c92f8c8b219a0947deb', 'label': 'four-argument-decoder-not-hook-target'}], 'field_candidate_rva': 5810400, 'previous_wrong_field_target_rva': 5551280}

class Stop(RuntimeError):
    pass

def sha(data):
    return hashlib.sha256(data).hexdigest()

def filehash(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for b in iter(lambda: f.read(1024*1024), b''):
            h.update(b)
    return h.hexdigest()

def regular(path):
    if not stat.S_ISREG(path.lstat().st_mode):
        raise Stop('Expected a regular file, not a symlink: ' + str(path))
    return path

def checked_output(args):
    r = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    if r.returncode:
        raise Stop('Command failed: ' + repr(args) + '\n' + r.stderr.strip())
    return r.stdout.strip()

def running(name):
    r = subprocess.run(['/usr/bin/pgrep', '-u', str(os.getuid()), '-x', name],
                       stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, text=True)
    if r.returncode not in (0, 1):
        raise Stop('Could not check running processes: ' + r.stderr.strip())
    return r.returncode == 0

def require_closed():
    if running('Polytopia'):
        raise Stop('Polytopia is already running. Quit it normally before this command.')

def config_check(path):
    c = configparser.ConfigParser(interpolation=None, strict=True)
    c.optionxform = str
    c.read_string(regular(path).read_text(encoding='utf-8-sig'))
    required = {('Detours','DetourProviderType'):'Funchook',
                ('IL2CPP','ScanMethodRefs'):'false',
                ('Logging','UnityLogListening'):'false'}
    for (section, key), expected in required.items():
        if c.get(section, key, fallback='').strip() != expected:
            raise Stop('Expected [%s] %s = %s. No configuration was changed.' %
                       (section, key, expected))
    return c

def clean_env():
    env = os.environ.copy()
    remove = {'ARCHPREFERENCE', 'LD_PRELOAD', 'LD_LIBRARY_PATH',
              'BEPINEX_GAME_ASSEMBLY_PATH', 'MONOMOD_DBGLOG', 'MONOMOD_DBGLOG_PATH',
              'MallocLogFile', 'POLYMOD_NEAR_MEMORY_LOG', 'POLYMOD_FUNCHOOK_NATIVE_LOG',
              'DNSPY_UNITY_DBG2'}
    for k in list(env):
        if k in remove or k.startswith('DYLD_') or k.startswith('DOORSTOP_'):
            env.pop(k, None)
    return env

def native_check(path, spec=None):
    # This is a file-only Mach-O check. No native library is loaded or executed.
    spec = spec or NATIVE_SPEC
    size = path.stat().st_size
    with open(path, 'rb') as f:
        def read(o,n):
            if o < 0 or n < 0 or o+n > size: raise Stop('Native read outside file')
            f.seek(o); b=f.read(n)
            if len(b)!=n: raise Stop('Incomplete native read')
            return b
        first=read(0,8); magic=struct.unpack_from('>I',first)[0];base=0;slicesize=size
        if magic in (0xCAFEBABE,0xCAFEBABF):
            count=struct.unpack_from('>I',first,4)[0]
            if not 0<count<=32: raise Stop('Invalid architecture count')
            archsize=20 if magic==0xCAFEBABE else 32;matches=[]
            for i in range(count):
                a=read(8+archsize*i,archsize);cpu=struct.unpack_from('>I',a)[0]
                o,n=struct.unpack_from('>II' if archsize==20 else '>QQ',a,8)
                if o+n>size:raise Stop('Architecture outside file')
                if cpu==0x01000007:matches.append((o,n))
            if len(matches)!=1:raise Stop('Expected one Intel native slice')
            base,slicesize=matches[0]
        hdr=read(base,32);magic,cpu=struct.unpack_from('<II',hdr)
        if (magic,cpu)!=(0xFEEDFACF,0x01000007):raise Stop('Not Intel Mach-O')
        count,cmdsize=struct.unpack_from('<II',hdr,16)
        if not 0<count<=512 or not 0<cmdsize<=65536 or 32+cmdsize>slicesize:
            raise Stop('Invalid native load commands')
        commands=read(base+32,cmdsize);p=0;uuid=None;text=None
        for _ in range(count):
            if p+8>len(commands):raise Stop('Truncated load command')
            cmd,n=struct.unpack_from('<II',commands,p)
            if n<8 or n%8 or p+n>len(commands):raise Stop('Invalid command size')
            if cmd==0x1b:
                if n!=24 or uuid is not None:raise Stop('Malformed native UUID')
                uuid=commands[p+8:p+24].hex()
            if cmd==0x19:
                if n<72:raise Stop('Truncated segment')
                if commands[p+8:p+24].rstrip(b'\0')==b'__TEXT':
                    if text is not None:raise Stop('Duplicate text segment')
                    text=struct.unpack_from('<QQQQ',commands,p+24)
            p+=n
        if p!=cmdsize or uuid!=spec['uuid']:
            raise Stop('The native game version/UUID changed. Do not reuse version-specific patches without review.')
        if text is None or text[0]!=0 or text[1]!=spec['text_size']:
            raise Stop('Unexpected native text layout')
        va,vs,fo,fs=text
        for g in spec['guards']:
            r,n=g['rva'],g['length']
            if r<va or r-va+n>fs or fo+r-va+n>slicesize:raise Stop('Guard outside native text')
            if sha(read(base+fo+r-va,n))!=g['sha256']:
                raise Stop('Native code changed at RVA 0x%X. Nothing launched.'%r)

def preflight(game, home):
    require_closed()
    if not game.is_dir() or not (game/'Polytopia.app').is_dir():
        raise Stop('Game folder not found: '+str(game))
    for rel in ['BepInEx','BepInEx/core','BepInEx/compat-backups']:
        p=game/rel
        if p.is_symlink() or not p.is_dir():raise Stop('Expected installed directory: '+str(p))
    cfg=config_check(game/'BepInEx/config/BepInEx.cfg')
    for rel,expected in EXPECTED.items():
        if filehash(regular(game/rel))!=expected:
            raise Stop('A version-pinned library differs: '+str(game/rel)+
                       '\nThis launcher makes no repairs. Keep your backup; do not reinstall automatically.')
    app=game/'Polytopia.app';exe=app/'Contents/MacOS/Polytopia'
    assembly=app/'Contents/Frameworks/GameAssembly.dylib'
    version=plistlib.loads((app/'Contents/Info.plist').read_bytes()).get('CFBundleShortVersionString')
    if version!='2.17.3.16375':raise Stop('Expected Polytopia 2.17.3.16375; found '+str(version))
    native_check(assembly)
    if not (game/'Polytopia_Data/il2cpp_data/Metadata/global-metadata.dat').is_file():
        raise Stop('The existing Polytopia_Data link/data path is missing.')
    work=Path(os.environ.get('POLYMOD_DOORSTOP_WORK', str(home/'polymod-doorstop-test-ae0bb92'))).expanduser()
    cand=work/'candidate-intel';doorstop=cand/DOORSTOP_NAME
    if (cand/'source-commit.txt').read_text().strip()!=DOORSTOP_COMMIT:
        raise Stop('Unexpected Doorstop revision record')
    lines=(cand/'SHA256SUMS').read_text().splitlines()
    hashes=[]
    for line in lines:
        m=re.fullmatch(r'([0-9a-fA-F]{64})\s+[* ]?(?:\./)?'+re.escape(DOORSTOP_NAME),line.strip())
        if m:hashes.append(m.group(1).lower())
    if len(hashes)!=1 or filehash(regular(doorstop))!=hashes[0]:
        raise Stop('External Doorstop candidate/checksum differs or is missing.')
    checked_output(['/usr/bin/arch','-x86_64','/usr/bin/true'])
    for p in [exe,assembly,app/'Contents/Frameworks/UnityPlayer.dylib',doorstop,
              game/'dotnet/libcoreclr.dylib',game/'BepInEx/core/libfunchook.dylib']:
        checked_output(['/usr/bin/lipo',str(p),'-verify_arch','x86_64'])
    return exe,assembly,doorstop,cfg

def ensure_logdir(home):
    d=home/'Library/Logs/Polytopia-Modded'
    if d.is_symlink():raise Stop('Refusing a symlink at the private launcher log directory')
    d.mkdir(parents=True,exist_ok=True,mode=0o700)
    return d

def safe_write(path,data):
    if path.exists() or path.is_symlink():regular(path)
    fd=os.open(str(path),os.O_WRONLY|os.O_CREAT|os.O_TRUNC|os.O_NOFOLLOW,0o600)
    with os.fdopen(fd,'wb') as f:f.write(data)

def rotate_logs(d):
    # Only this script's explicitly named log files are rotated; no recursive deletion.
    for n in LOG_NAMES:
        p=d/n;q=d/(p.stem+'.previous'+p.suffix)
        for x in (p,q):
            if x.exists() or x.is_symlink():regular(x)
    for n in LOG_NAMES:
        p=d/n;q=d/(p.stem+'.previous'+p.suffix)
        if p.exists():os.replace(str(p),str(q))
    for n in ['launch.log','near-memory.log','funchook-native.log']:
        safe_write(d/n,b'')

def launch_args(game,exe,assembly,doorstop,d):
    envs={
      'DYLD_INSERT_LIBRARIES':str(doorstop),
      'DYLD_LIBRARY_PATH':':'.join(map(str,[game,game/'dotnet',game/'Polytopia.app/Contents/Frameworks'])),
      'DOORSTOP_ENABLED':'1','DOORSTOP_IGNORE_DISABLED_ENV':'0',
      'DOORSTOP_TARGET_ASSEMBLY':str(game/'BepInEx/core/BepInEx.Unity.IL2CPP.dll'),
      'DOORSTOP_CLR_RUNTIME_CORECLR_PATH':str(game/'dotnet/libcoreclr.dylib'),
      'DOORSTOP_CLR_CORLIB_DIR':str(game/'dotnet'),
      'BEPINEX_GAME_ASSEMBLY_PATH':str(assembly),
      'MONOMOD_DBGLOG':'0',
      'POLYMOD_NEAR_MEMORY_LOG':str(d/'near-memory.log'),
      'POLYMOD_FUNCHOOK_NATIVE_LOG':str(d/'funchook-native.log'),
    }
    args=['/usr/bin/arch','-x86_64']
    for k,v in envs.items():args.extend(['-e',k+'='+v])
    return args+[str(exe)]

def launch(game,home,check_only=False):
    exe,assembly,doorstop,cfg=preflight(game,home)
    print('VERIFIED LOADER: '+LOADER_PROFILES[LOADER_PROFILE][0],flush=True)
    print('PolyMod.dll SHA256: '+filehash(game/'BepInEx/plugins/PolyMod.dll'),flush=True)
    if check_only:
        print('CHECK PASSED: installed compatibility hashes, eight native guards, configuration and Intel architectures match.')
        print('No game launched; no ZIP, configuration edit, or repair performed.')
        return 0
    if not any(running(n) for n in ['steam_osx','Steam','steam']):
        raise Stop('Open Steam first, then run the launcher again. It will not start or update Steam for you.')
    lock=game/'BepInEx/.polymod-hub-fix.lock'
    try:lock.mkdir()
    except FileExistsError:raise Stop('Another launcher/check is active, or its lock remains: '+str(lock))
    try:
        require_closed()
        d=ensure_logdir(home);rotate_logs(d)
        args=launch_args(game,exe,assembly,doorstop,d)
        start=time.time();record={'start_unix':start,'start_local':time.strftime('%Y-%m-%d %H:%M:%S %z'),
                                  'game':str(game),'launcher':'official-loader-profiles-v1','status':'launching',
                                  'loader_profile':LOADER_PROFILE,'loader_name':LOADER_PROFILES[LOADER_PROFILE][0],
                                  'loader_sha256':filehash(game/'BepInEx/plugins/PolyMod.dll'),
                                  'automatic_zip':False,'monomod_extra_trace':False,
                                  'config_sha256_before':filehash(game/'BepInEx/config/BepInEx.cfg')}
        safe_write(d/'session.json',(json.dumps(record,indent=2)+'\n').encode())
        print('Launching modded Polytopia. Leave the opening sequence untouched.',flush=True)
        print('Local logs: '+str(d)+' (latest and previous launch only).',flush=True)
        with open(d/'launch.log','ab',buffering=0) as out:
            out.write(('Daily launcher r1. Game: '+str(game)+'\n').encode())
            out.write(('Start: '+record['start_local']+'\n').encode())
            child=subprocess.Popen(args,cwd=str(game),env=clean_env(),stdin=subprocess.DEVNULL,
                                   stdout=out,stderr=subprocess.STDOUT)
            record['game_pid']=child.pid
            safe_write(d/'session.json',(json.dumps(record,indent=2)+'\n').encode())
            try:raw=child.wait()
            except KeyboardInterrupt:
                # Do not release the launcher lock while the GUI might still be alive.
                print('Quit Polytopia normally; keeping the launcher attached until it exits.',flush=True)
                prior=signal.signal(signal.SIGINT,signal.SIG_IGN)
                try:raw=child.wait()
                finally:signal.signal(signal.SIGINT,prior)
            status=128-raw if raw<0 else raw
            out.write(('\nGame exit status: %d. This alone is not a mod-success verdict.\n'%status).encode())
        record.update(status='exited',raw_returncode=raw,exit_status=status,end_unix=time.time(),
                      config_sha256_after=filehash(game/'BepInEx/config/BepInEx.cfg'))
        safe_write(d/'session.json',(json.dumps(record,indent=2)+'\n').encode())
        for source,dest in [(game/'BepInEx/LogOutput.log',d/'bepinex.log'),
                            (home/'Library/Logs/Midjiwan/Polytopia/Player.log',d/'player.log')]:
            try:
                if source.is_file() and not source.is_symlink() and source.stat().st_mtime>=start:
                    safe_write(dest,source.read_bytes())
            except OSError as e:print('Could not retain log snapshot: '+str(e),file=sys.stderr)
        print('Polytopia exited with status %d. No ZIP created.'%status)
        if status:print('To collect evidence separately, run this script with: report')
        return status
    finally:
        # The directory was created by this invocation and remains empty.
        try:lock.rmdir()
        except OSError:pass

def read_manifest(p):
    if p.is_symlink():return None,'symlink: not opened'
    try:
        if p.is_dir():
            m=regular(p/'manifest.json')
            if m.stat().st_size>262144:return None,'manifest too large'
            raw=m.read_bytes()
        elif p.is_file() and p.suffix.lower()=='.polymod':
            with zipfile.ZipFile(p) as z:
                names=[i for i in z.infolist() if i.filename=='manifest.json']
                if len(names)!=1:return None,'expected one root manifest.json'
                if names[0].file_size>262144:return None,'manifest too large'
                raw=z.read(names[0])
        else:return None,'not a mod archive/folder'
        m=json.loads(raw.decode('utf-8-sig'))
        if not isinstance(m,dict):return None,'manifest is not an object'
        return m,None
    except (OSError,ValueError,KeyError,zipfile.BadZipFile) as e:return None,str(e)

def inventory(folder,limit=3000):
    result=[]
    if not folder.is_dir() or folder.is_symlink():return result
    for parent,dirs,names in os.walk(str(folder),followlinks=False):
        dirs.sort();names.sort()
        for name in dirs+names:
            p=Path(parent)/name
            try:
                s=p.lstat();kind='symlink' if stat.S_ISLNK(s.st_mode) else ('dir' if stat.S_ISDIR(s.st_mode) else 'file')
                e={'path':str(p.relative_to(folder)),'kind':kind,'bytes':s.st_size}
                if kind=='symlink':e['target']=os.readlink(str(p))
                result.append(e)
            except OSError as e:result.append({'path':str(p),'error':str(e)})
            if len(result)>=limit:
                result.append({'notice':'inventory truncated at '+str(limit)+' entries'});return result
    return result

def report(game,home):
    """Collect existing evidence on explicit request; never launch or change mods."""
    require_closed()
    desktop=home/'Desktop'
    if not desktop.is_dir():raise Stop('Desktop directory is not available')
    fd,name=tempfile.mkstemp(prefix='polymod-castrum-report.',suffix='.zip',dir=str(desktop));os.close(fd)
    dest=Path(name);index=[];total=[0];budget=256*1024*1024
    mods=[];included=[];crashes=[];notes=[]
    try:
        with zipfile.ZipFile(dest,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
            def add(p,arc,limit=24*1024*1024,tail=True):
                try:
                    regular(p);before=p.stat();size=before.st_size
                    if size>limit and not tail:
                        index.append({'source':str(p),'omitted':'over per-file size limit','bytes':size});return False
                    offset=max(0,size-limit)
                    if total[0]+min(size,limit)>budget:
                        index.append({'source':str(p),'omitted':'report size budget'});return False
                    with open(p,'rb') as f:f.seek(offset);data=f.read(limit+1)
                    if len(data)>limit:raise OSError('File grew during collection')
                    after=p.stat()
                    z.writestr(arc,data);total[0]+=len(data)
                    index.append({'source':str(p),'archive':arc,'original_bytes':size,'included_bytes':len(data),
                                  'start_offset':offset,'mtime_unix':before.st_mtime,'sha256_of_included_bytes':sha(data),
                                  'changed_while_reading':(before.st_mtime_ns,before.st_size)!=(after.st_mtime_ns,after.st_size)})
                    return True
                except (OSError,Stop) as e:index.append({'source':str(p),'omitted':str(e)});return False
            for p,arc in [(game/'BepInEx/LogOutput.log','installed-logs/LogOutput.log'),
                          (home/'Library/Logs/Midjiwan/Polytopia/Player.log','installed-logs/Player.log'),
                          (game/'BepInEx/config/BepInEx.cfg','configuration/BepInEx.cfg')]:add(p,arc)
            d=home/'Library/Logs/Polytopia-Modded'
            for n in LOG_NAMES+['last-error.txt']:
                p=d/n
                if p.exists():add(p,'daily-launcher/'+n)
            for n in LOG_NAMES:
                p=d/(Path(n).stem+'.previous'+Path(n).suffix)
                if p.exists():add(p,'previous-launch/'+p.name)
            hashes={}
            for rel in EXPECTED:
                try:hashes[rel]=filehash(regular(game/rel))
                except (OSError,Stop) as e:hashes[rel]='unavailable: '+str(e)
            z.writestr('framework-checksums.json',json.dumps(hashes,indent=2)+'\n')
            mdir=game/'Mods'
            if mdir.is_dir():
                for p in sorted(mdir.iterdir(),key=lambda p:p.name):
                    m,error=read_manifest(p)
                    entry={'name':p.name,'manifest':m,'note':error};mods.append(entry)
                    if not m or m.get('id') not in ('cst','polibrary'):continue
                    if p.is_file():
                        if add(p,'mod-packages/'+p.name,64*1024*1024,False):included.append(p.name)
                    elif p.is_dir():
                        for parent,dirs,names in os.walk(str(p),followlinks=False):
                            dirs[:]=[n for n in dirs if not (Path(parent)/n).is_symlink()]
                            for n in sorted(names):
                                f=Path(parent)/n
                                if f.suffix.lower() in ('.dylib','.exe'):
                                    notes.append('Excluded unexpected native/executable mod file: '+str(f));continue
                                if add(f,'mod-folders/'+p.name+'/'+f.relative_to(p).as_posix(),64*1024*1024,False):
                                    included.append(str(f.relative_to(mdir)))
                z.writestr('Mods-inventory.json',json.dumps(inventory(mdir),indent=2)+'\n')
            z.writestr('mod-manifests.json',json.dumps(mods,indent=2)+'\n')
            # Include at most the three newest Polytopia crash-report candidates,
            # with timestamps; do not imply every included report matches this run.
            cand=[]
            for root in [home/'Library/Logs/DiagnosticReports',home/'Library/Logs/DiagnosticReports/Retired',
                         Path('/Library/Logs/DiagnosticReports')]:
                if not root.is_dir():continue
                try:
                    for p in root.iterdir():
                        if 'polytopia' in p.name.lower() and p.suffix.lower() in ('.ips','.crash') and not p.is_symlink() and p.is_file():
                            cand.append((p.stat().st_mtime,p))
                except OSError as e:notes.append(str(e))
            for num,(mtime,p) in enumerate(sorted(cand,key=lambda x:x[0],reverse=True)[:3],1):
                if add(p,'apple-crashes/%d-%s'%(num,p.name),12*1024*1024,False):
                    crashes.append({'file':p.name,'mtime_unix':mtime})
            summary={'collected_local':time.strftime('%Y-%m-%d %H:%M:%S %z'),
                     'game':str(game),'game_was_not_launched':True,'no_mods_moved_or_patched':True,
                     'crash_candidates':crashes,'mod_files_included':included,'notes':notes,
                     'scope':'Existing logs, configuration, checksums, mod inventory, Castrum/Polibrary packages, and newest Polytopia crash candidates. No saves or full native game library.',
                     'warning':'Crash candidates may predate the latest run. Match incident PID/time. Review local paths and identifiers before sharing.'}
            z.writestr('report.json',json.dumps(summary,indent=2)+'\n')
            z.writestr('collection-index.json',json.dumps(index,indent=2)+'\n')
        with zipfile.ZipFile(dest) as z:
            if z.testzip():raise Stop('ZIP verification failed')
    except Exception:
        # Preserve the partial evidence without falsely presenting it as ready.
        os.replace(str(dest),str(dest)+'.incomplete');raise
    print('REPORT READY: '+str(dest))
    print('Apple crash candidates: %d; Castrum/Polibrary files included: %d.'%(len(crashes),len(included)))
    print('No game launch, mod changes, Finder window, or automatic upload. Review the ZIP before sharing.')
    return 0

def main():
    global LOADER_PROFILE
    a=argparse.ArgumentParser(description=__doc__)
    a.add_argument('mode',nargs='?',choices=['launch','check','report'],default='launch')
    a.add_argument('--game',type=Path,help='Non-default Steam game folder')
    a.add_argument('--loader',choices=sorted(LOADER_PROFILES),default='beta',help='Exact unmodified loader to require; this never swaps libraries')
    args=a.parse_args()
    LOADER_PROFILE=args.loader
    EXPECTED['BepInEx/plugins/PolyMod.dll']=LOADER_PROFILES[LOADER_PROFILE][1]
    if sys.platform!='darwin':raise Stop('Run this launcher on your Mac')
    if os.getuid()==0:raise Stop('Run as your normal user, not with sudo')
    home=Path.home();game=(args.game or home/'Library/Application Support/Steam/steamapps/common/The Battle of Polytopia').expanduser().resolve()
    if args.mode=='report':return report(game,home)
    return launch(game,home,args.mode=='check')

if __name__=='__main__':
    try:sys.exit(main())
    except (Stop,OSError,ValueError,configparser.Error) as e:
        msg='STOP: '+str(e)+'\nNo framework library, configuration, or mod was changed by this command.\n'
        print(msg,file=sys.stderr)
        try:safe_write(ensure_logdir(Path.home())/'last-error.txt',msg.encode())
        except Exception:pass
        sys.exit(1)

POLYMOD_DAILY_PYTHON
