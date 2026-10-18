# UI-A17 — Current disguise sprites on map and menu

Authority: Alex’s 18 September 2026 test of0.3.53, including an Elyrion Mimic regrouping with a Polytaur. Clarifies current-form artwork on both surfaces; UI-A16 focus layout and all gameplay/ownership guards remain.

- Native Regroup sound is accepted. Native rear/front badge glyphs and fading are accepted, including Polytaur. Preserve these.
- Restore the two bottom portraits. The actual Mimic must be visible, alongside the current disguise. Focus controls the right/front/lower opaque portrait versus the left/back/upper faded one; names/stat inspection stay consistent.
- The copied unit’s genuine native type artwork must appear on the map and in the menu after Regroup. A Polytaur badge and15HP on a Warrior body is a failure. Test non-recruitable field forms as well as ordinary Warrior/Rider.
- Cosmetic profile records use exact native enum member names (e.g.Elyrion), which are distinct from lower-case content registry IDs. Reject invalid profile values without changing authoritative state.
- Repair the native visual lifecycle when the cached rendered UnitData is stale after an in-place UnitState form change. Preserve the exact logical/native unit identity, position, health, owner, support, investment and action flags; replace only obsolete visual objects through native lifecycle operations. Native rendering still owns fog, visibility, color, depth, status and pooling.
- No per-frame repeated destruction/recreation, no unsafe object reuse and no forced selection during command playback. The session-local actual/apparent preference remains keyed to the unit’s persistent identity.
- Accepted actual Mimic silhouette/readiness glow, Regroup eligibility/two-stage target flow and button order remain unchanged. Independent copied climate/birth/growth/concealment rendering remains a documented wider-branch gap; this increment must prove the basic copied native body type without claiming those deferred features.
- Alex controls gameplay. Keep model/fixture/native observations separate, leave game closed after installing the verified build, and ask for the next focused visual test.
