#!/usr/bin/env python3
"""Assemble both platform sections from a verified Mac kit and current sources."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import stat
import zipfile

ROOT = Path(__file__).resolve().parents[1]
TEMPLATES = Path(__file__).resolve().parent
WINDOWS_SOURCES = (
    'tools/native-platform-survey.py', 'tools/native-platform-build.py',
    'src/Native/CMakeLists.txt', 'src/Native/bridge-build-contract.json',
    'src/Native/deception-platform.h', 'src/Native/deception-state-bridge.cpp',
    'src/Native/deception-execution-bridge.cpp', 'src/Native/deception-command-bridge.cpp',
    'src/Native/PORTABILITY.md', 'tests/native/state-bridge-harness.cpp',
    'tests/native/execution-bridge-harness.cpp', 'tests/native/command-bridge-harness.cpp',
)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(ok, message):
    if not ok:
        raise ValueError(message)


def checksums(entries):
    return ''.join(f'{sha(data)}  {name}\n' for name, data in sorted(entries.items())).encode()


def assemble(mac_package, receipt_path, output):
    record = json.loads(mac_package.read_text())
    receipt = json.loads(receipt_path.read_text())
    require(receipt['status'] == 'MODEL_TESTS_PASSED_NATIVE_NOT_RUN', 'Build receipt is not passed')
    require(record.get('crc_and_all_members') == 'PASS', 'Mac ZIP verification missing')
    require(record.get('installer_tests', '').startswith('PASS:'), 'Mac installer tests missing')
    require(record['mod_sha256'] == receipt['artifact_sha256'], 'Mac kit uses another mod build')
    require(sha(Path(receipt['artifact']).read_bytes()) == receipt['artifact_sha256'], 'Changed mod archive')
    for rel, digest in receipt['sources'].items():
        require(sha((ROOT / 'project' / rel).read_bytes()) == digest, f'Stale build source: {rel}')
    version = json.loads((ROOT / 'project/content/manifest.json').read_text())['version']
    require(re.fullmatch(r'\d+\.\d+\.\d+', version), 'Unexpected release version')
    source_zip = Path(record['zip'])
    require(sha(source_zip.read_bytes()) == record['sha256'], 'Mac ZIP checksum differs')
    entries, modes = {}, {}
    with zipfile.ZipFile(source_zip) as archive:
        require(archive.testzip() is None, 'Mac ZIP CRC failed')
        original = {}
        for info in archive.infolist():
            name = PurePosixPath(info.filename)
            require(not info.is_dir() and name.parts[0] == 'Deception-Mac-Setup'
                    and '..' not in name.parts and '\\' not in info.filename
                    and not name.is_absolute(), 'Unexpected Mac ZIP path')
            require(not stat.S_ISLNK(info.external_attr >> 16), 'Symlink in Mac ZIP')
            rel = str(PurePosixPath(*name.parts[1:]))
            require(rel not in original, 'Duplicate ZIP entry')
            original[rel] = archive.read(info)
            modes['Mac/Deception-Mac-Setup/' + rel] = (info.external_attr >> 16) & 0o777
        expected = original['SHA256SUMS.txt']
        require(sorted(checksums({k:v for k,v in original.items() if k != 'SHA256SUMS.txt'}).splitlines())
                == sorted(expected.splitlines()),
                'Mac member hashes or file list differ')
        require(sha(original['mod/alex_deception_m0.polymod']) == receipt['artifact_sha256'],
                'Mac mod payload differs')
        preview = original['payload/game/BepInEx/plugins/PolyMod.dll']
        require(sha(preview) == record['polymod_sha256'], 'Mac PolyMod payload differs')
        # Adjust only human-readable extraction instructions. Installer bytes stay intact.
        readme = original['START-HERE.md'].decode()
        require('$HOME/Downloads/Deception-Mac-Setup' in readme, 'Review new Mac README paths')
        readme = readme.replace('$HOME/Downloads/Deception-Mac-Setup',
                              '$HOME/Downloads/Deception-Setup/Mac/Deception-Mac-Setup')
        readme = readme.replace('4. Extract the ZIP into Downloads. Keep the folder named **Deception-Mac-Setup** and keep its contents together.',
            '4. Extract the combined ZIP into Downloads. Open **Deception-Setup → Mac → Deception-Mac-Setup** and keep its contents together. If extracted elsewhere, open Terminal in that folder instead of using the Downloads path below.')
        upgrade_heading = '## Already installed the earlier kit?\n'
        require(upgrade_heading in readme, 'Review new Mac upgrade instructions')
        readme = readme.replace(upgrade_heading, upgrade_heading +
            '\n**Easy upgrade:** quit Polytopia, then double-click **Upgrade Deception.command** in the enclosing **Mac** folder. It runs the same updater below and leaves the result visible. After success, use your usual modded shortcut to launch. Keep the extracted folders together.\n', 1)
        original['START-HERE.md'] = readme.encode()
        original['SHA256SUMS.txt'] = checksums({k:v for k,v in original.items() if k != 'SHA256SUMS.txt'})
        entries.update({'Mac/Deception-Mac-Setup/' + k:v for k,v in original.items()})
    entries['Mac/Upgrade Deception.command'] = (TEMPLATES / 'MAC-UPGRADE.command').read_bytes()
    modes['Mac/Upgrade Deception.command'] = 0o755
    for rel in WINDOWS_SOURCES:
        data = (ROOT / 'project' / rel).read_bytes()
        require(sha(data) == receipt['sources'][rel], f'Windows source missing from receipt: {rel}')
        entries['Windows/project/' + rel] = data
    for source, dest in [('START-HERE.txt', 'START-HERE.txt'),
                         ('WINDOWS-START-HERE.txt', 'Windows/START-HERE.txt')]:
        entries[dest] = (TEMPLATES / source).read_text().replace('@VERSION@', version).encode()
    entries['Windows/source-manifest.json'] = (json.dumps({
        'purpose': f'Deception {version} Windows preparation; not an installable mod',
        'runtimeAdmitted': False,
        'files': {k.removeprefix('Windows/'): sha(v) for k,v in entries.items()
                  if k.startswith('Windows/project/')},
    }, indent=2) + '\n').encode()
    manifest = {
        'version': version, 'mod_sha256': receipt['artifact_sha256'],
        'mac_upgrade': 'Mac/Upgrade Deception.command',
        'mac_source_zip_sha256': record['sha256'], 'mac_polymod_sha256': record['polymod_sha256'],
        'platforms': {'Mac': 'installer included; recipient and online acceptance pending',
                      'Windows': 'preparation only; no admitted native game profile or runtime bundle'},
        'online': 'two-Mac/server acceptance NOT RUN; Windows/Mac compatibility NOT IMPLEMENTED',
    }
    entries['package-manifest.json'] = (json.dumps(manifest, indent=2) + '\n').encode()
    entries['SHA256SUMS.txt'] = checksums(entries)
    output.mkdir(parents=True, exist_ok=False)
    expanded = output / 'Deception-Setup'
    zip_path = output / f'Deception-Setup-{version}.zip'
    with zipfile.ZipFile(zip_path, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for rel, data in sorted(entries.items()):
            path = expanded / rel
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            mode = modes.get(rel, 0o644)
            path.chmod(mode)
            info = zipfile.ZipInfo('Deception-Setup/' + rel, date_time=(2026, 1, 1, 0, 0, 0))
            info.create_system = 3
            info.external_attr = (stat.S_IFREG | mode) << 16
            archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=6)
    with zipfile.ZipFile(zip_path) as archive:
        require(archive.testzip() is None, 'Combined ZIP CRC failed')
        require(len(archive.namelist()) == len(entries), 'Combined ZIP count differs')
        for rel, data in entries.items():
            require(archive.read('Deception-Setup/' + rel) == data, f'Combined ZIP differs: {rel}')
    manifest.update({'zip': str(zip_path.resolve()), 'sha256': sha(zip_path.read_bytes()),
                     'bytes': zip_path.stat().st_size, 'files': len(entries),
                     'crc_and_all_members': 'PASS', 'mac_installer_tests': record['installer_tests'],
                     'build_receipt': str(receipt_path.resolve()),
                     'frozen_source_count': len(receipt['sources'])})
    (output / 'PACKAGE.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mac-package', required=True, type=Path)
    parser.add_argument('--receipt', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    assemble(args.mac_package, args.receipt, args.output)
