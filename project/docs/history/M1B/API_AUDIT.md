# Native API metadata audit — M1B

`tools/native-wheel-api.json` records 144 exact method/accessor signatures from
three supplied generated reference assemblies. `tools/audit_native_api.py` checks
their hashes and parses their metadata without loading/executing them.

Included: QuickActions Show/Hide/AddButton/SortButtons, round-button creation,
UIUnitRenderer selection, event conversions, own-roster lookup, native train
validation, contextual getters, and the old research-audio calls.

By-reference game-list results use their exact generated generic list type rather
than `ref object`; this requires the added compile-time game-wrapper references.
The optional information-heading `TMP.text` assignment is best-effort; persistent
native button labels are the primary Mimic-mode cue. The native layout/callback
behavior cannot be established by metadata checking and remains a game test.

This is not a native-code disassembly or behavioral verification. The exact new
UI interactions and native IsValid success must be checked on the target Mac.
