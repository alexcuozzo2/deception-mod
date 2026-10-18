#!/usr/bin/env python3
"""Explicit, exact paired PolyMod preview + Deception install for A38.
No launch, network, dependency change or save access. This is not manage.py's
ordinary own-mod installer: Alex explicitly requested the supplied loader.
"""
import argparse,importlib.util,json,os,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('preview_manage',ROOT/'tools/manage.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
OLD='db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d'
SUPPLIED='a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016'
REL='BepInEx/plugins/PolyMod.dll'
OUT=ROOT.parent/'run-output/full-branch-implementation/candidate-0378'

def ordinary_parents(path):
    for p in (path,*path.parents):
        if p.is_symlink():raise m.Stop('Refusing linked install/backup path: '+str(p))

def publish(source,destination,expected_old):
    ordinary_parents(destination)
    if m.sha(m.regular(destination))!=expected_old:raise m.Stop('Destination changed; refusing replacement.')
    fd,name=tempfile.mkstemp(prefix='.polymod-reviewed-',dir=destination.parent)
    temporary=Path(name)
    try:
        with os.fdopen(fd,'wb') as f:f.write(source.read_bytes());f.flush();os.fsync(f.fileno())
        if m.sha(temporary)!=m.sha(source):raise m.Stop('Staged DLL integrity failed.')
        if m.sha(m.regular(destination))!=expected_old:raise m.Stop('Destination changed during preparation.')
        os.replace(temporary,destination)
        if m.sha(destination)!=m.sha(source):raise m.Stop('Installed DLL integrity failed.')
    finally:temporary.unlink(missing_ok=True)

def run(commit=False):
    game=m.GAME;folder=OUT/'polymod-preview';staged=folder/'staged-game'
    original=folder/'preview.dll';adapted=folder/'adapted/PolyMod.dll'
    if m.sha(m.regular(original))!=SUPPLIED:raise m.Stop('Supplied preview differs.')
    wanted=m.CONTRACT['framework_checksums'][REL]
    adaptation=json.loads(m.regular(folder/'adapted/receipt.json').read_text())
    if not adaptation.get('passed') or adaptation.get('inputSha256')!=SUPPLIED or adaptation.get('outputSha256')!=wanted:raise m.Stop('Adaptation receipt differs.')
    for path,digest in adaptation['toolSources'].items():
        if m.sha(m.regular(ROOT.parent/path))!=digest:raise m.Stop('Adaptation tool source changed: '+path)
    if wanted in (OLD,SUPPLIED) or m.sha(m.regular(adapted))!=wanted:raise m.Stop('No reviewed adapted preview contract.')
    receipt=json.loads(m.regular(ROOT/'build/latest.json').read_text())
    if receipt['sources']!=m.same_tree_sources():raise m.Stop('Stale candidate sources.')
    if receipt['framework_and_config']!=m.preflight(staged):raise m.Stop('Staged candidate/loader contract differs.')
    # Exercise the unchanged ordinary full installer and all receipt gates in
    # the disposable staging tree before changing either live package.
    m.install(staged)
    baseline=json.loads(m.regular(OUT/'preinstall-baseline.json').read_text())
    for path,expected in baseline['framework'].items():
        if m.sha(m.regular(Path(path)))!=expected:raise m.Stop('Live baseline changed: '+path)
    if m.sha(m.regular(game/REL))!=OLD:raise m.Stop('Unrecognized live PolyMod; nothing replaced.')
    if m.sha(m.regular(game/'Mods'/m.MODNAME))!=baseline['archive_sha256']:raise m.Stop('Live mod baseline changed.')
    if m.sha(m.regular(Path(baseline['launcher_path'])))!=baseline['launcher_sha256']:raise m.Stop('Launcher changed.')
    r=subprocess.run(['/usr/bin/pgrep','-u',str(os.getuid()),'-x','Polytopia'],capture_output=True)
    if r.returncode!=1:raise m.Stop('Polytopia must be confirmed closed.')
    record={'status':'REVIEWED','original_preview_sha256':SUPPLIED,'adapted_preview_sha256':wanted,'previous_loader_sha256':OLD,'mod_sha256':receipt['artifact_sha256'],'game_launched':False,'other_framework_files_changed':False}
    if not commit:
        (OUT/'preview-install-review.json').write_text(json.dumps(record,indent=2)+'\n');print(json.dumps(record));return
    backup_root=game/'Disabled Mods/PolyMod backups';ordinary_parents(backup_root);backup_root.mkdir(parents=True,exist_ok=True)
    backup=backup_root/('PolyMod-1.2.18-'+OLD[:16]+'.dll');ordinary_parents(backup)
    if not backup.exists():
        with backup.open('xb') as f:f.write((game/REL).read_bytes());f.flush();os.fsync(f.fileno())
    if m.sha(m.regular(backup))!=OLD:raise m.Stop('Existing loader backup differs.')
    publish(adapted,game/REL,OLD)
    try:
        m.preflight(game)
        # Keep the established full mod installer as the installation authority.
        subprocess.run(['bash',str(ROOT/'run.sh'),'install'],check=True)
    except BaseException:
        # Restore only the exact files this operation could have installed.
        mod=game/'Mods'/m.MODNAME
        if m.sha(m.regular(mod))==receipt['artifact_sha256']:
            oldmod=OUT/'baseline-0377.polymod'
            if m.sha(m.regular(oldmod))!=baseline['archive_sha256']:raise m.Stop('Rollback mod backup differs.')
            publish(oldmod,mod,receipt['artifact_sha256'])
        if m.sha(m.regular(game/REL))==wanted:publish(backup,game/REL,wanted)
        raise
    record.update(status='INSTALLED',backup=str(backup),mod=str(game/'Mods'/m.MODNAME),loader=str(game/REL))
    (OUT/'preview-install-receipt.json').write_text(json.dumps(record,indent=2)+'\n');print(json.dumps(record,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--install',action='store_true');args=parser.parse_args()
    try:run(args.install)
    except (m.Stop,subprocess.CalledProcessError) as e:raise SystemExit('STOP: '+str(e))
