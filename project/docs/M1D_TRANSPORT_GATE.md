# Identity-safe transport — next integration gate

Transport is deliberately not enabled in M1D. No genuine Mimic Boat registry/type mapping, reconstruction or native persistence implementation is claimed.

Inspected signatures in GameLogicAssembly.dll include `ActionUtils.WillUnitEmbark(UnitState,TileData,GameState)`, native `EmbarkAction.ExecuteDefault`, `DisembarkAction.ExecuteDefault`, `UpgradeAction.ExecuteDefault`, and UnitState accessors for `id`, `passengerUnit`, `home`, `health`, `moved`, `attacked`, `previousTurnEndCoordinates`, `createdTurn`. The wrappers marshal into native game code and do not establish its ordering. UnitState has no supplied custom persistent-data slot. Do not restore a nonexistent `embarksTo` registry field, substitute apparent UnitData during an authoritative move, or repair HP/type only after native callbacks already ran.

Before enabling boarding, establish from the pinned Mac build:

1. The vessel-selection and passenger-reconstruction hooks, including whether native logical IDs survive, and every callback that can observe an intermediate genuine vessel.
2. Actual Mimic body/passenger identity and 10 stored HP throughout boarding/disembarkation; actual attack/retaliation remains unavailable before, during and after callbacks.
3. One native move/action history, owner, home/support slot, actual paid investment and acquisition history across native reconstruction. Rejected moves must change none of them.
4. Independent apparent vessel and passenger mapping: ordinary transportable units → Raft (`transportship`), Giant appearance → Juggernaut, Cloak appearance → Dinghy (`cloak_boat`); native swimmers/flyers/special fleets use their own real rules and receive no invented passenger.
5. Apparent HP/wounds and passenger conversion follow the represented native form independently. No cost/refund/weapon gain from an apparent Scout/Rammer/Bomber upgrade; Mimic Boat cannot buy a genuine warship.
6. Save/load and replay serialization must preserve stable identity, cargo, investment, health and one action budget. A pointer-only cache is insufficient. Keep cross-version migration unsupported until specifically implemented.

Related requirements: FINAL_v1_5 §§7/8/15, DEC-051–075, A01 all-tribe scope, UI-A04. Native Cloak concealment/infiltration is a separate deferred adapter.
