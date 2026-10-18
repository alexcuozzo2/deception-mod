#!/usr/bin/env python3
"""Installer and version-independent mod updater for the reviewed Intel Mac PolyMod preview stack.

No downloads, game patching, save access, process termination or elevation.
First game launch lets BepInEx generate the recipient's own bindings.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import plistlib
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile

KIT = Path(__file__).resolve().parent
VERSION = '2.17.3.16375'
IMAGE_SHA = '5391c27cf9e63ba3db10b168ba7435da723853b265695821f31942bb7ab95dc0'
RECEIPT = 'deception-mac-setup.json'
MOD_VERSION = '0.3.100'
# update-deception replaces any existing Deception file without creating backups.
# The user explicitly requested version-independent mod-only upgrades.

class Stop(RuntimeError):
    pass

def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()

def regular(path):
    safe_parents(path.parent)
    if path.is_symlink() or not path.is_file():
        raise Stop('Expected an ordinary file: ' + str(path))
    return path

def absent(path):
    if path.exists() or path.is_symlink():
        raise Stop('Already exists; nothing will be overwritten: ' + str(path))

def safe_parents(path):
    for parent in [path] + list(path.parents):
        if parent.is_symlink():
            raise Stop('Refusing a destination through a symbolic link: ' + str(parent))

def run(args):
    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode:
        raise Stop('Command failed: ' + ' '.join(map(str, args)) + '\n' + result.stderr.strip())
    return result.stdout.strip()

def closed(include_steam=False):
    names = ['Polytopia'] + (['steam_osx', 'Steam', 'steam'] if include_steam else [])
    for name in names:
        r = subprocess.run(['/usr/bin/pgrep', '-u', str(os.getuid()), '-x', name],
                           stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        if r.returncode == 0:
            raise Stop('Quit ' + name + ' normally, then rerun this command.')
        if r.returncode != 1:
            raise Stop('Could not check whether ' + name + ' is running.')

def verify_package():
    manifest = json.loads(regular(KIT / 'package-manifest.json').read_text())
    if manifest.get('kit') != 'deception-mac-0382-r1' or not isinstance(manifest.get('files'), dict):
        raise Stop('Unrecognized package manifest.')
    fixed = {'mod/alex_deception_m0.polymod', 'payload/game/PolyMod.json',
             'payload/game/BepInEx/config/BepInEx.cfg', 'payload/game/BepInEx/plugins/PolyMod.dll',
             'payload/launcher/launch-polytopia.sh'}
    doorstop = 'payload/doorstop/candidate-intel/'
    fixed.update(doorstop + n for n in ['libdoorstop-pr110-ae0bb92-intel.dylib', 'source-commit.txt', 'SHA256SUMS', 'LICENSE'])
    for rel, sha in manifest['files'].items():
        parts = rel.split('/')
        if any(p in ('', '.', '..') for p in parts) or '\\' in rel or not re.fullmatch('[0-9a-f]{64}', sha):
            raise Stop('Invalid package path or checksum.')
        flat_library = (len(parts) == 5 and parts[:4] == ['payload', 'game', 'BepInEx', 'core']) or (len(parts) == 4 and parts[:3] == ['payload', 'game', 'dotnet'])
        if rel not in fixed and not flat_library:
            raise Stop('Package destination outside the permitted loader files: ' + rel)
    if not fixed.issubset(manifest['files']):
        raise Stop('Package is missing required files.')
    actual = set()
    for root in ['payload', 'mod']:
        safe_parents(KIT / root)
        if not (KIT / root).is_dir():
            raise Stop('Package directory missing: ' + root)
        for p in (KIT / root).rglob('*'):
            if p.is_symlink():
                raise Stop('Unexpected symbolic link in package: ' + str(p))
            if p.is_file():
                # Finder may create this after a valid ZIP is extracted. It is
                # never a payload destination and is never copied by install().
                if p.name == '.DS_Store':
                    continue
                actual.add(p.relative_to(KIT).as_posix())
    if actual != set(manifest['files']):
        missing = sorted(set(manifest['files']) - actual)
        extra = sorted(actual - set(manifest['files']))
        detail = ('\nMissing: ' + ', '.join(missing[:12]) if missing else '')
        detail += ('\nUnexpected: ' + ', '.join(extra[:12]) if extra else '')
        raise Stop('Package file list differs.' + detail + '\nSend this complete message to Alex.')
    for rel, expected in manifest['files'].items():
        if digest(regular(KIT / rel)) != expected:
            raise Stop('Package checksum differs: ' + rel)
    return manifest

def verify_game(game):
    if platform.system() != 'Darwin':
        raise Stop('This package is for macOS with the Intel Steam game.')
    if os.geteuid() == 0:
        raise Stop('Run as your normal user, without sudo.')
    safe_parents(game)
    app = game / 'Polytopia.app/Contents'
    info = plistlib.loads(regular(app / 'Info.plist').read_bytes())
    found = info.get('CFBundleShortVersionString')
    if found != VERSION:
        raise Stop('This package requires game ' + VERSION + '; found ' + str(found) + '. Send this message to Alex.')
    native = regular(app / 'Frameworks/GameAssembly.dylib')
    if digest(native) != IMAGE_SHA:
        raise Stop('GameAssembly differs from the supported game build. Send this message to Alex; do not bypass the check.')
    regular(app / 'Resources/Data/il2cpp_data/Metadata/global-metadata.dat')
    run(['/usr/bin/arch', '-x86_64', '/usr/bin/true'])
    for p in [app / 'MacOS/Polytopia', native, app / 'Frameworks/UnityPlayer.dylib',
              KIT / 'payload/game/dotnet/libcoreclr.dylib',
              KIT / 'payload/game/BepInEx/core/libfunchook.dylib',
              KIT / 'payload/doorstop/candidate-intel/libdoorstop-pr110-ae0bb92-intel.dylib']:
        run(['/usr/bin/lipo', str(regular(p)), '-verify_arch', 'x86_64'])
    if not os.access(game, os.W_OK):
        raise Stop('Your Steam game folder is not writable by your user.')

def destinations(game, home):
    return {'game': game, 'launcher': home / 'Library/Application Support/PolyModMac',
            'doorstop': home / 'polymod-doorstop-test-ae0bb92'}

def verify_fresh(game, home):
    # Refuse an existing loader, including partial installs and older experiments.
    for name in ['BepInEx', 'dotnet', 'Mods', 'DeceptionNative', 'Polytopia_Data',
                 'PolyMod.json', 'libdoorstop.dylib', 'run_bepinex.sh', '.doorstop_version', RECEIPT]:
        absent(game / name)
    for key, path in destinations(game, home).items():
        safe_parents(path)
        if key != 'game':
            absent(path)

def exclusive_copy(source, target):
    safe_parents(source)
    safe_parents(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    with regular(source).open('rb') as src, target.open('xb') as dst:
        shutil.copyfileobj(src, dst)
    target.chmod(source.stat().st_mode & 0o777)

def install(game, home, manifest):
    verify_fresh(game, home)
    targets = destinations(game, home)
    record = {'schema': 1, 'kit': manifest['kit'], 'game': str(game), 'state': 'installing',
              'created': [], 'game_version': VERSION, 'game_image_sha256': IMAGE_SHA}
    # An exclusive receipt reserves the installation; failures leave evidence,
    # never an automatic deletion or overwrite of another setup.
    with (game / RECEIPT).open('x') as f:
        json.dump(record, f, indent=2)
    try:
        for name in ['BepInEx', 'dotnet', 'Mods']:
            (game / name).mkdir()
            record['created'].append(str(game / name))
        for key in ['launcher', 'doorstop']:
            targets[key].mkdir(parents=True, exist_ok=False)
            record['created'].append(str(targets[key]))
        for rel in sorted(manifest['files']):
            parts = Path(rel).parts
            if parts[0] != 'payload':
                continue
            target = targets[parts[1]].joinpath(*parts[2:])
            exclusive_copy(KIT / rel, target)
        for rel in ['BepInEx/compat-backups', 'BepInEx/patchers']:
            (game / rel).mkdir(parents=True, exist_ok=True)
        (game / 'Polytopia_Data').symlink_to('Polytopia.app/Contents/Resources/Data')
        record['state'] = 'installed'
    except Exception as e:
        record['error'] = str(e)
        print('PARTIAL INSTALL: send the error and ' + str(game / RECEIPT) + ' to Alex. Existing game files were not replaced.', file=sys.stderr)
        raise
    finally:
        regular(game / RECEIPT).write_text(json.dumps(record, indent=2) + '\n')
    print('INSTALLED: PolyMod preview and the pinned Mac compatibility stack.')
    print('Deception is not enabled yet. Open Steam, then run: bash setup.sh launch')

def reported_mod_version(path):
    # Diagnostic only: do not trust a manifest version to authorize replacement.
    try:
        with zipfile.ZipFile(path) as archive:
            info = archive.getinfo('manifest.json')
            if info.file_size > 65536:
                return 'unreadable'
            version = json.loads(archive.read(info)).get('version')
            if isinstance(version, str) and re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', version):
                return version
    except (OSError, ValueError, KeyError, zipfile.BadZipFile):
        pass
    return 'unreadable'

def different_mod(path, found):
    advice = 'Use update-deception from the newest extracted ZIP to upgrade. No mod was replaced.'
    return Stop('Installed Deception differs from this test package.'
                + '\nPackage target: ' + MOD_VERSION
                + '\nInstalled version (reported): ' + reported_mod_version(path)
                + '\nInstalled SHA256: ' + found
                + '\nInstalled file: ' + str(path)
                + '\n' + advice + '\nSend this complete message to Alex.')

def verify_installed(game, home, manifest, allow_mod_replacement=False):
    receipt = json.loads(regular(game / RECEIPT).read_text())
    if receipt.get('kit') != manifest['kit'] or receipt.get('state') != 'installed' or receipt.get('game') != str(game):
        raise Stop('Installation receipt is incomplete or belongs to another setup. Send its message to Alex.')
    targets = destinations(game, home)
    for target in targets.values():
        safe_parents(target)
    for rel, expected in manifest['files'].items():
        parts = Path(rel).parts
        if parts[0] != 'payload' or rel.endswith('BepInEx.cfg') or rel.endswith('PolyMod.json'):
            continue  # BepInEx/PolyMod may serialize their configs on first run.
        path = targets[parts[1]].joinpath(*parts[2:])
        safe_parents(path)
        if digest(regular(path)) != expected:
            raise Stop('Installed dependency changed: ' + str(path))
    if json.loads(regular(game / 'PolyMod.json').read_text()).get('autoUpdate') is not False:
        raise Stop('Keep PolyMod autoUpdate false for this pinned preview setup.')
    for subdir, allowed in [('plugins', {'PolyMod.dll', '.DS_Store'}), ('patchers', {'.DS_Store'})]:
        folder = game / 'BepInEx' / subdir
        safe_parents(folder)
        if not folder.is_dir() or any(p.name not in allowed for p in folder.iterdir()):
            raise Stop('Unexpected files in BepInEx/' + subdir + '; keep this test installation isolated.')
    mods = game / 'Mods'
    if mods.is_symlink() or not mods.is_dir():
        raise Stop('Mods folder is missing or is a symbolic link.')
    allowed = {'alex_deception_m0.polymod', '.DS_Store'}
    if any(p.name not in allowed for p in mods.iterdir()):
        raise Stop('Use this test setup with only Deception; move other mods outside Mods yourself.')
    mod = mods / 'alex_deception_m0.polymod'
    if mod.exists() or mod.is_symlink():
        found = digest(regular(mod))
        if not allow_mod_replacement and found != manifest['files']['mod/alex_deception_m0.polymod']:
            raise different_mod(mod, found)
    return targets['launcher'] / 'launch-polytopia.sh'

def update_deception(game, home, manifest):
    verify_installed(game, home, manifest, allow_mod_replacement=True)
    source = KIT / 'mod/alex_deception_m0.polymod'
    expected = manifest['files']['mod/alex_deception_m0.polymod']
    if digest(regular(source)) != expected:
        raise Stop('Replacement mod changed after package verification.')
    mod = game / 'Mods/alex_deception_m0.polymod'
    if not mod.exists():
        exclusive_copy(source, mod)
    elif digest(regular(mod)) != expected:
        previous = digest(regular(mod))
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(prefix='.deception-update-', suffix='.tmp', dir=mod.parent, delete=False) as staged:
                temporary = Path(staged.name)
                with source.open('rb') as src:
                    shutil.copyfileobj(src, staged)
            temporary.chmod(source.stat().st_mode & 0o777)
            if digest(temporary) != expected or digest(regular(mod)) != previous:
                raise Stop('Mod changed while preparing the update.')
            os.replace(temporary, mod)
        finally:
            if temporary is not None and temporary.exists():
                temporary.unlink()
    if digest(regular(mod)) != expected:
        raise Stop('Installed mod verification failed.')
    print('DECEPTION ' + MOD_VERSION + ' ENABLED. Both players must use this exact package and a fresh match.')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['check', 'install', 'enable-deception', 'update-deception', 'launch'])
    parser.add_argument('--game', type=Path, default=Path.home() / 'Library/Application Support/Steam/steamapps/common/The Battle of Polytopia')
    args = parser.parse_args()
    game = args.game.expanduser().absolute()
    home = Path.home()
    manifest = verify_package()
    verify_game(game)
    closed(include_steam=args.command == 'install')
    if args.command == 'install':
        install(game, home, manifest)
    elif args.command == 'check' and not (game / RECEIPT).exists():
        verify_fresh(game, home)
        print('READY TO INSTALL: package checksums, exact game build and Intel/Rosetta checks passed. No files changed.')
    else:
        launcher = verify_installed(game, home, manifest, allow_mod_replacement=args.command == 'update-deception')
        # Keep the audited launcher's own native/config guards as the final gate.
        run(['/bin/bash', str(launcher), 'check', '--game', str(game)])
        if args.command == 'enable-deception':
            mod = game / 'Mods/alex_deception_m0.polymod'
            if not mod.exists():
                exclusive_copy(KIT / 'mod/alex_deception_m0.polymod', mod)
            print('DECEPTION ' + MOD_VERSION + ' ENABLED. Start a fresh match for testing.')
        elif args.command == 'update-deception':
            update_deception(game, home, manifest)
        elif args.command == 'launch':
            return subprocess.call(['/bin/bash', str(launcher), 'launch', '--game', str(game)])
        else:
            print('INSTALLED CHECK PASSED. No game launched.')
    return 0

if __name__ == '__main__':
    try:
        sys.exit(main())
    except (Stop, OSError, ValueError) as e:
        print('STOP: ' + str(e), file=sys.stderr)
        print('Do not retry with sudo or replace libraries. Send the complete message to Alex.', file=sys.stderr)
        sys.exit(1)
