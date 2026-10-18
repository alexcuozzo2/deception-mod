---

# 21. Visual / Audio / UI production appendix (finalized for implementation handoff)

This appendix is normative for presentation and interaction unless it directly conflicts with the gameplay rules above. Where presentation would otherwise leak hidden state, the anti-leak rule takes precedence. The goal is that deception feels **convincing, readable, and native to Polytopia**.

## 21.1 Art direction and source-of-truth styling

- New art should match **The Battle of Polytopia**'s existing visual language: low-poly, clean geometric silhouettes, bright readable palettes, strong faction color blocking, simple material definition, and high readability at small sizes.
- Mimic and Spy assets should sit naturally beside native units and icons rather than look like external fan art or hand-painted overlays.
- Follow native proportions, idle motion amplitude, shadow treatment, outline/edge treatment, and icon density as closely as practical.
- Use the user-supplied official graphics folder as a **style reference source**, not as permission to copy implementation details blindly. Match the game's visual language and asset naming/organization conventions where practical.
- New UI icons should be legible at the game's smallest in-match scale and should read correctly against snow, desert, water, forests, ice, algae, flooded tiles, and city backgrounds.

### New required art assets

#### Mimic unit assets
- **Mimic portrait / info sprite**: used in recruitment/info panels only. This is the only place a “bare Mimic” should normally be seen.
- **Private Mimic badge**: small controller-only marker shown on the map when the controller views their own Mimics.
- **Ability icons**:
  - Smuggle
  - Regroup
  - Disguise
  - Feign Heal
  - Feign Swarm
  - Feigned naval upgrade variants if the game needs distinct button art rather than reuse
- **Selection-panel badges**:
  - Actual
  - Appearance
  - Feint

#### Spy unit assets
- **Spy unit sprite/model** in native style
- **Spy portrait / info sprite**
- **Ability icons**:
  - Subvert
  - Blackout
  - Relay (if surfaced explicitly in UI/help)
- **Rogue status visuals**:
  - light red shading / tint
  - red “agitated/rogue” bolt effect above the head
  - optional broken-banner or rogue marker in unit panel

#### Shared state/UI assets
- **Propaganda overlay marker** for enemy fog tiles over your visible map
- **Blackout target bullseye** (reuse native red target style where practical)
- **Actual/Appearance tab indicators** or equivalent mode chip
- **Cloak eye icon compatibility** for real or fake cloak-like apparent forms
- **Transformation poof effect** for disguise stripping on damage or genuine specialist reveal
- **Shuffle / appearance-transfer FX** shared by Regroup and Disguise

## 21.2 General presentation principles

1. **Opponent view must always be believable.**
   No audio, shading, or motion should accidentally reveal actual identity.
2. **Controller clarity beats mystery.**
   The controller should always be able to tell actual form, apparent form, current menu mode, and genuine versus feigned actions.
3. **Same public cue for Regroup and Disguise.**
   Opponents should know that “one of the deception actions occurred,” but not which one, and not which unit was the Mimic.
4. **Damage is always truthful.**
   Damage numbers, HP loss, death, and retaliation results must reflect actual combat.
5. **Animations may be fake; results may not.**
   Apparence can govern the source animation. Reality governs actual damage, ownership, support eligibility, and capture legality.

## 21.3 Recommended shuffle animation choice

Two candidate concepts were considered:
- **Converge/overlay**: both units move toward a midpoint, overlap, then resolve back outward.
- **Shell-game rotation**: both units revolve around each other, like a cups-and-balls swap, then return to tiles.

### Selected recommendation: shell-game rotation

Use the **rotating shell-game / cups-and-balls animation** as the primary public shuffle animation.

#### Why this is preferred
- It communicates uncertainty more intuitively: the viewer understands that positions and/or identities may have been mixed.
- It reads better when the two units have very different silhouettes, including coast cases where one endpoint may be a land body and the other a vessel/passenger representation.
- It naturally supports the “maybe swapped, maybe not” design goal.
- It is more visually expressive and memorable for the deception branch.

### Exact animation structure

For visible participants, the public effect should be a **two-stage sequence**:

#### Stage A — appearance-transfer cue
- A short shared effect appears, indicating a deception interaction occurred.
- This can be a brief flash/shimmer/puff between the two tiles.
- This stage is identical for Regroup and Disguise.
- It should not identify the source unit.
- It should be visible only for participants/tiles that the observer could already legitimately see.

#### Stage B — shell-game shuffle
- The two visible units orbit around a midpoint or visually “cross” around each other in a tight arc.
- Duration should be brief and readable; target roughly the feel of a quick, playful but clear magic-trick swap.
- End state lands cleanly on the two destination tiles.
- This exact public animation plays whether the user selected **Swap** or **Stay**.
- If the final result is “Stay,” the animation still plays and resolves with each unit back on its original tile.

### Invisible / partially visible cases
- If only one participant is visible to the observer, show the stage-A transfer cue at the visible unit/tile only, plus a localized abbreviated shuffle cue that does **not** reveal the hidden tile.
- If both participants are invisible to the observer, play **no location-revealing shuffle**. At most, play sound only if that matches native hidden-action rules; safest default is no special public effect.
- Never reveal the location of an invisible Cloak or invisible cloak-like disguised unit just because it participated.

### Space-constrained fallback
If map density, camera scale, or tile geometry makes the orbital shuffle unreadable, a simplified midpoint converge/cross can be used **internally as a fallback**, but the intended shipped effect should still feel like a shell-game rather than a simple merge.

## 21.4 Regroup / Disguise public VFX and SFX

### Required public sameness
The following must be the same for Regroup and Disguise from the opponent’s perspective:
- action-name popup behavior (preferably none)
- general effect family
- sound family
- timing
- intensity
- target highlighting behavior
- whether the animation implies a swap

### Proposed visual recipe
- A brief **magical misdirection pulse** between the two units
- Low-intensity dust/smoke/sparkle burst in a style compatible with Polytopia's simple, readable FX
- Followed by the shell-game shuffle
- End with a tiny settling “pop” or snap as the units finish

### Proposed sound recipe
- A short soft **“whoosh + pop”** or **“shuffle + poof”** sound
- Light, trickster-like, not sci-fi and not heavy explosion
- Reuse the exact same sound for Regroup and Disguise
- Keep it short so repeated chain-shuffles do not become audio clutter

## 21.5 Disguise stripping effect

When a genuine disguise is removed due to:
- taking positive damage,
- committing a genuine direct attack,
- or using a genuine specialist ability,

a **small poof/unmask effect** should play and the unit should immediately resolve to its true appearance.

### Visual notes
- The effect should be readable but not overly dramatic.
- It should feel like a magical or theatrical unmasking, not a death explosion.
- If the unit is already dying from the same event, the death presentation takes precedence. A Mimic does not become a standing bare Mimic.

### Audio notes
- Short “poof” or “reveal” sound
- Should differ from death and from the Regroup/Disguise shuffle sound

## 21.6 Rogue presentation

Rogue units need to be instantly readable by **both players**.

### Required map cues
- Light red hostile/rogue tint overlay
- Red jagged “anger/confusion” bolt effect over the head
- Maintain the apparent body sprite underneath; Rogue is a state overlay, not a body replacement

### Required unit-panel cues
- Status label: **Rogue**
- Show remembered owner and current commander
- Make it clear that the unit can attack either side, but cannot capture cities/villages/ruins or be a friendly support recipient

### Audio suggestion
- On Subvert success, play a short “corrupt/confuse” chime or sting distinct from Mind Bender conversion

## 21.7 Spy ability presentation

### Subvert
**Interaction flow**:
1. Select Spy (Actual menu)
2. Tap **Subvert**
3. All valid target units in network reach are highlighted
4. Select target unit
5. Resolve Rogue conversion, reveal the initiating Spy if disguised, then transfer command

**Targeting visuals**:
- Use native hostile target highlighting style where possible
- No need to display the specific relay path
- If desired, show a faint network pulse across your connected Spies privately, but do not expose hidden topology to opponents

**Animation**:
- The **initiating Spy** is the one that visibly performs the action, even when relay extended the reach
- Play a small ranged “command dart / signal pulse / covert order” animation from the initiating Spy to the target
- On hit, target gains Rogue visuals immediately

**Audio**:
- Short signal/whisper/hex sound
- Distinct from Blackout's smoke-bomb sound

### Blackout
**Interaction flow**:
1. Select Spy (Actual menu)
2. Tap **Blackout**
3. All valid tiles in network reach are highlighted with the native red bullseye style
4. Select target tile
5. Resolve fog placement for all opponents

**Animation**:
- A visible ranged throw from the initiating Spy to the tile
- The payload should read as a **smoke bomb / obscuring device**
- On impact, ordinary fog closes over the tile

**Audio**:
- Soft thrown-object / smoke-puff sound
- Distinct from Subvert

### Relay
Relay is passive and should generally **not** need its own active button.
If any helper visualization exists, it should be controller-only: e.g. faint lines or pulsing connected nodes when a Spy is selected.

## 21.8 Propaganda interface

Propaganda should not use a dedicated global HUD button.

### Interaction flow
- Select an **enemy unit or city**.
- If you have Propaganda, show the selected enemy’s fog mask over tiles **you can currently see**.
- Use a translucent fog overlay or fog icon treatment clearly distinct from your own normal fog.

### Requirements
- No information in your own fog.
- No change to the opponent’s interface.
- Works on Rogue units by using the **remembered original owner**.
- Overlay is informative only; it grants no action and no persistent revealed-state bookkeeping.

### Visual treatment
- Translucent enemy-fog overlay, subtle but readable
- It should not make units beneath it unreadable
- It should not look like actual Blackout fog placed on the map; it is an inspection overlay

## 21.9 Mimic sprite and UI contract

### On-map appearance
- Mimics always appear as their **apparent form** on the battlefield.
- There is no living “revealed Mimic” battlefield sprite.
- The controller alone sees a small private Mimic badge over/near their Mimics.
- The badge should be compact and not obscure faction color or important status indicators.

### Panel appearance
- The info/recruitment panel should have a proper Mimic portrait/sprite and descriptive text.
- The panel should clearly show:
  - Actual form
  - Appearance form
  - Home city
  - Whether the current menu is Actual or Appearance

### Menu toggling
- First select: Actual mode
- Click the selected unit again: Appearance mode
- Click again: back to Actual
- Selecting another unit resets to Actual mode

The mode chip/tab should be unmistakable. Suggested labels:
- **Actual**
- **Appearance**

In Appearance mode, feigned actions should have private labels such as:
- **Feign Heal**
- **Feign Swarm**
- **Feign Scout Upgrade** / etc. if needed

## 21.10 Feigned action presentation

Feigned support should be visually convincing to opponents while remaining clearly identified to the controller.

### Controller-facing rules
- The button text/icon should indicate feint status
- Tooltip should state that the action affects **appearance only** and does not change actual stats or ownership
- Real and apparent outcomes should both remain visible in the panel after use

### Opponent-facing rules
- They see the ordinary represented support animation
- No “fake” tag, warning, or different sound
- If they inspect their own unit, they still see true underlying state because controllers always get truthful information about their own units

## 21.11 Naval UI and visual treatment

### Apparent naval forms
To opponents, ordinary embark/disembark and disguise interactions should read as normal naval events.
- Giant-family embarkation appears as Juggernaut-style vessel
- Cloak-family visible embarkation appears as Dinghy-style vessel; invisible cases remain concealed appropriately
- Ordinary land units become Raft-style apparent vessels

### Mimic Boat identity
- Controllers know whether a vessel is a Mimic Boat via the private Mimic badge and Actual panel
- Opponents just see the apparent ship/passenger state

### Free feigned naval upgrades
In Appearance mode, eligible apparent Rafts may feign upgrade within your territory.
- Use ordinary naval-upgrade icons where possible, but label privately as feigned if needed
- Free feigned upgrades should have a visually lighter confirmation, but still look native
- Performing a feigned upgrade should not strip disguise

### Genuine paid naval upgrades
In Actual mode, use the ordinary native paid-upgrade interface.
If the unit has an independent appearance, preserve that appearance after the real upgrade unless another reveal rule applies.

## 21.12 Capture, Smuggle, and city feedback

### Smuggle
Smuggle should feel like a native resource action.

**Interaction flow**:
1. Select eligible Mimic (Actual menu)
2. Click resource-harvest action on fruit/fish/animal tile if movement+action are both unused
3. Deduct cost and award population to the current home city

**Visuals**:
- Reuse native harvest-style action language
- No “revealed Mimic” presentation afterward
- Population should visibly travel/fly to the receiving city, similar in spirit to other native reward transfers
- Optionally outline or ping the home city for the controller only

**Audio**:
- Use a harvest/resource confirmation sound
- Do not add a special enemy warning sound

### Capturing villages/ruins/cities
- Mimics use the native capture/explore presentation of their apparent form
- Rogue units cannot capture and therefore never enter these native presentations

## 21.13 Accessibility and anti-leak checklist

### Accessibility
- Do not rely on color alone for Rogue or Mimic information shown to the controller.
- Pair tint overlays with iconography where practical.
- Tooltips and panel text should name Actual vs Appearance clearly.
- Sound should support, not replace, visual understanding.

### Anti-leak requirements
Do **not** let the following reveal hidden truth to opponents:
- action shading
- different button availability for enemy inspections
- distinct sound variations between Regroup and Disguise
- source/endpoint arrows that identify the Mimic
- a visible relay path through hidden Spies
- post-shuffle ready/exhausted tint changes
- invisible-unit shuffle effects on unseen tiles
- separate “revealed Mimic” battlefield sprite
- different smoke-bomb origin selection depending on chosen relay path
- native upgrade/panel transitions that briefly flash the actual unit form

## 21.14 Suggested implementation order for presentation work

1. Actual/Appearance unit-panel framework
2. Private Mimic badge and Rogue overlays
3. Regroup/Disguise shared effect pipeline
4. Subvert presentation and Rogue state cues
5. Blackout projectile + fog application cue
6. Propaganda overlay
7. Feigned support button set and panel labels
8. Naval feigned-upgrade UI
9. Cloak/invisibility edge-case presentation
10. Replay / save-load / multiplayer visibility audit

## 21.15 Presentation acceptance checks (supplemental)

These are in addition to the gameplay acceptance cases.

### Simple
1. Selecting your own Mimic shows Actual mode first, with actual and apparent forms both visible in the panel.
2. Selecting the same Mimic again toggles to Appearance mode without changing the map sprite.
3. Selecting a different unit resets that new selection to Actual mode.
4. An opponent cannot tell from on-map readiness shading whether a shuffled unit has spent its action.

### Moderate
5. Regroup and Disguise look the same to the opponent aside from the resulting visible board state.
6. Swap and Stay produce the same public animation family.
7. A visible disguised Spy that uses Subvert reveals itself; a disguised passive relay Spy does not.
8. A Mimic performing Smuggle still looks like its apparent form to the opponent before, during, and after the action.
9. A real unit disguised as a Cloak shows normal eye-warning behavior when appropriate.
10. If only one participant in a shuffle is visible, the effect does not reveal the invisible partner’s tile.

### Advanced
11. A genuine Juggernaut disguised as a Raft causes real splash damage but does not play Juggernaut-identifying source animation.
12. A Mimic Boat apparently upgraded to Bomber shows Bomber-like visuals but cannot deal actual damage.
13. An actual Raft with an independent apparent Raft can still access Appearance-mode feigned naval upgrades.
14. Propaganda overlay appears only while an enemy unit/city is selected and only across tiles you can see.
15. Blackout uses ordinary fog visuals for all opponents and gives no special persistent marker to the caster.
16. Replays do not leak hidden actual identities through animation choices, path traces, or panel state.
