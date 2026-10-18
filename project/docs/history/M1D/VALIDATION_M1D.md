# Validation of the M1D source candidate

Observed in the hosted Linux x86_64 workspace on 2026-09-15. No Mac was connected. Python and shell execution were available; dotnet, csc, mcs and mono were absent. The supplied generated assemblies support read-only metadata inspection; they are not a native game runtime.

| Layer | Observed result | Meaning |
|---|---|---|
| Initial handoff integrity | PASS: 144 exported files; all 56 M1C source inputs matched the successful Mac receipt | Started from the supplied unchanged M1C project |
| Final baseline preservation | PASS: all 53 non-project handoff files byte-identical; no original project file deleted | Baseline evidence, wrappers, compatibility records and launcher reference preserved |
| Protected M1C source | PASS: TechTree, RecruitmentCatalog, ResearchAudio and TechPresentation hashes unchanged | Core research/roster/audio/popup mechanisms preserved in source |
| Python/file/installer fixtures | PASS: 91 checks; exit 0 | Source contracts, metadata, receipt validation, backup/install file operations in temporary fixtures; no real installation |
| Native metadata | PASS: 235 exact signatures against pinned supplied bytes | Names/types/staticness/constructors checked; no native rule bodies executed |
| Extra Unity dependencies | NOT CHECKED HERE: 2 signatures; Mac build requires both | CoreModule was not included in supplied references; no invented hash or replacement library |
| Python and shell syntax | PASS | All Python tools/tests parse; bash -n run.sh succeeds |
| Managed C# model tests | NOT RUN HERE: 157 authored cases | Must compile and execute on the bundled Intel runtime during Mac build |
| Managed production UI adapter fixtures | NOT RUN HERE: 44 authored cases | Actual adapter callbacks plus synthetic native stand-ins; not Unity/Harmony/native behavior |
| Native adapter C# compilation | NOT RUN HERE | Required before the build can produce an installable mod |
| Actual Mac game/UI tests | NOT RUN for M1D | Follow M1D_PLAYTEST.md; no gameplay fix is certified yet |

The observed Python command, from project/, was:

```bash
DECEPTION_API_REFERENCE_DIR=/workspace/scratch/c0884f46a1c6/deception-workspace-handoff/references python -m unittest discover -s tests -p 'test_*.py'
```

It finished with `Ran 91 tests` and `OK`, exit 0. This path identifies the hosted observation, not a command to copy onto the Mac. Source-only packages omit the private reference DLLs; local Python runs without that environment variable explicitly skip the supplied-reference checks. The guarded Mac build resolves the existing installed references itself.

Some inherited packaging tests fabricate build records and call the report/installer functions inside temporary directories to test their behavior. Their synthetic PASS records and temporary archive/backup messages are not managed execution, a collected user diagnostic ZIP, or an installation. No real report collection, Finder action, game launch or framework modification occurred.

Three real Astra subagents performed independent presentation/UI, native actions/transport, and specification/test/ABI investigations. The lead integrated shared files. Final source reviews found no remaining concrete blocker in the reviewed paths; compilation and gameplay remain necessary gates. The newest after-release command/wait/processing cancellation branches were source-reviewed and are not individually covered by the authored synthetic cases.

Original M1C evidence remains historical. Its successful Mac compilation/150 model/24 fixture results and user-reported gameplay successes must not be attributed to M1D. No active build receipt or prebuilt .polymod is included in this candidate.
