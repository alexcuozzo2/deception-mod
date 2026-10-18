# M1B implementation status

**Observed M1A baseline:** user could research Deception, heard its research chime,
and saw the 3-star category only after research. The popup picker failed and a
selected form was rejected as not unlocked. Recruitment was not demonstrated.

**This M1B candidate:** source implemented, file-level/metadata checks passed;
C# compilation and native game/UI execution NOT RUN in the assistant environment.

| Area | M1B change or retained behavior | Native status |
|---|---|---|
| Original Basic root, sixth branch, all-tribe data | `TechTree.cs` unchanged | User-tested in earlier build; regression check still useful |
| Deception label and research audio | Name key retained; description cleaned; `ResearchAudio.cs` unchanged | User confirmed M1A; not re-executed here |
| Normal city menu | One Mimic category after Deception; concrete variants filtered out | Needs M1B test |
| Mimic appearance wheel | Native QuickActions/UIRoundButton/UIUnitRenderer, labels, prices and Back | Needs first in-game test, including layout/keyboard/cancel |
| Unlock consistency | Concrete native registrations plus own-source/Deception guards | Native validator must accept valid purchases in next test |
| Purchase | Ordinary native confirmation/command/action, no separate debit | Native cost/occupancy/support/spawn still unverified |
| Basic prototype | Distinct type; 1 actual HP, no damaging attacks/retaliation | Retained M1A prototype; not full combat/status acceptance |
| Supported forms | Same 26 reviewed forms as M1A; own native unlocked roster | Mooni, Cloak, Mermaid Cloak, Moth, Aquapult deferred |
| Movement | Same source movement/safe traits | No land/skating transitions onto water/frozen water yet |
| Public disguise | Truthful prototype names/stats | Not implemented; no full opponent appearance filtering |
| Persistence | Native prototype type IDs; no new persistent wheel state | Same-build save/reload to test; no migration claim |
| AI | Variants not added as normal trainable command choices | No Mimic recruiting strategy |
| Other mechanics | Existing model retained | Feints, Smuggle, exchanges, higher techs, complete appearance state not connected |
| Centipedes | A01 rules model retained | Native topology/growth integration still deferred |

This is a recruitment-interface checkpoint, not completion of the Mimic.
No release-scope exception is granted by these implementation gaps.
Community content and Polibrary stay outside Mods. Keep all Mac compatibility
libraries, configuration, external Doorstop candidate and everyday launcher intact.
