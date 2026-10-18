# M1B — M1A report review and recruitment-wheel repair

## Supplied evidence
Input: `deception-m0-report.8he6zjgy.zip`.
Installed M1A mod checksum in `build/last-install.json`:
`57e1389e2d0ad1212a3776d23b5eeeaa55121566634996bb1d159e254873b7ee`.

`deception/events.jsonl` records the native Tech cue, then repeated appearance
picker openings: initially 1 eligible form, later 2, later 8. There is no successful
recruitment-confirmation event in that log. M1A did not log the native rejection
reason when `TrainCommand.IsValid` returned false, so the exact error wording and
all branches inside that native method cannot be recovered from this report.
The user reports a closes-instead-of-next button and a not-unlocked rejection.

Source inspection establishes two defects/gaps:
- The picker redraws the same BasicPopup inside its button callback. It does not
  provide the requested persistent native wheel. This also exposes the replacement
  UI to the old button's close/release behavior; the report does not log that event
  ordering, so the exact native dismissal sequence is not claimed as traced.
- All concrete forms are `hidden=true` and absent from Deception's `unitUnlocks`.
  Only the non-spawnable category `dcp_mimic` is registered there. M1A tries to
  compensate with an IsUnlocked hook, but the user observed that native training
  still rejects the selected concrete type. The provided wrapper metadata names
  the third GetUnlockedUnits argument `shouldIncludeHidden`.

## Changes
- Register concrete 3-star/1-HP types under Deception and make them visible to
  native training validation. This does NOT grant the genuine source technologies.
- Filter effective unlock lists by own native roster plus Deception; preserve
  original unit entries using a fresh list rather than modifying a cached result.
- Keep only the Mimic category in the ordinary city wheel. Concrete appearance
  choices are shown in the second wheel, not as 26 additional city choices.
- Add a negative-only TrainCommand.IsValid guard for source-form eligibility.
  Eligible requests still run native IsValid. No prefix forces IsValid=true.
- Revalidate at selected-form confirmation and TrainCommand execution. The native
  command/action owns charging and creation. The menu-only anchor cannot execute.
- Native TrainAction validation checks form eligibility/cost, but does NOT recheck
  the pre-purchase wallet after a command could already have charged it.
- Use QuickActions and UIRoundButton with native UIUnitRenderer icons. Show two-line
  Mimic labels and cost badges; add a Back button. No Next/Choose popup exists.
- Queue transitions until after the click dispatch; keep a per-city/player/turn
  UI session, root callbacks until native buttons are cleared, reject repeated
  selections and stale contexts. Cancel/Back performs no gameplay command.
- Use native TechItem.GetUnlockItems's `onlyPickFirstItem` flag for Deception's
  one category icon. Do not mutate gameplay unlock data for presentation.
- Preserve research sound code, Basic-root algorithm, stable mod ID, and framework.

## Evidence boundaries
144 method/accessor signatures matched the supplied generated wrapper metadata.
This verifies names/types, NOT native behavior or the wheel's pixel layout.
70 Python file-level tests passed here. The 150 C# model/registry/wheel tests and
new C# adapter have NOT been compiled or executed here; the user's offline build
must pass its gates before installing. No native gameplay success is claimed.

## Native tests to perform
New disposable ordinary-tribe match; Deception researched. With only Basic units
unlocked, one Warrior appearance plus Back. Add Riding and check Rider appears.
Cancel and Back repeatedly with no spending. Confirm Warrior and Rider separately:
3 stars each, one distinct Mimic each, no not-unlocked error. Re-select a new city,
change turn, test a full/occupied city and insufficient stars, and save/reload.
Also inspect labels/costs around a full wheel. Special-tribe starter replacements
remain a separate coverage test. Do not open older M1A saves as a migration test.
