# UI-A60 — Blackout Rediscovery and Balance (0.3.97)

## User scope

Blackout-covered tiles must behave like ordinary native fog when genuinely rediscovered, including border growth and Explorer rewards. Fogger and Spy/Spymaid recruitment costs become **5 stars**. Remove A59's added Elyrion Dragon Egg recruitment; keep native city rewards and growth.

## Discovery architecture and defects

Blackout removes native tile exploration for affected players and persists per-player masks to prevent stationary covered units/cities from immediately undoing the effect. Exact native new-discovery events grant permissions; successful native ExploreAction execution or immediate exploration clears the matching mask. Native fog rendering/clicks, meetings, animations and route behavior remain authoritative. Do not replace this with arbitrary turn scans or global fog clearing.

Exact native audit: run-output/blackout-discovery-0397/native-audit (pinned local GameLogicAssembly wrapper and native image).

- Native ScoutMoveAction represents an Explorer, with a synthetic animation ID and no UnitState. Authorize its exact queued event/state/current player, bind exploration to its native selected To tile, and use native sight range/terrain. An unrelated unit on the target tile must not control Explorer sight or ownership. Covered owned-city rewards remain eligible.
- Native ExpandCityAction queues ExploreAction children directly rather than calling ActionUtils.ExploreTile. On successful original execution, admit only newly appended native ExploreAction children for the actor within the native city's border area. The queued action must belong to the current player and exact owned city, even if the city is covered. Existing/stale/foreign/outside-area children do not gain permissions.
- RuleAreaAction already calls ExploreTile and retains that path; do not globally admit queued ExploreAction requests.
- Keep exact state/map/turn context, exception/suppressed execution cleanup, nested scope isolation, per-player permissions, saved ledger round trips and lighthouse debit/reveal protections. Rendering, loading and passive covered-unit refreshes are not new discovery.
- Retain A59 successful RenderMap completion requirement for full map refresh.

## Balance

Change only SpyCatalog.Cost and FoggerCatalog.Cost from8 to5. Spy and Spymaid remain5 HP,1 defense with existing movement/stealth/Subvert behavior. Fogger remains10 HP,1 defense,0 attack and retains flight, Dash, Scout and two Blackout shots.

Remove ElyrionEggRecruitment preprocessing/validation. Native Dragon Egg remains hidden/non-recruitable with its original catalog cost, native identity and growth schedule; Forest Magic gains no Dragon Egg unlock. City super-unit rewards remain native and existing eggs are not deleted or edited. Existing genuine paid investment history is not rewritten. Mimic field-copy variants and roster policy remain unchanged.

Trade Secrets returns to **1/2/3 stars per tier1/2/3 unowned technology per met living opponent**. Payments still stack across opponents. Equivalent tribe replacement technologies count once per opponent. Researching a slot stops every contribution from that slot; forecasts, tree labels and native turn payments share one projection.

Blackmail costs a **flat2 stars per purchase**, including repeat layers. Native eligibility, one purchase per unit per turn, city-income capacity, hostility and all existing contract rules remain unchanged. Action quotes and treasury validation use the same price. Confirm that permanent Mind Bender conversion ends every layer from every blackmailer, including after conversion back; the existing owner validation plus command-completion pruning provides this behavior. Temporary Rogue/Subvert ownership is not permanent conversion and retains contracts.

## Acceptance and distribution

Retained full source/model/UI/native metadata/compiler gates plus focused Explorer and direct queued border cases, covered-city rewards, negative permit boundaries, native Egg preservation and all custom costs. Automated fixtures are not observed gameplay. Alex performs playtesting; no automatic game launch or save changes. Install own mod only with closed game and no old-mod backup; preserve existing framework/configuration/launcher. Combined ZIP contains the Mac installer and Windows preparation-only section. Matching multiplayer versions are required; no new Windows or online validation claim.
