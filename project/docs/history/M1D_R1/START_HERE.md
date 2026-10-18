# Start here — M1D-R1 compiler metadata repair

Read AGENTS.md, HANDOFF.md, unchanged LATEST_FEEDBACK.md and NEXT_MILESTONE.md. The authoritative design remains project/spec/START_HERE.md and its referenced materials. This revision repairs the M1D final-build failure and does not change gameplay policy.

Use project/README.md for exact commands. Extract this folder to ~/Downloads/deception-m1d-r1 and build with the game closed. Continue to install only after M1D-R1 BUILD READY. Preserve old source folders and existing mod/backups; do not reinstall framework libraries.

See project/docs/BUILD_REPAIR_M1D_R1.md for file/symbol references and diagnosis, and project/docs/VALIDATION_M1D_R1.md for observed validation. Original M1D managed tests passed on the user's Mac, but its final adapter compilation failed. The R1 repair still needs an actual Mac rebuild and the existing project/docs/M1D_PLAYTEST.md checks.

Optional integrity command: python3 verify_candidate.py. This checks source bytes only. No compiled mod, native reference DLLs, game saves or active build receipt is included.
