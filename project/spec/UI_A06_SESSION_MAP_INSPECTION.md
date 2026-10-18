# UI-A06 — session-local map inspection

## Authority and scope

Source: Alex's explicit continuation instruction after the local 0.2.11 inspection playtest, 2026-09-15. This is the current Mimic inspection amendment. It supersedes UI-A05 where the requirements below conflict: the map is included in the owner's chosen view; a toggle does not open information automatically; and selection, actions and turns do not reset the choice. Preserve UI-A05 and older specifications as historical documents. Their nonconflicting gameplay, input, popup and privacy requirements remain in force.

This document records requested behavior, not an implementation or acceptance result. The 0.2.11 screenshots establish the earlier increment and do not establish UI-A06 success. Original acceptance transcriptions must not be silently rewritten or marked passed.

## Requested behavior

1. **One chosen view for map and bottom-left presentation.** A deliberate hold on the selected controlled Mimic or its explicit bottom-left button toggles that unit between apparent disguise and actual Mimic. The owner's map rendering and bottom-left portrait/name follow the same choice. Selection alone does not toggle it. Both ready and spent Mimics remain inspectable when the relevant native interaction guards permit inspection.
2. **Information opens deliberately.** The toggle changes presentation only; it does not queue or open a stats popup after the hold or its release. The existing information icon opens the native popup with the chosen actual/apparent portrait and matching stats. Keep one native stats population and no extra bottom-left HP prose. Genuine units' ordinary native clicks, double-clicks and information behavior keep their meaning.
3. **Remember each unit's choice during this play session.** The choice is local to a logical unit and viewer. It survives selecting another unit, clearing selection, native actions and turn changes. Unit A's choice does not change unit B's choice. An authorized owner still sees A's chosen map representation while B is selected; the bottom-left panel follows B. A temporary input/command restriction must not be treated as permission to erase A's remembered choice.
4. **Reload starts apparent.** Choices are memory-only and are not written to a save, config, replay, network message, native gameplay field or persistent appearance record. Loading/reloading a match, including the same saved match, starts every Mimic in apparent mode. A new match also starts apparent. Reused unit IDs or native object pointers must not inherit a previous unit/session's choice.
5. **Owner-only disguise cue.** In apparent mode the owner's disguised Mimic has a visibly translucent map appearance so the owner can distinguish it from a genuine unit. In actual mode the owner's actual Mimic art is opaque. This is a local visual cue; it does not alter invisibility, concealment, targetability or gameplay state. Bottom-left and information portraits must remain readable and match the chosen identity.
6. **Other players see ordinary apparent presentation.** Other players receive neither actual Mimic art nor the owner's translucency cue, labels or inspection control. Their visible map and information remain ordinary apparent form and apparent data, subject to existing visibility rules. A local viewer switch must not carry another viewer's private rendering into the new view.

## State and rendering boundaries

- Keep remembered preference separate from permission to toggle, permission to render private information, and native action readiness. Existing owner/control, current-viewer, fog/visibility, targeting, command, touch/drag and popup-lifetime protections still apply to their respective operations. A selection guard required for initiating a toggle must not make the stored map preference disappear when selection changes.
- Recheck rendering authorization against the current unit and viewer. Loss of ownership or visibility must immediately suppress unauthorized private art/cues and close or restore private UI as required; retaining a memory entry never authorizes disclosure. Do not draw an overlay through fog or on a hidden/destroyed/replaced unit.
- Key memory by the current match-load session, logical unit identity and viewer identity, with protection against native object/pool reuse. Do not key it only by a reusable native pointer. Session cleanup must retire owned render objects/material instances and popup sources safely.
- Change only owned local presentation. Do not recolor shared materials, replace globally registered disguise sprites, mutate the real unit's type/health/ownership/investment/action flags, rewrite its apparent record, or use XP/effects/health fields as counterfeit persistence. No command, payment, spawn, action refresh, attack or retaliation is caused by a toggle.
- Actual Mimic health remains 1/1 and damaging attack/retaliation remain unavailable. Existing apparent health and injury rules remain; this amendment does not create a new map HP-label policy or grant apparent weapons. Existing native actions, research, recruitment, Back, support limits and NoPrototypeBoarding remain unchanged.
- Use the available actual Mimic art/placeholder for this increment. Tribe-specific actual art is future work and is not a prerequisite or an excuse to delay this behavior. Keep framing readable without modifying the authoritative unit or public appearance.

## Implementation choices to make and report

These details were not numerically specified by Alex; use bounded implementation judgment and native visual evidence, without inventing additional design approvals:

- Choose a readable translucency level that clearly distinguishes the owner's apparent Mimic from a genuine unit; avoid making the unit difficult to locate. Record the selected value and screenshots. No opacity change is permitted in another viewer's view.
- The existing 0.55-second threshold is an implementation choice, not a new immutable design requirement. Keep deliberate holds distinct from ordinary short clicks, movement/drag, UI holds, multitouch and stale map hits.
- Treat each match load/reload as a fresh session boundary. Handle wrapper recreation during an uninterrupted session without mistaking the same logical unit for a new choice, and reject stale references to different units.
- On temporary authorization loss, suppress private presentation immediately. Whether an inaccessible memory entry is retained until session end or discarded on irreversible unit/ownership retirement is a defensive implementation detail; it must not reset choices merely because selection/actions/turns changed or leak a previous owner's choice to another viewer.

## Acceptance matrix

All cases begin **NOT RUN** for UI-A06. Record source/model/fixture/native outcomes separately. Native screenshots and observed interaction outcomes are required for rendering/input claims; posting an event or passing a fixture does not establish native success.

| ID | Setup and action | Required result |
| --- | --- | --- |
| A06-01 | Load a disposable match containing two owned Mimics and a genuine unit. | Both Mimics default apparent; owner-only translucent map cue; genuine unit is ordinary. No automatic popup. |
| A06-02 | Select owned Mimic A; deliberately hold and release once. | Exactly one actual toggle; map and bottom-left agree; actual map art opaque; no stats popup opens. No stars/action or unit-state change. |
| A06-03 | Toggle A using the bottom-left button, including a deliberate hold over that UI button. | Exactly one toggle per native button activation; UI contact does not also trigger map inspection; no automatic popup. |
| A06-04 | Open the information icon in each mode. | One native stats group and portrait matching that mode; actual 1/1 and zero damaging offense, appropriate operative movement; native close works. |
| A06-05 | Leave A actual, select B, toggle B, select a genuine unit, clear selection, then reselect A/B. | Each unit retains its independent choice; A's authorized map view remains actual while unselected; bottom-left follows the selected unit; genuine UI stays ordinary. |
| A06-06 | From actual mode, issue a permitted native move/action and allow native refresh/reselection. | Command completes normally and spends its normal budget; same Mimic remains in its remembered view afterward. Inspection does not create another action budget. |
| A06-07 | End turns with A actual and B apparent; inspect both on the owner's next turn. | Choices survive the turn transition; native ready/spent state remains correct. No cross-viewer private flash during the transition. |
| A06-08 | Save, leave and reload that same disposable match; also start a different disposable match. | No inspection preference was serialized; all units start apparent with the appropriate owner's cue. Old IDs/pointers do not import choices. |
| A06-09 | Render the same disguised Mimic for another player before/during/after the owner's actual toggle. | Other view stays ordinary apparent, with no opacity/portrait/name/stats/control identity leak. An ordinary enemy-unit test alone does not establish this case. |
| A06-10 | Change local viewer; exercise ownership/control loss and regain where supported. | Private render objects/cues cannot survive into an unauthorized view; no preference is copied between viewers. Ownership checks remain effective. |
| A06-11 | Lose visibility or hide/destroy/replace a unit while actual mode or its info is active. | No through-fog art, stale popup truth, retained overlay or reused-pointer disclosure; cleanup does not corrupt native rendering. |
| A06-12 | Short click, native double-click/focus, unselected hold, drag, invalid command/target context and multitouch. | Native behaviors remain; rejected holds cannot rearm during the same contact or spend/refresh actions. No unwanted toggle or auto-popup. |
| A06-13 | Repeatedly open/close actual and apparent info, then open genuine/enemy info and reuse native UI objects. | Correct new portrait/stats, one group, no stale source exception, no custom identity/cue on another unit. |
| A06-14 | Exercise newly recruited/spent and next-turn ready Warrior/Rider forms; inspect map and portraits. | Both input paths, session memory, opacity and framing work for both forms; operative movement and apparent/native gameplay are preserved. |
| A06-15 | Recheck research, second wheel/Back and a normal three-star purchase. | Existing tree/popup/roster/support/payment/spawn behavior remains; Back restores native options without a purchase. |
| A06-16 | Compare gameplay/save/network-visible data before/after toggles; release resources and reload. | No counterfeit persistence or authoritative state mutation; only owned local display state/resources change and session choices reset on load. |

A passing single-player owner test does not establish opponent-owned Mimic, hotseat/multiplayer, replay or spectator privacy. Report unavailable native contexts as unverified, not passed.
