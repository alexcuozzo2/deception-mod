"""Repackage a frozen shared candidate using only a pinned published dependency kit.

No downloads, installation, loader changes or native validation are performed.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import stat
import zipfile

REPO = Path(__file__).resolve().parents[1]
BASE_SHA = 'd423fe902fd4c4875383ada25116bd7c45b4e1b6953de8ecc784fa7015b0cf0c'
ROOT = 'Deception-Setup/'
MAC = ROOT + 'Mac/Deception-Mac-Setup/'
WIN = ROOT + 'Windows/'
MAC_MOD = MAC + 'mod/alex_deception_m0.polymod'
WIN_MOD = WIN + 'payload/Mods/alex_deception_windows_candidate.polymod'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe(name):
    if not isinstance(name, str) or not name or '\\' in name or ':' in name or name.startswith('/') or any(p in ('', '.', '..') for p in name.split('/')):
        raise ValueError('Unsafe path: ' + repr(name))
    return name


def unpack(data):
    files, modes, seen = {}, {}, set()
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for info in archive.infolist():
            name = safe(info.filename.rstrip('/') if info.is_dir() else info.filename)
            folded = name.casefold()
            if folded in seen:
                raise ValueError('Duplicate ZIP path: ' + name)
            seen.add(folded)
            mode = info.external_attr >> 16
            if stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR) or stat.S_ISLNK(mode):
                raise ValueError('Non-regular ZIP entry: ' + name)
            if not info.is_dir():
                files[name] = archive.read(info)
                modes[name] = mode & 0o777 or 0o644
    for name in files:
        if any(str(parent) in files for parent in PurePosixPath(name).parents if str(parent) != '.'):
            raise ValueError('ZIP file/directory collision: ' + name)
    return files, modes


def verify_sums(files):
    lists = [n for n in files if PurePosixPath(n).name in ('SHA256SUMS', 'SHA256SUMS.txt')]
    if ROOT + 'SHA256SUMS.txt' not in lists or MAC + 'SHA256SUMS.txt' not in lists:
        raise ValueError('Missing release checksum lists')
    for name in lists:
        prefix = name.rsplit('/', 1)[0] + '/'
        seen = set()
        for line in files[name].decode().splitlines():
            match = re.fullmatch(r'([0-9a-f]{64}) [ *](.+)', line)
            if not match:
                raise ValueError('Invalid checksum row: ' + name)
            expected, relative = match.groups()
            relative = safe(relative)
            target = prefix + relative
            if target in seen or target not in files or sha(files[target]) != expected:
                raise ValueError('Checksum mismatch or duplicate: ' + target)
            seen.add(target)
        if name in (ROOT + 'SHA256SUMS.txt', MAC + 'SHA256SUMS.txt'):
            required = {n for n in files if n.startswith(prefix) and PurePosixPath(n).name != 'SHA256SUMS.txt'}
            if seen != required:
                raise ValueError('Incomplete release checksum coverage: ' + name)


def verify_manifest(files, name, prefix, scopes):
    manifest = json.loads(files[name])
    entries = manifest['files']
    if not isinstance(entries, dict) or not entries:
        raise ValueError('Empty manifest: ' + name)
    for rel, expected in entries.items():
        target = prefix + safe(rel)
        if target not in files or sha(files[target]) != expected:
            raise ValueError('Manifest checksum mismatch: ' + target)
    actual = {n[len(prefix):] for n in files if any(n.startswith(prefix + scope) for scope in scopes)}
    if actual != set(entries):
        raise ValueError('Manifest coverage mismatch: ' + name)
    return manifest


def candidate(receipt_path, repo):
    receipt = json.loads(receipt_path.read_text())
    if receipt.get('status') != 'SHARED_CANDIDATE_BUILT':
        raise ValueError('Receipt is not a shared candidate build')
    version = receipt['version']
    if not re.fullmatch(r'\d+\.\d+\.\d+', version) or not re.fullmatch(r'[0-9a-f]{7,40}', receipt['source_commit']):
        raise ValueError('Invalid version/source commit')
    if receipt.get('source_dirty') is not False:
        raise ValueError('Candidate source commit must be clean')
    sources = receipt['sources']
    if not isinstance(sources, dict) or not sources:
        raise ValueError('Missing frozen source hashes')
    project = repo / 'project'
    current = {}
    for directory in ('src', 'tests', 'tools', 'content', 'spec'):
        for path in (project / directory).rglob('*'):
            if path.is_file() and '__pycache__' not in path.parts and path.suffix != '.pyc':
                current[path.relative_to(project).as_posix()] = sha(path.read_bytes())
    for name in ('run.sh', 'run.ps1'):
        if (project / name).is_file():
            current[name] = sha((project / name).read_bytes())
    if current != sources:
        raise ValueError('Stale source inventory or hashes')
    for rel, expected in sources.items():
        path = project / safe(rel)
        if not path.resolve().is_relative_to(repo.resolve()) or path.is_symlink() or sha(path.read_bytes()) != expected:
            raise ValueError('Stale source: ' + rel)
    artifact = Path(receipt['artifact'])
    if not artifact.is_absolute() or artifact.is_symlink():
        raise ValueError('Artifact must be an absolute regular file')
    data = artifact.read_bytes()
    if sha(data) != receipt['artifact_sha256']:
        raise ValueError('Candidate checksum mismatch')
    contents, _ = unpack(data)
    manifest = json.loads(contents['manifest.json'])
    if manifest.get('version') != version:
        raise ValueError('Candidate manifest version mismatch')
    return receipt, data


def encoded(value):
    return (json.dumps(value, indent=2) + '\n').encode()


def refresh_sums(files):
    # Inner lists first, so parent manifests/checksums can authenticate them.
    names = [n for n in files if PurePosixPath(n).name in ('SHA256SUMS', 'SHA256SUMS.txt')]
    for name in sorted(names, key=lambda n: n.count('/'), reverse=True):
        prefix = name.rsplit('/', 1)[0] + '/'
        if name in (ROOT + 'SHA256SUMS.txt', MAC + 'SHA256SUMS.txt'):
            targets = sorted(n for n in files if n.startswith(prefix) and PurePosixPath(n).name != 'SHA256SUMS.txt')
        else:
            targets = [prefix + re.fullmatch(r'([0-9a-f]{64}) [ *](.+)', line).group(2) for line in files[name].decode().splitlines()]
        files[name] = ''.join(f'{sha(files[n])}  {n[len(prefix):]}\n' for n in targets).encode()


def build(base_zip, receipt_path, output, repo=REPO):
    packaging_paths = ['distribution/repackage_shared_release.py', 'distribution/mac-template/setup.py',
                       'distribution/mac-template/setup.sh', 'distribution/MAC-UPGRADE.command',
                       'distribution/Install-Windows.ps1', 'distribution/SHARED-SETUP.md']
    packaging_hashes = {p: sha((repo / p).read_bytes()) for p in packaging_paths}
    base = base_zip.read_bytes()
    if sha(base) != BASE_SHA:
        raise ValueError('Prior published ZIP hash differs from pinned release')
    files, modes = unpack(base)
    if any(not n.startswith(ROOT) for n in files):
        raise ValueError('Unexpected top-level ZIP path')
    verify_sums(files)
    mac = verify_manifest(files, MAC + 'package-manifest.json', MAC, ('payload/', 'mod/'))
    win = verify_manifest(files, WIN + 'windows-manifest.json', WIN + 'payload/', ('',))
    receipt, mod = candidate(receipt_path, repo)
    old = json.loads(files[ROOT + 'RELEASE.json'])
    version, mod_sha = receipt['version'], sha(mod)
    if files[MAC_MOD] != files[WIN_MOD] or sha(files[MAC_MOD]) != old['modSha256']:
        raise ValueError('Prior platform candidates disagree')
    files[MAC_MOD] = files[WIN_MOD] = mod
    mac['files']['mod/alex_deception_m0.polymod'] = mod_sha
    win['files']['Mods/alex_deception_windows_candidate.polymod'] = mod_sha
    win.update(modSha256=mod_sha, version=version)
    files[MAC + 'package-manifest.json'] = encoded(mac)
    files[WIN + 'windows-manifest.json'] = encoded(win)
    for name in ('setup.py', 'setup.sh'):
        text = (repo / 'distribution/mac-template' / name).read_text()
        if name == 'setup.py':
            text, count = re.subn(r"^MOD_VERSION = '[^']+'$", f"MOD_VERSION = '{version}'", text, flags=re.M)
            if count != 1:
                raise ValueError('Missing unique installer version')
        files[MAC + name] = text.encode()
    for relative, source in [('Mac/Upgrade Deception.command', 'MAC-UPGRADE.command'), ('Windows/Install-Windows.ps1', 'Install-Windows.ps1')]:
        files[ROOT + relative] = (repo / 'distribution' / source).read_bytes()
    files[WIN + 'Install or update.cmd'] = b'@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Windows.ps1" %*\r\npause\r\n'
    docs = (repo / 'distribution/SHARED-SETUP.md').read_text().replace(old['version'], version).replace(old['modSha256'], mod_sha)
    docs = re.sub(r'Windows startup, tribe selection.*?relevant log\.',
                  'This new shared candidate has not yet passed native Windows/Mac gameplay or a real cross-platform match. Automated build checks do not establish native acceptance. Both peers must update together before testing.', docs, flags=re.S)
    docs = docs.replace('Move older Deception archives outside Mods first.', 'The updater atomically replaces Deception without making an old-mod backup.')
    files[ROOT + 'START-HERE.md'] = files[MAC + 'START-HERE.md'] = docs.encode()
    donor_readme = ROOT + 'Sources/Native-resource-donor/README.txt'
    if donor_readme in files:
        files[donor_readme] = files[donor_readme].decode().replace('Install the ' + old['version'] + ' archive', 'Install the ' + version + ' archive').encode()
    release = dict(old, version=version, sourceCommit=receipt['source_commit'], modSha256=mod_sha,
                   baseZipSha256=BASE_SHA, packagingSources=packaging_hashes, candidateReceiptSha256=sha(receipt_path.read_bytes()),
                   status='experimental; native gameplay and cross-platform match not verified')
    files[ROOT + 'RELEASE.json'] = encoded(release)
    refresh_sums(files)
    verify_sums(files)
    verify_manifest(files, MAC + 'package-manifest.json', MAC, ('payload/', 'mod/'))
    verify_manifest(files, WIN + 'windows-manifest.json', WIN + 'payload/', ('',))
    final_receipt, final_mod = candidate(receipt_path, repo)
    if final_receipt != receipt or final_mod != mod:
        raise ValueError('Candidate inputs changed during packaging')
    if packaging_hashes != {p: sha((repo / p).read_bytes()) for p in packaging_paths}:
        raise ValueError('Packaging sources changed during build')
    output.mkdir(parents=True, exist_ok=False)
    archive = output / f'Deception-Setup-{version}-windows-mac.1.zip'
    for name, data in files.items():
        dest = output / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(data)
        dest.chmod(modes.get(name, 0o644))
    with zipfile.ZipFile(archive, 'x', zipfile.ZIP_DEFLATED) as z:
        for name in sorted(files):
            info = zipfile.ZipInfo(name, (2026, 10, 17, 0, 0, 0))
            info.create_system = 3
            info.external_attr = (stat.S_IFREG | modes.get(name, 0o644)) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(info, files[name])
    (output / 'SHA256SUMS.txt').write_text(f'{sha(archive.read_bytes())}  {archive.name}\n')
    return archive


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ('base-zip', 'receipt', 'output'):
        parser.add_argument('--' + argument, type=Path, required=True)
    args = parser.parse_args()
    print(build(args.base_zip, args.receipt, args.output))
