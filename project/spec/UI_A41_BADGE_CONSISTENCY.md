# A41 — Apparent badge consistency

Alex reports that a Mimic copying a Spy through Regroup has a larger badge glyph than the genuine Spy. A badge must not reveal whether its displayed unit type is a disguise.

Use the same icon, circle-relative size and alignment for each displayed identity, including Spy and Spymaid, in owner and opponent views. Owner-only actual/apparent inspection still has its intentional rear-badge fade and separate slots; switching focus must not resize the glyph. Preserve native team colors, shader alpha encoding, visibility, pool ownership and exact restoration when custom presentation ends. Do not resize accepted unit bodies, heads or portraits to fix badge geometry. Ordinary native unit badges retain their normal artwork and geometry.

Verify genuine versus copied badges, repeated refresh and focus toggles, missing/changed placeholder glyphs, and pooled cleanup. Automated renderer fixtures are separate from native gameplay acceptance. Capture the current 0.3.80 archive and logs, recognize its exact replacement hash, keep Hub metadata current, run the full check/build and install only this mod with Polytopia closed. Run the everyday launcher's read-only check. Alex leads gameplay; no automatic launch or save edits. A39 native movement focus and A40 concealment remain unchanged.
