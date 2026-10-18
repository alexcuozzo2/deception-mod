# UI-A12 — supplied Mimic and Spy artwork

Authority: Alex supplied seven PNGs on 17 September 2026 and asked to get these working first, deferring the other technology/UI icons. The seven originals and provenance manifest are in content/unit-art. This changes art, not the underlying unit rules or the preceding private-inspection requirements.

The actual Mimic uses separate fixed feet, grayscale team-color tunic, a native tribe/skin head and the supplied physical mask. Its unit-type glyph is the supplied mimic_icon.png. The same assembled identity should appear in owner-only map inspection, bottom-left portraits and explicit actual information. Disguise bodies remain native. Current view memory, two fixed badge slots, foreground clarity/rear alpha and cleanup/visibility/owner guards remain required.

Use the owning player's actual match color, not a hard-coded tribe default. Do not recolor shared sprites/materials or change gameplay state to render the body. Keep each fixed/team pair under one geometry transform so the original pixels align. Preserve source PNG bytes. Original interior alpha and edge artifacts must be reported rather than hidden by a success claim.

Spy supplied art consists of a fixed brown coat/boots, team-color shirt/pouch and a hat/glasses unit glyph. Native tribe heads can be reused. Asset preparation does not establish Spy registration, recruitment or network behavior; those are presently absent from the native adapter. Do not expose a placeholder combat unit as a completed Spy.

Acceptance remains pending Alex's observation: usable unit/category glyphs, actual body proportions, head/mask placement, assigned color across tribes/skins, correct portraits, stable swaps and no leaked layers on ordinary/foreign units. Fixture/metadata results cannot establish native visual success.

## 0.3.41 correction after Alex’s test

The 0.3.40 artwork is not visually accepted: native head lookup failed and aborted actual body/title/badge projection; tech transforms retained Warrior; the movement tag retained the longer header width. Use the native asynchronous head address path with safe pending artwork, update actual selected/map/popup art only while its guards remain valid, and render the Deception category as a native-sized owned unit preview. Match native title-to-movement-tag spacing after setting the final title. User-facing Hub and assembly version descriptions must track every release. Automated tests remain separate from Alex’s next observation.

## 0.3.42 human feedback

Alex confirms0.3.41 actual/disguise toggling, matching menus and information, mask placement, title/tag spacing and updated Hub metadata. Deception node, Mimic reward and recruitment category remain blank. Brief first-tech-open lag was observed; installed framework sprite loading performs a redundant managed copy of every pixel. Use direct native PNG decoding with unchanged source bytes, filter and mip policy. Native technology fitters inspect Sprite vertices in native pixel units and ignore per-image scaling; category preview geometry must honor that contract.

Alex explicitly requests enlarging the native head now: the unchanged body was already traced from a Warrior. First correction is25% larger head and attached mask, anchoring the bottom of the head to the same neck height; fixed/team body layers retain exact geometry. Alex prepares Camouflage/Propaganda/Intelligence icons independently; do not invent replacements. Native appearance still requires his observation.

## 0.3.43 human feedback and corrective increment

Alex confirms0.3.42 Deception/reward/recruitment artwork is visible, research/recruitment/toggle function. Remaining failures: preview overflow; large body, small detached head/mask; Warrior flicker at end-turn; missing native ready outline and spent washout. Alex asks body roughly50% smaller, head25% larger than0.3.42, mask50% larger and slightly upward. Anchor visible head base at the body's neck, handle borrowed sprite pivot, keep source PNGs unchanged. Category bounds must measure the actual drawn head footprint while retaining packed native sprite rendering.

Map appearance choice must remain visible during AI turns while the same owner remains the current local viewer. This supersedes the earlier current-turn requirement for read-only map presentation only. Interactive inspection and commands retain their turn/idle/selection guards; map still requires exact live instance, local owner/viewer, game/session, native visibility and non-replay/non-spectator context. Native material effects control ready/spent presentation; do not fake action flags or brighten art by guessing gameplay state. The owned actual silhouette should follow native outline activity and color.

This records the requested change and implementation intent, not native acceptance. Alex performs the next comparison.

## 0.3.44 human feedback and corrective increment

Alex confirms that0.3.43 eliminates end-turn flicker and matches native spent washout. Reduce head dimensions25%, increase aligned body dimensions25%, retain the current450-unit mask and its head-relative offset, and increase head/body overlap to close the visible gap. Keep the existing foot baseline. Double outline dilation/blur and increase padding without enlarging texture/mesh/cache caps. Continue using native outline activity/color.

The tech tree remains slow. Own PNG frames and cached category PPU views now explicitly request FullRect, avoiding duplicate Tight alpha-contour generation; original source PNG bytes and borrowed native head sprites remain unchanged. Conservative full-rectangle fitting can leave more space around previews. Keep mip/filter policy and add bounded per-stage/per-new-view timings. The previous outline failure was not localized: record source, counts and stage if it recurs. These changes still require Alex's visual and timing comparison.

## 0.3.45 human feedback and corrective increment

Alex confirms0.3.44 still has no end-turn flicker. Menu art is relatively decent, but map head/body are too small and still separated. Increase head about30% and body slightly more (35%), preserving mask dimensions and the existing foot baseline. Native Imperius head has transparent space below its visible mesh; anchor the actual lower mesh at the tunic collar rather than the full rectangle bottom. Use bounded cached geometry, preserve borrowed sprites and fall back safely for an unsupported mesh. Original artwork pixels remain unchanged.

The ready glow should form a diffuse cloud extending outward with a gentle fade. Keep the native shader/material, color and activity; use a small dilation with a broader three-pass blur and enough padding. Preserve faint alpha values until final quantization. Native texture/cache/mesh caps and cleanup remain. Shader inspection establishes continuous sampled alpha, not native visual acceptance. Alex compares the next build beside a Warrior and confirms no-flicker/spent-state behavior.

## 0.3.46 silhouette refinement

Alex's0345 screenshot shows improved shape, stillslightlysmall. Increase both head and body10% while preserving footbaseline and maskdimensions. Move mask20designunits up and20units towardhead center; actual screen pixels depend oncamera. Sharedcomposition and meshcollar contact remain. Body950.4, headScale1.67578125, maskX89.375 andhead-relativeY−82.8125. UseoneBodySizeconstant forbothlayers/contact soresizecannotdetachthem. Verifybar/popupfit aftergrowth.

Alex explicitly wants silhouette finalized before further glowwork; glow remains unaccepted. Keepcurrentglowimplementation for thisincrement. Do notrequestatracedborderPNG yet.

## 0.3.47 glow correction after silhouette acceptance

Alex accepts the 0.3.46 body/head/mask proportions and placement as final. Preserve that geometry and the original source PNGs. The readiness glow is still a tiny thin edge: broaden its visible fade several times at map scale while following native readiness, color, visibility and cleanup behavior. No separate hand-drawn border PNG is required for this correction because all four native alpha masks generate successfully. Do not equate the generated texture profile with native visual acceptance; Alex compares the result beside a Warrior.

## 0.3.48 contour correction

Alex rejects 0.3.47’s broad averaging blur: it resembles weak central haze, whereas the Warrior has an obvious thick glow following its border. Generate the halo from distance to sprite coverage, with a strong narrow border and a smooth outward fade. Thin features should retain edge strength, rather than fade away because they cover few pixels. Preserve the accepted silhouette, source pixels, native color/activity and ownership guards. Do not claim native visual success from numerical contour tests. Alex reports tree loading, research and recruitment work.

## 0.3.49 intensity refinement

Alex reports 0.3.48’s contour glow works extremely well and is almost perfect. Reduce its intensity 20–30%; use 25% (generated edge alpha 240 to 180). Preserve band width, tail, coverage threshold, body/head/mask geometry and all original PNGs. Keep native renderer alpha and readiness encoding unchanged. Alpha-byte rounding and overlapping halos mean perceived brightness is not an exact linear measurement. No broader art redesign is requested.
