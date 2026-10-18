# UI-A63 — Remove Lighthouse Reward (0.3.100)

Immediately retire the lighthouse star Easter egg. Delete its physical click-input adapter and all registration/lifecycle references. No click sequence, new offer, fresh command or replay of an uncommitted request can award stars. Remove the reward amount, calculation and treasury writes from production.

Keep the stable serialized kind13 and registry ID1086 reserved so existing saved receipts/types do not shift. Retain only exact already-committed receipt recognition and silent animation completion; these are no-ops and never change currency or ledger. Reject new/uncommitted or conflicting requests. Existing saved currency and legitimate lighthouse population/exploration behavior are unchanged. No automatic deduction from existing balances.

Preserve Spy/Spymaid2 movement and all other0.3.99 changes. Update the former active-reward fixture to exercise retirement, no mutation, receipt round-trip/duplicate/conflict and presentation completion. Remove the obsolete input-adapter build recipe after deleting its production file. Keep other build and metadata gates. Install with game closed, own mod only, no new backup; preserve saves and runtime. Alex handles gameplay testing. Package Mac installer and Windows preparation-only instructions; matching online builds required.
