# Native-facing API audit — M0.2

The pinned working framework contract remains byte-for-byte unchanged from M0.1.
Only one managed PolyMod prefix is now installed:

`PolyMod.Loader.ProcessGameLogicData(Polytopia.Data.GameLogicData, Newtonsoft.Json.Linq.JObject)`.

The prior `TechView_CreateNode` prefix and all replacement roots have been removed.
The existing PolyMod layout handles the extended native Basic array. Current
research evidence shows that a custom cloned root is NOT interchangeable with the
already-owned native Basic technology: an assigned ID such as 1004 is not index 0.

The wrapper contracts used for JSON remain the previously compiled/tested M0.1
ones: native `ToString` slot via `NativeJson.Read`, `JObject.Parse(string)` and its
string `Item` indexer. M0.2 commits only a replacement `techData` token; it never
replaces the native `tribeData` token. On transaction failure it restores that one
original token.

The source mutation adds only `dcp_deception` with `idx=-1`, cost tier 1, no units
or children yet; it appends one edge to existing `techData.basic.techUnlocks`.
Native `basic.idx=0`, `cost=0`, unit/movement/improvement fields and every other
technology stay unchanged. Native starting lists and modifiers remain unchanged.

The supplied registry has no playable `basic` override and every playable tribe
starts with Basic. An unexpected root override is rejected for audit rather than
being treated as auto-researched. Native child overrides are read, not changed.

A repeated pipeline call with fresh base JSON can be applied again. An already
modified JSON object is accepted only after validating one Deception edge/node and
all native root invariants; it is not appended twice.

Primary cross-check: PolyMod's TechData documentation identifies Basic's idx=0,
techUnlocks as prerequisite edges and cost as a tier:
https://github.com/PolyModdingTeam/PolyMod/wiki/2.2-TechData
The captured game registry, not that documentation's older example roster, supplies
all values/tribes tested here. This is not evidence of M0.2 native execution.
