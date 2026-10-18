# UI-A54 — Visibility and balance (0.3.92)

Authority: Alex's current playtest feedback and flat Trade Secrets follow-up. This amendment supersedes A52's Motherfogger price and tier-scaled Trade Secrets income.

- Motherfogger costs 8 stars, has 10 HP and 1 defense; retains zero attack, flying, 2 movement/range, Dash, Scout and two Blackout shots.
- Trade Secrets grants exactly 1 star per unowned technology known by at least one met living opponent. Multiple opponents knowing it do not multiply income. Technology-slot equivalence, privacy, viewer research and native turn settlement remain unchanged. Forecast and tree star labels use the same projection.
- Visible tier-three tech name is Counter Intelligence. Preserve its existing internal/save identifier.
- Inverted Reconnaissance art gains a diffuse white silhouette glow, with bounded cached resources and the existing icon footprint.
- A Mimic can Smuggle after movement if its action is unused. The existing authoritative validator checks attacked, not moved; Dash belongs to every Mimic carrier. Smuggle still consumes its single remaining action and 2 stars, with native harvesting research, resource, ownership, home, peace, frozen and command guards. Do not synthesize a unit click or reselect after movement.
- Blackout must not block an owned city's pending level-up choice. Permit only the current actor's actual city to reach native CityReward validation and execution; preserve fog and hidden enemy information. Native pending reward, allowed choice and action queue remain authoritative.
- Genuine native upgrades are a source of renewed vision using the resulting unit's actual sight range. A Scout upgrade may clear covered tiles it can genuinely explore; an apparent-only upgrade must not grant new sight.
- Passive disguise growth remains unable to clear Blackout. Its expected discovery-action count must match that restriction.
- Exact-preview finished-game deletion: observe the existing SetParticipationDone task, then complete native cache removal only after actual server success, for a recognized modded Ended game, unchanged account/manager/original summary/request identity. Native RemoveGameSummary owns events and counts. Never send a replacement delete request, hide server rejection, or remove active games. The observer is bounded and runs on the game thread.
- Live timer remains unresolved: native UI depends on server deadline/time bank, and the pinned preview explicitly returns a no-op response for SkipTurn. Preserve the native clock; do not manufacture a live time bank/deadline or claim timeout enforcement. Add bounded boolean-only field diagnostics for the next online test (no identifiers, timestamps or payloads). Exact-match backend success remains authoritative.

Release verification: required offline model/adapter/native metadata/build gates; Alex performs gameplay and two-client online testing. No agent game launch. Combined setup ZIP includes Mac installer/upgrader and Windows preparation-only section. Mod update overwrites the authorized own-mod archive atomically without old-mod backups; loader/game/configuration/saves remain protected. Online peers require the same build.
