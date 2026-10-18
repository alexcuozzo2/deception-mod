# Rogue hover calculator — 0.3.107

The native hover calculator returns early when the selected and hovered units return the same Owner object. Rogues use their commander as native owner, so Rogue-to-friendly previews never reached BattleHelpers. This is a display gate, not the actual attack restriction.

Inside the existing validated synchronous hover scope, the second owner comparison receives a detached PlayerState with unchanged fields when either exact same-controller body is an active Rogue. The exception is consumed once, before computation. It changes no native owner, player ID, diplomacy, unit statistics or action flags. The native calculator and existing apparent-form projection still produce the result. Ordinary same-owner pairs remain suppressed.

The fixture now reproduces the native owner-pointer comparison. 87 preview cases and 136 assertions pass, including Rogue-to-friendly, reverse, Rogue-to-Rogue, Rogue-to-enemy, ordinary allies, hidden/replay/wrong-viewer/stale/closed guards, and unchanged actual ownership after the scope. Native gameplay testing remains separate.

Mac 0.3.106 refactor playtesting was reported successful by Alex. Test 0.3.107 on Mac and Windows using the same shared artifact; check both damage directions against a friendly unit while commanding a Rogue.
