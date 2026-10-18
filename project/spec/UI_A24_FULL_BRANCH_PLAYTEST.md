# A24 — Full branch implementation for broad playtesting

Authority: Alex's Spy/prototype feedback and full implementation request, 2026-09-18. This supersedes A23's temporary omission of Spy commands and earlier requests to stop expanding after each individual feature. It does not supersede FINAL_v1_5 mechanics unless stated below. Alex remains the hands-on tester; do not launch the game or use Computer Use. Game quit confirmed. Install this mod with the existing exact-backup guard; preserve framework, launcher and saves.

## Requested changes

- Enlarge Spy body by50% and head by70%, close their gap and center the assembled sprite. Alex will judge the resulting proportions in game.
- An exposed Mimic copied as a disguise must appear as a Mimic with1/1HP and a white health label, including when a genuine Warrior wears it. Preserve that Warrior's true type, health and actions.
- Implement Subvert, Blackout and passive Relay, including genuine versus apparent Spy distinction and the Rogue rules in FINAL_v1_5 §§10–12.
- Implement feigned support abilities, including Heal, Boost/Swarm and Recover. Feints affect only apparent state and spend the initiating physical unit's one action allowance. Actual support continues to act on eligible genuine units. Disguises may copy the same unit type and independently copy visible status effects, including poison, without adding the condition to the actual body.
- Complete known form gaps needed for native single-body special tribes: Mooni, Moth, Crab, Gaami, Bug Egg and Larva, with native phase timers and apparent terrain abilities. Preserve the existing identity guard around compound/linked bodies; that limitation must be stated explicitly until it has a complete identity-safe implementation.
- Check edge cases before handoff, but do not require perfect native stability or polished assets before Alex can test the complete feature set.

## Integration decisions

Use the existing framed state, append-only content IDs, persistent action intents, authoritative commit validation and rollback. Track temporary command separately from remembered permanent ownership. Blackout masks persist until legitimate rediscovery and must also restrict commands, selection, private information and network membership. Relay is passive rather than another active button.

Preserve the accepted single map sprite, fixed dual badge slots, dual portraits, action ordering, native stats, naval transitions, growth, shed behavior and Cloak eligibility. Do not grant actual attack/support abilities to an apparent specialist. Actual1HP Mimics remain non-damaging.

Evidence must distinguish source/model/fixture checks from native playtesting. Report remaining limitations and native acceptance honestly; no full multiplayer/replay secrecy claim.
