# UI-A58 — Resignation Error Handling (0.3.96)

Alex reports resignation crashing a newly created 0.3.95 online game. Preserved Player.log records `Online snapshot compatibility contract differs` escaping GameInfoPopup.OnResignOkClicked through TaskCompletionSource.SetException, followed by fatal shutdown. The underlying mismatch has not been attributed to an old match or peer version.

- Retain exact framed snapshot, match, mod contract, account and thread admission. Never bypass a failed compatibility check, rewrite its ledger or upload a rejected resignation.
- Complete precommit failures with the preview's native failed ServerResponse through SetResult. Native UI hides its loader and displays ErrorMessage through ShowBackendErrorPopup. Do not fault the native task for ordinary operation/validation failures.
- Compatibility rejection has a specific safe explanation and confirms no resignation was sent. Other exceptions use uncertainty-aware guidance to reopen/check status; a thrown upload does not prove the server rejected it.
- Preserve send-lock serialization, game-thread native calls, one completion attempt, scope cleanup, malformed-follow-up rejection and postcommit success despite a failed local refresh.
- Log bounded local/saved compatibility fingerprints, without game/account identifiers or hidden state. Startup logging reports the actual assembly version instead of a stale literal version.
- No server-only resignation fallback is supported by the audited preview. The cause of the reported new-game mismatch remains open; do not claim all online resignations are repaired.
- Preserve existing Fogger art and loader bytes. A59 adds the separately authorized egg recruitment and reload repair. User owns gameplay testing; use offline regressions then guarded install with game closed, no old-mod backup, and combined Mac/Windows preparation package.
