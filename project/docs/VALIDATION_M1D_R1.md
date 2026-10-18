# M1D-R1 validation

## User's uploaded Mac attempt — original M1D

Both report ZIPs describe the same attempt. Verified records: 157/157 model tests and 44/44 production UI fixture tests PASS under the bundled Intel .NET 6.0.7; 235 pinned signatures plus the two installed Unity visibility signatures PASS. Final native adapter compilation FAILS CS0656 for nullable compiler constructors. No M1D game execution is established; the game logs are historical M1C. Exact original records are in evidence/input-m1d-build/.

## Hosted R1 checks actually run

101 Python checks: PASS, zero failures/errors/skips. This includes the retained packaging/API checks plus ten tests of the new compiler metadata guard. The guard's positive fixture is the supplied precompiled Il2CppInterop.Runtime.dll with known managed attributes; negative cases alter only in-memory parsed metadata. No game DLL is rewritten or redistributed. Exact result and per-test log: evidence/m1d-r1-python-results.json and evidence/m1d-r1-python-tests.txt.

All prior gameplay C# and C# fixture files are byte-identical to the delivered M1D candidate. Only src/Version.cs differs among old C# files; src/PolyMod/CompilerMetadata.cs is new. All Python sources parse and bash -n run.sh passes. Source/package checksum verification is also performed before delivery.

The direct unittest run returned exit 0 but its captured stream lacked the usual summary. To resolve that reporting ambiguity, the same suite was run through TextTestRunner with an explicit serialized TestResult: 101 run, 0 failures, 0 errors, 0 skips. That result is the retained evidence. Both runs exercised Python checks only.

## Remaining required Mac gate

R1's new shim has NOT been compiled here. The final adapter and its new emitted-metadata audit must pass on the Mac. This workspace has no dotnet/csc/mono or Mac game runtime. A source-level repair and an auditor tested on another assembly are not a completed candidate build.

The retry must print M1D-R1 BUILD READY only after all 157 model and 44 UI cases pass again, Deception.dll compiles, its nullable annotations are verified to use the local managed constructors, and source/framework checks remain unchanged. Install requires the matching audit receipt. No gate was bypassed, nullable analysis remains enabled, and the native/UI playtests remain necessary afterward.
