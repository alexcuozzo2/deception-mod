# A52 — Intelligence branch and Motherfogger

Alex's 2026-09-27 request supersedes earlier branch/Blackout placement and Spy Blackout behavior. No automatic game launch or agent playtesting; Alex owns native acceptance.

## Technology layout

- Deception (tier 1): Mimic, unchanged.
- Propaganda (tier 2, Deception): Disinformation and Disguise.
- Espionage (tier 3, Propaganda): Spy/Spymaid and Blackmail.
- Intelligence (tier 2, Deception): Reconnaissance (enemy technology markers) and Surveillance (existing fog overlay).
- Counterintelligence (tier 3, Intelligence): Motherfogger and Trade Secrets.

The historical `dcp_camouflage` registered identity is retained internally for Counterintelligence to avoid renumbering persisted native IDs. The visible name is Counterintelligence. Fresh matches and identical new builds for online peers are required for this branch change; no silent mixed-build compatibility bypass.

## Motherfogger

User-confirmed cost 3 stars, 10 HP, 1 defense, zero attack. Flying; 2 movement, range 2, Dash, Scout, Blackout twice per turn. Retain the native Double Attack tag and legitimate DoubleAttack/DoubleReady action budget, verified against the pinned binary. The Blackout help explains the two nondamaging fog shots; do not add a duplicate two-shot tag. No damaging attack/retaliation. Existing normal capture/disband rules and paid investment apply. Recruitment remains exhausted under native timing.

Blackout's projectile, impact sound and visibility/replay rules transfer from Spies to Motherfoggers. Spies retain Subvert, Scout, cloak-like movement-earned invisibility, 5 HP/1 defense and move OR act. No Blackout for Spy/Spymaid, stale commands or disguised Mimics. Copied movement/appearance never grants real Blackout or Scout.

Uploaded fixed/team images remain source assets, with runtime geometric/tint handling. Fixed artwork includes the head; do not add a tribal head. Trade Secrets uses the original beige document and team crown; Blackmail retains the black document.

## Reconnaissance and Trade Secrets

Intelligence marks researched technologies of met living foreign players on the viewer's tree with their tribe badges. Respect private viewer/turn/lifecycle guards. Normalize native special-tribe overrides to the tree's corresponding slots. No research is granted and no unmet information exposed.

User confirmed: each technology pays once overall across met opponents. With Counterintelligence, each technology the viewer lacks but a met living opponent has contributes its native tier (1/2/3) in stars per turn. Basic/tier-zero, dead opponents and own technologies do not pay. Stop income when the viewer researches it or the last eligible opponent is gone. Top-bar income and ordinary native turn credits must agree; no double replay payments. Display income with a star on the corresponding locked tech. Per-frame refreshes must be bounded and clean up pooled UI.

## Verification and release

Offline source/model/native-boundary fixtures cover tree edges/reentry, IDs, Spy/Fogger separation, first/second/third shots, movement, frozen/newborn/exhausted, cancellation/rollback/replay, Rogue commands, serialization, tribe overrides, met/unmet and duplicate-enemy technologies. Maintain all existing full build gates. Install only with game closed, exact predecessor backup and existing loader/launcher untouched. Combined Mac/Windows ZIP includes the guarded Mac upgrade shortcut; Windows remains preparation-only.
