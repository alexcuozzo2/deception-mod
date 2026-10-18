# A31 — Aquarion units and flooded-terrain repair

Alex's 0.3.70 playtest feedback, 19 September2026; overrides conflicting older art/terrain/feint choices. Alex remains native tester; game was observed closed; no launch, Computer Use or save edits.

- Aquarion recruits amphibious **Mermimic** and **Spymaid** through native tribe unit replacements. Use supplied transparent body layers, native tribe heads, team color and existing Mimic mask. Mermimics never turn into Mimic Boats. Persist their actual species independently of disguise and temporary ownership.
- Other Mimics keep their incoming actual land/boat representation on flooded terrain. Land→flood stays land; boat→flood stays boat. A transition must never produce an exposed internal carrier name, unsupported badge, cargo loop or stalled commands. Fix underlying native transport legality/identity, not only labels.
- Spy heads10%smaller than0.3.70 and about5nativepixels lower, overlap/in front of body. Normalize visible size across tribe-specific head sprites.
- Remove Feign Recover from new offers/actions. Preserve old enum/receipt decoding for compatibility; no ID reuse.
- Blackout retains accepted grenade art/flight. Fix the observed native AudioClip.Create wrapper failure; supplied audio needs no reformatting by Alex. Impact-only custom sound respects native SFX settings.
- A selected/focused Spy must refresh automatic Subvert bullseyes after movement without reselection. Preserve native movement/drag behavior and explicit Blackout targeting.
- Rogue badge stays original-owner colored; immediately show readiness glow when acquired and able to act, not only after movement. Suppress actual siege/capture flames including poisoned city tiles. Restore compact bolt placement near the old0.3.69 size, slightly taller/thicker, never spanning adjacent tiles.

Native observations and modeled tests must remain distinct. Preserve logs and exact predecessor backup; bump mod version and install only this mod after complete checks.
