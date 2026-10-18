# A51 — Online resignation and finished-game maintenance

Alex authorized installing the prepared 0.3.89 candidate and reported a friend's resignation crash plus inability to delete finished multiplayer games.

## Resignation

The pinned PolyMod preview performs native state decoding/player resolution and live follow-up command delivery directly inside an asynchronous resignation handler. Cover these boundaries using the existing game-thread dispatcher and exact framed-session admission. Keep the preview's send lock, shadow fetch/cache, native Resign command, server upload and response semantics. Never hold thread-local authority across asynchronous waits; preparation and live application are separate synchronous operations. No network request may report success before upload succeeds. Do not deliver to a different active match or partially deliver malformed follow-ups. Native loader bytes and online compatibility checks remain unchanged.

The local logs show successful local resignations and no conclusive friend crash stack. Finding unsafe code paths is evidence for the change, not confirmation of the friend's exact crash cause. Actual online regression acceptance remains pending a disposable matching-build match.

## Finished games

Inspect the actual native deletion branch and preview backend routing. Do not remove compatibility checks from launch/resume merely because the summary is terminal. Do not silently hide active matches, delete saves, resign online matches or pretend the server accepted removal. Fix only a supported and verified client-side defect; document any backend limitation or missing reproduction evidence.

## Release

Complete focused offline tests and native metadata gates, then the normal guarded full check/build/install with exact 0.3.88 backup. Preserve framework, launcher and save signatures. Combine Mac and Windows sections in the sharing ZIP with the existing guarded Mac upgrade shortcut; Windows remains preparation-only.
