# M1D-R1 handoff

Version 0.2.4; milestone M1D-R1. Started from the exact delivered M1D candidate (its original archive SHA-256 was 873c67abfa571cae535779553a0b2fd5da11ce7d93c845c58290e256d6a8511c). Original M1D handoff is preserved at project/docs/history/M1D/HANDOFF.md.

The two new reports contain one identical Mac attempt: model157/UI44/native signatures235+2 passed, then final Deception.dll compilation failed with 697 CS0656 errors for nullable compiler constructors. Game logs are historical M1C; no M1D native gameplay occurred. See project/evidence/input-m1d-build/summary.json.

R1 adds native-only local managed compiler attributes, an emitted-metadata audit, receipt/install/report validation, and release documentation. Every original gameplay and managed fixture C# file remains byte-identical, except the assembly version file changes to identify this release. No library, launcher, hook, action/presentation rule, or native payment change. Exact revision differences are recorded in project/evidence/M1D_R1_FILE_CHANGES.json.

The exact offending dependency was not proven: some installed wrapper bytes are absent here. The local metadata fix directly supplies the required members and checks emitted binding without relying on an unverified alias workaround. R1 cannot be called build-ready until the user's Mac compiler succeeds and the new audit passes. Hosted guard tests use an existing compiled reference as a fixture, not the new candidate DLL.

Use project/README.md for build/install/report commands and project/docs/M1D_PLAYTEST.md after successful installation. Keep all original M1D gaps, notably identity-safe transport and Cloaks, explicitly open. Preserve the M1C archive/backups if present; no automatic restore, framework reinstall, Finder or diagnostic ZIP generation.
