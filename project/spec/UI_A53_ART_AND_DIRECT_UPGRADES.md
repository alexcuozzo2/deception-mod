# A53 — artwork polish and direct mod upgrades

Alex's 2026-09-27 follow-up to A52:
- Make Motherfogger artwork about20% larger. Preserve genuine/copy equality, action/collision behavior and native badge geometry.
- Give Reconnaissance inverted colors to distinguish its icon from Surveillance. Invert existing RGB in the runtime artwork path; retain alpha and the original Surveillance image.
- Friend received the old-version STOP while likely using0.3.88. The updater must replace any installed Deception version directly, even if intermediate releases were skipped. No predecessor whitelist and no old-mod backup creation, per explicit user instruction.
- Keep new-package integrity, safe-path, game-closed and existing loader/game compatibility checks. Update only the mod, atomically; a staging/commit error must leave the previous file intact. Do not delete existing historical backups or modify libraries/saves.

Release0.3.91 retains A52 mechanics. Both online peers use the exact new build. Combined ZIP retains Mac installer/upgrader and Windows preparation-only section. User owns gameplay testing; no agent launch/playtest. User confirmed the game closed for installation. Local install uses the explicit no-backup option while retaining source/result/hash checks.
