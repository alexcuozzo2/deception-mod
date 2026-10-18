"""Official loader admission for an isolated local/pass-and-play review build.
Never edits the loader or installs files. This is not runtime/gameplay proof.
"""
from pathlib import Path
from cli_metadata import CLI

VERSION = '1.2.18'
SHA256 = 'db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d'
URL = 'https://github.com/PolyModdingTeam/PolyMod/releases/tag/v1.2.18'
REL = 'BepInEx/plugins/PolyMod.dll'
CONTRACTS = (
    ('PolyMod.Loader', 'ProcessGameLogicData', 'System.Void', ['Polytopia.Data.GameLogicData','Newtonsoft.Json.Linq.JObject']),
    ('PolyMod.Loader', 'CreateMappings', 'System.Void', ['Newtonsoft.Json.Linq.JObject']),
    ('PolyMod.Managers.Visual', 'BuildSprite', 'UnityEngine.Sprite', ['System.Byte[]','System.Nullable`1<UnityEngine.Vector2>','System.Single']),
)

def admit(api, loader):
    if api.sha(api.regular(loader)) != SHA256:
        raise api.Stop('Review requires the exact unmodified official PolyMod '+VERSION+'.')
    cli = CLI(loader)
    for owner, name, result, args in CONTRACTS:
        matches = [m for m in cli.methods(owner) if m['name']==name and m['ret']==result and m['args']==args and m['flags'] & 0x10]
        if len(matches)!=1:
            raise api.Stop('Official PolyMod API contract differs: '+owner+'.'+name)
    return {'version':VERSION,'sha256':SHA256,'source':URL,'contracts_checked':len(CONTRACTS),'native_execution':'NOT RUN','online':'UNAVAILABLE'}

def configure(api, game):
    evidence=admit(api, game/REL)
    # Process-local override only; the installed-stack contract stays unchanged.
    api.CONTRACT['framework_checksums'][REL]=SHA256
    api.CONTRACT['reference_api_sha256']['PolyMod.dll']=SHA256
    return evidence

if __name__=='__main__':
    import argparse,json
    import manage as api
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game',type=Path,required=True)
    parser.add_argument('--build',action='store_true')
    args=parser.parse_args()
    args.game=args.game.expanduser().resolve()
    with api.lock():
        result=configure(api,args.game)
        api.preflight(args.game)
        print(json.dumps(result),flush=True)
        if args.build: api.build(args.game)
