# M1D reviewed source candidate

## Baseline and authority

Started from the unchanged M1C source in the supplied handoff. Initial verification: 144 exported files and all 56 recorded source inputs matched. The installed M1C hash is preserved in `evidence/m1c-baseline.json` and the installer. `ResearchAudio.cs`, `Core/TechTree.cs`, `Core/RecruitmentCatalog.cs`, and `TechPresentation.cs` remain byte-identical to M1C. The wheel keeps its existing delayed transition, captured city position, native confirmation and payment path; its new Back call is presentation only.

Latest UI-A04 supersedes the old Actual-first rule for Mimics. Normal display starts apparent. The second intentional selection inspects actual identity privately; the third returns apparent. A new unit, viewer, turn, world, or loss of authorization resets inspection. Command mode and the real action allowance are separate. The true-form widget is the private inspection popup/information panel; the battlefield model stays apparent. No final Mimic artwork was supplied, so the candidate uses a deliberate text placeholder.

## Implemented increment

- `Core/State.cs:Selection` exposes `InspectionMode` separately from command `Mode`; `Core/Presentation.cs:MimicInspectionSession` holds local native-adapter selection context.
- `PolyMod/NativeApi.cs:DetachedClone` invokes the native IL2CPP Object clone and checks distinct native pointers before display changes. A shallow managed-wrapper copy is explicitly rejected.
- `PolyMod/MimicPresentationState.cs:TryProject` resolves the existing custom form, current authoritative native unit membership, and independent display HP. Actual HP must remain 10 native units. The current native disguise is immutable after recruitment: it can be reconstructed from persisted custom type on load. Arbitrary future apparent injuries/feints need a native persistence adapter before those mechanics are exposed; no experience/effect/action field is repurposed.
- `PolyMod/MimicPresentationUi.cs` supplies detached state/data to health and information widgets and confines true inspection to the local UI. Native rendering/color, popup order, dispatch and all observation modes remain Mac test gates.
- `PolyMod/MimicControls.cs` provides explicit text controls for the Mimic category and Back, with pooled-widget restoration. No asset name was guessed or final sprite claimed.
- `PolyMod/OrdinaryActionEligibility.cs` permits a native readiness query only while that same physical Mimic is being tested by native capture/ruins/disband predicates. Native return values, research, ownership, action timing, actual cost and refund remain authoritative. `RecruitmentHooks.NoAttack` retains its ordinary offense guard outside the narrow scope.

## Diagnosis and uncertainty

Capture/ruins/disband are not abilities removed by the prototype's ability filter. In the captured registry, Warrior keeps all of its native abilities. Native disband also requires its tribe's technology/player ability. M1C's global `UnitState.CanAttack` denial is a plausible common blocker because native noncombat predicates may use this as action readiness. Generated wrappers expose method signatures, not those native rule bodies. The candidate records bounded predicate outcomes and whether readiness was consulted so Mac playtests can confirm or reject that explanation. It never makes an unknown native rejection succeed by forcing `true`.

The source-confirmed boarding veto remains. Removing `embarksTo` did not establish the failure mechanism: the captured registry has no such field, and the current UnitData wrapper has no matching accessor. Native EmbarkAction/DisembarkAction/UpgradeAction selection and reconstruction are opaque here. Enabling water entry without an actual identity/persistence adapter risks manufacturing a genuine vessel. See `M1D_TRANSPORT_GATE.md`.

## Independent work and integration

Three real Astra subagents investigated presentation/UI, native actions/transport, and specification/tests/ABI. The presentation agent owns the two new native UI/control files; the actions agent owns the new predicate scope; the reviewer owns model/synthetic fixture changes. One lead owns shared state, NativeApi, Entry registration/lifecycle, wheel integration, ABI manifest, build/install and packaging. No concurrent edits to those shared files were assigned.

The independent source review found and addressed stale native-unit references, popup authorization lifetime, pooled-icon restoration and the difference between CLI generic notation and CLR reflection names. A deferred inspection popup revalidates context after the native click finishes; intervening commands or busy actions cancel it. File/symbol references and remaining runtime uncertainties are recorded here instead of treating inspection or fixture results as gameplay evidence.

## Open coverage

This is a coherent first presentation/noncombat-readiness increment, not completion of the complete Deception specification. Identity-safe transport, omitted forms including Cloaks, keyboard/controller repeat inspection, real feints/exchanges/Smuggle, higher technologies, native independent-wound save serialization, global enemy-readiness masking, combat previews/AI observation, replay/multiplayer secrecy and save migration remain unfinished. Native capture/disband behavior is still a candidate correction awaiting the exact game test.
