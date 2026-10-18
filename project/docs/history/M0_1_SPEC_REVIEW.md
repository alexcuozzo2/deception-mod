# Source review and design decisions

## Authoritative inputs

Read the complete supplied `polytopia_deception_v1_implementation_handoff_v1_5.zip`, including the full outer specification, the 180 machine-readable cases, the visual/audio/UI appendix and every file in the nested implementation handoff. The unfilled test-results template is not evidence of implementation success.

The outer v1.5 file preserves the nested DF-1 specification and adds the presentation appendix as section 21. The outer and inner acceptance JSON files are byte-identical. All four file-size/SHA-256 entries in the inner manifest validate. `spec/SOURCE_MANIFEST.json` records the exact archive/member hashes and reconciliation. `spec/original_handoff/` preserves the original nested files for audit.

Use `spec/FINAL_v1_5.md` as the complete reading copy. Old conversation summaries, including any no-enemy-city-capture proposal, are not the design source.

The appendix mentions an artwork folder but the supplied archive contains only three outer documents plus a nested ZIP; that nested ZIP contains five documents. There are no supplied sprites, fonts, audio files, shaders or code assets. Visual choices are therefore recorded as requirements, not as examined artwork.

## Explicitly retained rules

- Ordinary-tribe sixth branch, with all five existing branches intact. Deception → Misdirection → Propaganda, and Deception → Camouflage → Espionage.
- Mimic: three stars, one actual HP, zero actual offense/retaliation, independent actual and apparent records. It is never a living bare Mimic on the public map.
- Eligible enemy-city capture is permitted. The older prohibition is obsolete. Rogues are a different rule and cannot capture.
- Smuggle costs two stars and needs BOTH unused movement and action. A delivery by exchange can leave a partner ready; ordinary walking does not. Actual harvesting technology and the current native home-city assignment control its outcome.
- Range-one exchanges; no Propaganda range bonus. Partners retain movement/actions/statuses. Copy the apparent snapshot rather than the target's truth.
- Actual/Appearance are private command modes backed by one physical allowance. Same actual/apparent type does not erase independent state.
- Genuine movement, weapon and unit-affecting powers remain actual; apparent weapons are never granted. Genuine paid upgrades preserve an independent disguise.
- Positive actual damage is a reveal/death trigger; zero damage and nondamaging statuses do not get reclassified as damage. Activated genuine specialist actions reveal their actor; support targets do not automatically reveal.
- Enemy readiness is not public. A correct hidden state with a disclosure is still a failed test.
- Later Spy/Rogue/Blackout behavior remains exactly as DF-1 specifies, including restoration after income, no refresh through chains, and no automatic fog timeout. It is not implemented in M0.

C01–C20 remain documented completion defaults, not invented native measurements. V01–V16 remain native integration gates. No deliberate balance change is introduced by this package.

## Milestone boundary

The source's section 18 explicitly starts with native audit/test harness → sixth tree and state schema → base Mimic recruitment/movement/capture. M0.1 completes a source implementation for that first tree/schema checkpoint and an executable model-test suite, but native testing has not been performed.

No fake “complete Mimic” is inserted merely to demonstrate recruitment. Native Mimic recruitment requires appearance generation, native action/HP scaling, identity preservation and save integration together. Those are the next adapter work, not silently satisfied by the pure model.

## Implementation choices versus design rules

The `dcp_` identifier prefix, per-ordinary-tribe hidden root clones, separate pure C# model, local development filenames and source build layout are engineering choices. They are not claimed as user-specified terminology. The visible technology is named Deception. The root clones are grouping data only, not replacements for native research technologies.

The synthetic test profiles and Manhattan distance fixture are deliberately labeled test inputs. Real native data, adjacency, population yield, flag transitions and special-form behavior must come from their reviewed adapters, not those convenient fixtures.
