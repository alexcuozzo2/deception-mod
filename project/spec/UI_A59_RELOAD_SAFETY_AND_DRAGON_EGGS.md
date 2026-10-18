# UI-A59 — Reload Safety and Dragon Eggs (0.3.96)

## Match reopen crash

Alex reports a crash reopening a match after returning to the menu. Preserved logs in `run-output/resume-crash-0396/initial-logs` show successful framed online admission followed by a NullReferenceException in `MapRendererInner.ReRenderAllTiles`, called by `MapRenderer.Refresh`. This differs from A58's resignation error.

The native renderer publishes `Current` before asynchronous level preparation finishes. A full refresh assumes its tile array and entries already exist. Blackout UI previously set `shouldRefresh` on the first context observation even with no Blackout tiles. Native disassembly confirms this is unsafe before successful map rendering; the log alone does not prove the precise null member at the incident.

- Request Blackout refresh only after successful native `MapRenderer.RenderMap(MapData)` completion for the current renderer and current state's map.
- Baseline existing masks at completion; normal initial rendering already uses native exploration plus Blackout. No redundant first-observation refresh.
- Invalidate readiness on render start, clear, destruction and context replacement. A live Unity object or nonempty allocated array alone is not completion evidence.
- Preserve refreshes for genuine Blackout changes and viewing-player changes after readiness, including a cast between render completion and the first Update.
- Keep native fog clicks, covered-selection cleanup and ordinary rendering intact. Do not swallow native renderer exceptions or alter saves.

## Elyrion recruitment

Alex requests genuine Dragon Egg recruitment for **5 stars**, available **from the start** (confirmed). Use native egg identity and normal city training, ownership, support, cost, action timing, growth, persistence and command synchronization. Other tribes do not receive the recruit option. Preserve free city reward spawning and actual-paid investment accounting. No new unit identifiers or artificial save fields.

## Release scope and acceptance

Include A58 resignation failure handling. Exact online compatibility admission remains required; the underlying new-game contract mismatch is unresolved. User owns native playtesting; validate source and offline fixtures, install with game closed and no old-mod backup, and provide a combined Mac installer / Windows preparation package. Do not claim observed native reopen, online resignation or Windows acceptance.
