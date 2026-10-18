# Declarative content review — 0.3.107

This refactor separates ordinary game data and original artwork/audio from the compiled behavior. It preserves 0.3.105 gameplay and the shared Mac/Windows native helpers. Alex reported successful Mac and Windows playtesting of the shared 0.3.107 package on 2026-10-18, including confirmation of the Rogue-to-friendly calculator fix on Mac. The tested artifact is promoted unchanged; no rebuild. These user reports are not exhaustive interaction or multiplayer validation.

## Start here

| Review area | Source | In the `.polymod` |
|---|---|---|
| All 64 custom unit rows: Mimic/Mermimic, Spy/Spymaid, Fogger and internal Mimic forms | `project/content/patch.json`, `unitData` | `patch.json` |
| Five custom technologies and unit unlocks | same, `techData` | same |
| Aquarion unit replacements | same, `tribeData` | same |
| Names and custom ability descriptions | `project/content/localization.json` | `localization.json` |
| Original PNG layers, masks and icons | `project/content/unit-art/` | `assets/unit-art/dcp_<original>__.png` |
| Original smoke PNGs and PCM sound | `project/content/spy-effects/` | `assets/spy-effects/` |
| Original Mimic portrait | `project/content/mimic-portrait.png` | `assets/dcp_mimic-portrait__.png` |
| Custom gameplay | `project/src/Core/` and `project/src/PolyMod/` | `Deception.dll` |

There are **no embedded PNG/WAV assets or embedded patch definitions in the production DLL**. Test assemblies alone embed fixture inputs so they can run independently of a game installation. Source PNG/WAV files are unchanged byte-for-byte. This is still one `.polymod` download; players do not extract its contents.

## Official conventions checked

- [Mod format](https://github.com/PolyModdingTeam/PolyMod/wiki/1.-Mod): manifest, localization, PNGs, sprites/prefabs and compiled scripts.
- [GLD patching](https://github.com/PolyModdingTeam/PolyMod/wiki/3.-GLD-patching): lowercase identifiers, normal GLD sections, `idx: -1`, array replacement.
- [UnitData](https://github.com/PolyModdingTeam/PolyMod/wiki/2.3-UnitData) and [TechData](https://github.com/PolyModdingTeam/PolyMod/wiki/2.2-TechData): ordinary unit stats/ability lists and research unlock fields.
- [PolyScripts](https://github.com/PolyModdingTeam/PolyMod/wiki/4.-PolyScripts): existing static `Load(ManualLogSource)` entry point is supported.

The wiki labels its game reference as 2.13.0. API details were also checked against public PolyMod 1.2.18 source and the supplied clean multiplayer beta. Neither loader is modified or distributed here. Existing community examples (Militas, The Ancients, French Empire) were inspected for structure; their assets/code are not copied.

## Why a small registry adapter remains

PolyMod merges `patch.json` before `ProcessGameLogicData`. Deception validates those merged definitions, reconstructs their historical declaration order, and lets its existing autoindex ordering adapter run. This preserves existing save identities. It does not enable unsafe indices or change PolyMod's counter. Reapplying a patch resets its `idx` values; already processed GLD is validated before accepted indices are restored and checked against the actual native enum mappings.

The five original native root branches retain their order; a guarded insertion keeps Deception between Fishing and Hunting. Unknown native unit metadata remains preserved when declarative mod fields are applied. Dynamic Smuggle harvest and inert command-carrier improvement rows still derive from native metadata. They are internal action transport, not playable units.

## Why some visual code remains

PolyMod loads the namespaced PNG files normally. Cropped sprites borrow the loader-owned textures: no second PNG decode and no destruction of loader textures by Deception. Presentation code still handles actual/apparent unit views, layered player colors, tribe heads, portrait fitting, outlines and visibility guards. These are not ordinary static prefab substitutions. The asset map above makes their source artwork visible without decompilation.

Stock PolyMod's image decoder copies pixels through managed wrappers, so large original PNGs may affect startup time. This refactor does not install an image-loader bypass. Native startup timing should be checked during playtesting.

Both inspected loader versions have a no-op WAV loader. The existing PCM playback adapter therefore remains, reading the loose WAV file. Its impact-time behavior is unchanged.

## Custom abilities versus ordinary ability lists

Ordinary native unit abilities (Dash, Scout, Fly and so on) are declared in `patch.json`. Custom action/state logic still implements Mimicry, Regroup, Disguise, Subvert, Blackout, Blackmail, Surveillance, Reconnaissance and Trade Secrets. Their localized labels/descriptions remain in `localization.json`. This refactor does not claim JSON alone implements those mechanics or that all native hooks have been replaced by helper libraries.

## Validation and playtest focus

Automated checks cover definition equivalence, merged-patch lifecycle and saved IDs, exact own-mod asset lookup, shared-texture ownership, and retained gameplay/UI fixtures. Build results and the candidate checksum accompany the handoff.

On both platforms, test a new match and an existing Deception save: tech tree, recruitment for ordinary and Aquarion tribes, all three unit families in map/menu/info views, Regroup/Disguise, Blackout impact sound, return to menu and reload. Keep both multiplayer peers on the same candidate. No multiplayer/resign acceptance is implied by source or fixture checks.
