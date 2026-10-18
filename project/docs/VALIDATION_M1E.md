# M1E validation and limits

| Layer | Result |
|---|---|
| Incoming R1 Mac evidence | Successful build/install; 157 model and 44 synthetic UI cases; 235 pinned + two local Unity signatures; final adapter compilation and local nullable metadata audit |
| Baseline correspondence | All 68 receipt source entries match delivered R1; exact installed archive hash accepted for backup/replacement |
| Hosted M1E Python/file-level checks | 101 passed; source/contracts/build/installer/report tests use temporary fixtures, not the user's Mac |
| Hosted M1E native metadata | 285 supplied-wrapper signatures matched; no wrapper code executed |
| Hosted Unity dependencies | Two local Unity signatures unavailable here; build requires them on the preserved Mac installation |
| M1E managed model/UI execution | NOT RUN here; required Mac build gates: 157 model + 53 synthetic production-adapter cases |
| M1E final C# adapter compilation | NOT RUN here; Mac build retains R1 compiler metadata definitions and emitted-binding audit |
| M1E game acceptance | NOT RUN; native input ordering, internal setter interception, portrait layout/color and pooled navigation remain Mac checks |

Hosted execution environment is Linux x86_64 with Python and shell tools. No dotnet/csc/mono or Mac game/runtime execution is available here. No dependency was installed or downloaded. Source/API inspection cannot prove Harmony detours, IL2CPP marshalling, hidden native method bodies, Unity rendering or native game rules.

New fixture behavior deliberately appends stats on every UnitData assignment and records owner at population. This models the reported duplication failure; the actual native setter body was not decompiled. The suite executes production managed adapters against stand-in objects on the Mac; it does not prove native calls route through the setter hook.

Evidence: ../evidence/m1e/api-signatures.json, hosted-python-results.json, incoming-r1-summary.json and source-changes.json. Historical receipts are kept outside build/. No native DLLs, game saves or compiled candidate are distributed.
