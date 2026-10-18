# UI-A09 — translucent rear type badge

## Authority and precedence

Source: Alex's explicit live request on 2026-09-15 after the UI-A08 badge playtest. Make the rear type badge semi-transparent so the clear front badge emphasizes the identity of the displayed map body.

This changes only the emphasis of the overlapping map type badges. UI-A08's single map body and the approved bottom-left portrait stack remain unchanged. UI-A08's prohibition on added map-body translucency remains in force; its prohibition on added badge translucency is superseded for the **rear badge only**. Keep all earlier amendments as history. UI-A06 session memory, deliberate information opening and all nonconflicting authorization, native-status, input and cleanup rules remain.

This is a requirements amendment. UI-A08 screenshots or passing fixtures do not establish UI-A09 success.

## Required behavior

- Keep exactly one chosen map body and the existing two partly overlapping small type badges in the native top-right area.
- The front badge matches the displayed body and remains clear. The other identity's rear badge is semi-transparent and still recognizable.
- With an apparent disguise body, the native disguise type badge is clear and in front; the Mimic mask badge is translucent behind it. With the actual Mimic body, the mask badge is clear and in front; the disguise type badge is translucent behind it.
- Use **50% of native opacity** as the initial rear-badge implementation value. Alex requested semi-transparency without specifying a numerical alpha; 0.5 is a bounded implementation choice to confirm visually. The front badge has no additional opacity reduction. Preserve native fades/visibility rather than forcing an otherwise hidden or faded native element opaque.
- Apply the emphasis to the visible elements of the relevant badge consistently, preserving native per-role activation and renderer-enabled state. Do not reactivate a hidden outline/shadow or modify health, ready/spent, selection, target, ability or other status indicators.
- Do not add body translucency, alter the approved bottom-left stack, or change popup presentation. Hold/button still toggles view without opening information. The native information action still opens one matching portrait and one stats group.
- Keep the cue owner-only under existing authorization/visibility/native-instance guards. Other viewers receive native apparent presentation with no mask badge and no private rear-badge dimming.
- Preserve per-unit/per-viewer session choices through selection, actions and turns. Reload/new match starts apparent, with the owner's disguise badge clear in front and the mask badge translucent behind it when authorized.

## Ownership and restoration boundaries

Rear emphasis is a local rendering projection. Do not modify real unit type, health, ownership, investment, action flags, apparent records, saves, configs, replays, network messages, shared materials or globally registered sprites.

When the native disguise badge becomes the rear badge, retain and restore its native colors only while the exact renderer/status-display ownership still holds. Native refresh/fade changes must become the new baseline rather than being overwritten by stale cached colors. Compute rear opacity from that native baseline; repeated refreshes must not multiply an already reduced value and progressively fade the badge away.

Before native UI source duplication, restore any projected native badge opacity and exclude owned private badge/body objects. Selection/viewer/visibility changes, native status pooling, unit retirement and session cleanup must not leave another unit or viewer with dimmed native badges. Preserve native inactive/disabled roles throughout projection and cleanup. These rules also apply when a copy or refresh throws.

## Acceptance matrix

Every UI-A09 case begins **NOT RUN**. Record source/model, synthetic fixture, metadata and native outcomes separately. Use disposable matches and preserve the user's save. The native screenshots must show the intended clear/translucent difference; logged alpha assignments alone do not establish readability.

| ID | Setup/action | Required result |
| --- | --- | --- |
| A09-01 | View an owned apparent Warrior/Rider Mimic beside a genuine unit. | One normal-opacity disguise body; clear disguise badge in front; recognizable semi-transparent mask badge behind. Genuine body/status/badges stay native. |
| A09-02 | Toggle actual with a deliberate selected-unit hold. | One actual body; clear mask badge in front and translucent disguise badge behind; no automatic popup or body/panel opacity change. |
| A09-03 | Toggle repeatedly using both hold and button, including a UI-button hold. | Exactly one switch per deliberate activation; ordering and opacity reverse together; rear does not progressively disappear; no duplicate art or popup. |
| A09-04 | Open/close info in both modes and then open genuine/enemy info. | One matching stats/portrait group; usable native information layout; no dimmed badge, private mask or source-copy artifact in unrelated UI. |
| A09-05 | Leave A actual and B apparent; change/clear selection, complete a permitted action and advance a turn. | Independent choices and correct front/rear emphasis survive; native action budget, health and ready/spent/status presentation remain correct. |
| A09-06 | Exercise native badge refresh/fades and inactive outline/disabled shadow states. | Rear is 0.5 times the current native opacity, front uses native opacity; inactive/disabled roles remain inactive/disabled; re-enabled roles recover correct emphasis. |
| A09-07 | Switch viewer, lose ownership/visibility or hide/destroy/recycle the unit/status display. | Private cues and dimming are removed safely; no through-fog badge or stale opacity on another unit/viewer; native colors remain intact. |
| A09-08 | Duplicate native portrait sources, including a failing/nested copy, while either badge is rear. | Native UI sees restored native badge opacity and no owned private map objects; balanced cleanup restores the current authorized map emphasis afterward. |
| A09-09 | Save/leave/reload the same disposable match and start another match. | Apparent default with appropriate clear disguise-front/translucent mask-rear cue; no serialized preference or leaked renderer state. |
| A09-10 | Compare gameplay/native state and native status data before/after toggles and cleanup. | Only owned local rendering and authorized projected badge opacity change; no gameplay/persistence mutation, added action/payment, or change to approved single-body/bottom-left behavior. |

Opponent-owned Mimic, fog, ownership, replay, spectator and multiplayer cases remain unverified unless directly exercised in the native game. Do not reuse owner-only evidence as a broader privacy result.
