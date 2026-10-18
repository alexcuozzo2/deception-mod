# M1C — two M1B UI defects

## Inputs inspected

- User report: `deception-m0-report.hbg493h_(2).zip`.
- Its installed mod SHA-256: `9ebbb99a905820a0665e34598ff182f88947f81790712703299eb9d5a282fa55`.
- Source: the previously delivered `deception-m1b.zip`.
- Previously supplied generated `PolytopiaAssembly.dll`, `GameLogicAssembly.dll`
  and `Il2Cppmscorlib.dll`, pinned by the existing API contract.

The user's observation is that the technology popup exposes every Mimic form,
and selecting Mimic closes the city wheel without opening the appearance wheel.
This report contains text/log evidence, not a screenshot of either interface.

## 1. Wheel failure is identified in the report

In `deception/events.jsonl`, each of the three Mimic clicks follows:

```text
mimic-wheel-queued
mimic-wheel-error:
  System.NullReferenceException
  at QuickActions.set_Target(UnityEngine.Transform value)
  at Deception.PolyModAdapter.RecruitmentWheelUi.Draw(Session p)
  at Deception.PolyModAdapter.RecruitmentWheelUi.Tick()
mimic-wheel-closed: UI operation failed
```

The attempts are at 18:51:53, 18:51:55 and 18:51:56 +05:30 on 14 September 2026.
No `mimic-wheel-option`, `mimic-wheel-open` or recruitment-confirmation event is
recorded. The deferred callback runs: this is not merely a queue that never fires.
The exception occurs before the first appearance button is made.

M1B Draw snapshots Target only after the deferred delay, calls Hide/ClearButtons,
then unconditionally calls `QuickActions.set_Target` with the snapshot. Target is
a nullable native Transform reference. The log does not print its value or the
internal object that was null, so an actual null argument is an explanation
consistent with the source, not a measured fact.

M1C removes that nullable setter from the transition. It captures the existing
city wheel's world Position in InterceptSpawn, before the deferred transition,
then restores Position and sets KeepWorldPosition=true after clearing buttons.
The submenu belongs to a stationary city, so it need not follow a unit Transform.
Native QuickActions still performs layout, show/hide and world-position tracking.
Existing context, double-click, Back, cancel and confirmation gates are retained.

New event: `mimic-wheel-position`. Existing open/error/option events remain.

## 2. Popup display was not covered by the node-icon filter

M1B's only compact-preview hook targets `TechItem.GetUnlockItems` and changes
`onlyPickFirstItem`. It does not hook `TechPopupContent.CreateTechDataContent` or
legacy `TechPopup.SetTechData`. The generated wrappers expose these as separate
entry points. The authoritative Deception registry contains 27 unit unlocks:
the menu category plus 26 concrete forms. That is required for the existing
native training checks but should not be rendered as 27 separate tech rewards.

M1C covers all three display entry points. For Deception only, it shallow-clones
the **native IL2CPP TechData object**, replaces the clone's unitUnlocks with a new
list containing only dcp_mimic, and passes that clone to the display operation.
Selection is by ID, not by assuming the category occupies the first list slot.
The native clone method is selected specifically from Il2CppSystem.Object's
own declaration; System.Object.MemberwiseClone on a wrapper would be wrong.
A same-native-pointer check rejects accidental aliasing before assigning a list.

The live registry is never swapped or edited for display. Concrete forms remain
unlocked by Deception and still require the player's own eligible native roster.
Tech IDs, costs, source UnitData, other unlock lists and other technologies are
preserved. The modern content builder is not the research transaction handler.

New events: `mimic-tech-preview`, `mimic-tech-preview-error`, with view name.

## Change boundaries

All Core source files, RecruitmentHooks.cs, ResearchAudio.cs and NativeApi.cs
are byte-for-byte unchanged from M1B. Only UI implementation, release identifiers,
build/test/report wiring, tests and documentation change. No new gameplay forms,
no research/payment bypasses and no Mac framework patches are introduced.

## Evidence and remaining limits

The supplied wrappers validate 149 method/accessor signatures, including the new
position and native clone APIs. Metadata tells us the signatures, not the native
behavior. The current environment has no usable .NET runtime/compiler; C# code
has NOT been executed here. The build must execute 150 model tests and 24 tests
using the production managed UI source with explicitly synthetic native objects
on the user's bundled runtime, then compile the actual adapter.

Those fixtures deliberately reproduce a null-Target failure and a clear that
resets placement, then exercise the corrected Draw/Back/Choose paths. They also
exercise the actual preview filter, no-live-mutation guard and hook registration.
They do not emulate Unity animation, Harmony detouring, native IL2CPP marshalling,
sprite rendering or actual payment. The on-Mac game test is still necessary.
