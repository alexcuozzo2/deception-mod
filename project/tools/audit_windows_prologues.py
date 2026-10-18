"""Read-only conservative hook review. Requires capstone and pefile.

Uses on-disk game instructions, never already-detoured process bytes. Output is
private diagnostic data. A clean row is NOT runtime compatibility certification.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re

import capstone
import pefile

GAME_SHA = 'bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926'


def inspect(code, address, width=6):
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    decoder.detail = True
    consumed, hazards = 0, []
    for instruction in decoder.disasm(code, address):
        raw = instruction.bytes
        # These are the three known broken relocation families in stock Dobby.
        opcode = instruction.opcode[0]
        if 0x70 <= opcode <= 0x7f or opcode == 0xeb or (
                opcode == 0x0f and 0x80 <= instruction.opcode[1] <= 0x8f):
            hazards.append('relative-branch:' + instruction.mnemonic)
        if instruction.group(capstone.CS_GRP_RET):
            hazards.append('return-in-overwrite-window')
        if opcode in (0xe0, 0xe1, 0xe2, 0xe3):
            hazards.append('loop-family-review')
        consumed += len(raw)
        if consumed >= width:
            break
    if consumed < width:
        hazards.append('incomplete-decode')
    return consumed, hazards


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', type=Path, required=True)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    payload = args.game.read_bytes()
    if hashlib.sha256(payload).hexdigest() != GAME_SHA:
        raise SystemExit('Unreviewed game image; refusing to apply this profile')
    inventory = json.loads(args.inventory.read_text(encoding='utf-8-sig'))
    if inventory.get('status') != 'POINTER_INVENTORY_COMPLETE':
        raise SystemExit('Incomplete pointer inventory')
    methods = inventory.get('all_methods', [])
    if not methods:
        raise SystemExit('Full all_methods inventory required')
    refs = {}
    for file in args.source.glob('*.cs'):
        # Deliberately includes ordinary calls as well as hooks; dynamic targets
        # and helper-owned hooks require manual review, listed as limitations.
        for match in re.finditer(r'Native\.M\(\s*"([^"]+)"\s*,\s*"([^"]+)"', file.read_text(encoding='utf-8-sig')):
            refs.setdefault('.'.join(match.groups()), set()).add(file.name)
    pe = pefile.PE(data=payload)
    rows, found = [], set()
    for method in methods:
        name = method['method']
        if name not in refs:
            continue
        found.add(name)
        rva = method['rva']
        size, hazards = inspect(pe.get_data(rva, 32), rva)
        rows.append(dict(method=name, signature=method['signature'], rva=rva,
                         overwrite_bytes=size, hazards=hazards, sources=sorted(refs[name])))
    report = dict(game_sha256=GAME_SHA, overwrite_minimum=6, rows=rows,
                  unresolved=sorted(set(refs)-found), gameplay_verified=False,
                  limitations=['Literal Native.M references are a conservative superset, not an installed-hook census.',
                               'Dynamic targets, native helper hooks and Unity methods require separate coverage.',
                               'No branch hazard does not prove relocation or ABI correctness.'])
    args.output.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(dict(resolved=len(rows), unresolved=len(report['unresolved']),
                          flagged=sum(bool(r['hazards']) for r in rows))))


if __name__ == '__main__':
    main()
