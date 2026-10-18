# M1D findings

The previous overflowing technology popup and missing second recruitment wheel were corrected in M1C and are now user-reported successes. Their historical diagnosis is preserved at `history/M1C/FINDINGS.md`; do not reopen those defects without a regression observation.

M1C's native Mimic data has actual maximum health 10 stored units, which represents 1 displayed HP. Ordinary UI reads this true unit state, exposing the low health instead of the apparent form. M1D provides detached display state and data for native health and information widgets. It never writes apparent HP into the live unit, changes global health getters, or creates a new battlefield unit. The precise native color/render behavior cannot be established from generated wrappers and remains a Mac test gate.

`MimicPresentationUi.BeginRelease`, `TryDeselect` and `EndRelease` implement intentional repeat selection; `Tick` opens the authorized private popup after native click cleanup. Command dispatch, busy actions and changed selection/viewpoint invalidate pending inspection. Third selection after dismissal returns apparent information. `Core.State.Selection` keeps command mode independent.

`OrdinaryActionEligibility` addresses a plausible common blocker for capture, ruins and disband: M1C's unconditional Mimic `CanAttack` denial may prevent native noncombat readiness checks. The candidate allows the original readiness query only inside the exact same-unit native predicate, without forcing its result. The generated DLLs do not contain the native rule bodies, so the bounded predicate observations and actual in-game outcomes must confirm this explanation. Native disband also requires the tribe's normal research ability.

`MimicControls` hides the misleading Warrior/question-mark art and uses explicit MIMIC and < BACK placeholders, restoring pooled objects it changed. No final artwork was delivered.

See `M1D_REVIEW.md` for the integrated change review and `M1D_TRANSPORT_GATE.md` for the still-unresolved identity-safe transport requirements.
