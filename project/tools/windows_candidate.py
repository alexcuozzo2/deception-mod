"""Assemble exact native resources for an opt-in Windows/Mac test candidate."""
import importlib.util
import json
from pathlib import Path
import zipfile
from cli_metadata import CLI

GAME_SHA = 'bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926'
MAC_MOD_SHA = '98211f2a80934a5c16ebe1098a83cc91c2ca622ec56aaf4f918995640119c9cc'
BETA_SHA = '61588813d4b62d133db06fad64a2c440869aaffaf7460ec8bc8e9d82b1be63d7'


def prepare(api, game, work, native_pack, mac_mod, probe_report):
    def require(ok, why):
        if not ok: raise api.Stop(why)
    require(all(p is not None for p in (native_pack,mac_mod,probe_report)),
            'Candidate requires --native-pack, --mac-mod and --probe-report.')
    require(api.sha(game/'GameAssembly.dll') == GAME_SHA, 'Unknown Windows game image.')
    require(api.sha(game/'BepInEx/plugins/PolyMod.dll') == BETA_SHA,'Candidate requires the exact multiplayer beta.')
    require(api.sha(mac_mod) == MAC_MOD_SHA,'Unknown Mac mod resource donor.')
    spec = importlib.util.spec_from_file_location('candidate_native_build',api.ROOT/'tools/native-platform-build.py')
    native = importlib.util.module_from_spec(spec); spec.loader.exec_module(native)
    pack = native.audit_pack(native_pack)
    require(pack['rid']=='win-x64','Expected an audited win-x64 helper pack.')
    require(pack['sourceSha256']==native.inputs(),'Native pack source differs; rebuild the helpers.')
    report = json.loads(api.regular(probe_report).read_text())
    require(report.get('status')=='POINTER_INVENTORY_COMPLETE' and report.get('native_exception_probe')==0,
            'Successful in-game pointer and native exception probe required.')
    methods = {row['method']:row for row in report['methods']}
    require(len(methods)==len(report['methods'])==16,'Expected 16 unique probed methods.')
    game_image = CLI.__new__(CLI)
    # Parse only PE section mapping here, not CLI metadata in a native image.
    import struct
    data = (game/'GameAssembly.dll').read_bytes()
    pe = struct.unpack_from('<I',data,0x3c)[0]
    section_at = pe+24+struct.unpack_from('<H',data,pe+20)[0]
    game_image.sections = [struct.unpack_from('<8sIIIIIIHHI',data,section_at+i*40)
                           for i in range(struct.unpack_from('<H',data,pe+6)[0])]
    resources = []
    for kind, title, filename in (('state','State','native-state-seams.json'),
                                 ('execution','Execution','native-execution-seams.json'),
                                 ('command','Command','native-command-outer-seams.json')):
        keys = [key for key in json.loads((api.ROOT/'tools'/filename).read_text()) if key!='provenance']
        seams = {}
        for key in keys:
            row = methods[key]; raw = bytes.fromhex(row['prologue'])
            offset = game_image.off(row['rva'])
            require(len(raw)==32 and data[offset:offset+32]==raw,'Probe/file prologue mismatch: '+key)
            seams[key]=row['prologue']
        seam = work/('win-'+filename); seam.write_text(json.dumps(seams,indent=2)+'\n')
        library = native_pack/pack['artifacts'][kind]['path']
        digest = work/(kind+'.sha256'); digest.write_text(api.sha(library)+'\n')
        stem = 'Deception.Win.'+title+'Bridge'
        resources += [(library,stem+'.dll'),(digest,stem+'.sha256'),(seam,stem+'.seams.json')]
    with zipfile.ZipFile(mac_mod) as archive:
        require(archive.getinfo('Deception.dll').file_size<16*1024*1024,'Unexpected donor DLL size.')
        donor = work/'mac-resource-donor.dll'; donor.write_bytes(archive.read('Deception.dll'))
    cli = CLI(donor); b=cli.b; pe=cli.u32(0x3c); opt=pe+24
    dd=opt+(112 if cli.u16(opt)==0x20b else 96)
    header=cli.off(cli.u32(dd+14*8)); start=cli.off(cli.u32(header+24)); size=cli.u32(header+28)
    wanted = {f'Deception.{kind}Bridge.{ext}' for kind in ('State','Execution','Command') for ext in ('dylib','sha256','seams.json')}
    found={}
    for offset,flags,name,implementation in cli.rows.get(40,[]):
        name=cli.s(name)
        if name not in wanted: continue
        require(implementation==0 and 0<=offset<=size-4,'Invalid embedded donor resource.')
        length=cli.u32(start+offset)
        require(0<length<1024*1024 and offset+4+length<=size,'Donor resource outside bounds.')
        require(name not in found,'Duplicate donor resource.')
        payload=b[start+offset+4:start+offset+4+length]
        path=work/name; path.write_bytes(payload); found[name]=payload; resources.append((path,name))
    require(set(found)==wanted,'Missing donor bridge resources.')
    for kind in ('State','Execution','Command'):
        stem='Deception.'+kind+'Bridge'
        require(api.digest(found[stem+'.dylib'])==found[stem+'.sha256'].decode().strip(),'Mac helper digest differs.')
    return resources


def package(api, work, dll):
    manifest=json.loads((api.ROOT/'content/manifest.json').read_text())
    manifest['name']+=' — Mac/Windows test candidate'
    entries={'manifest.json':json.dumps(manifest,indent=2).encode(),
             'localization.json':(api.ROOT/'content/localization.json').read_bytes(),
             'Deception.dll':dll.read_bytes(),
             'DEVELOPMENT-STATUS.txt':b'SHARED TEST CANDIDATE. Exact public 1.2.18 and clean-beta profiles; public is local/pass-and-play only. Windows beta predecessor tested with stock Dobby; Windows public native acceptance pending. This exact artifact requires Mac/Windows acceptance; cross-platform multiplayer and resign remain unverified. Both peers require this exact archive. No approval marker or test flag required on the admitted Windows runtime. No loader or game libraries bundled.\n'}
    entries.update(api.content_entries())
    output=work/'alex_deception_windows_candidate.polymod'
    with zipfile.ZipFile(output,'x',zipfile.ZIP_DEFLATED) as archive:
        for name,data in entries.items():
            item=zipfile.ZipInfo(name,(2026,10,17,0,0,0)); item.compress_type=zipfile.ZIP_DEFLATED
            archive.writestr(item,data)
    return output
