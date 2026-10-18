# M1D-R1 build failure review

## What the supplied reports establish

Both `deception-m0-report.qzv9axvg.zip` and `deception-m0-report.y4bb3dmv.zip` contain byte-identical members from the same build attempt. Exact test records and a compact failure summary are retained under `evidence/input-m1d-build/`.

The Mac used SDK 9.0.303's Roslyn compiler for compilation and the existing bundled Intel .NET 6.0.7 for test execution. All 157 managed model tests, 44 managed production UI adapter fixtures, 235 pinned signatures and two local Unity visibility signatures passed. Only the third compiler invocation, targeting the final Deception.dll with real game/framework references, failed. It produced 697 CS0656 errors naming NullableAttribute..ctor or NullableContextAttribute..ctor. There was no successful build receipt, packaged mod or M1D installation. The included game logs are older M1C logs.

This is a compiler metadata problem, not evidence that the gameplay tests failed or the working runtime needs reinstalling. The reports record no ordinary active Deception archive at collection time; the build code itself does not modify Mods, so the reports do not establish why that slot was unavailable.

## Root-cause boundary

`tools/manage.py:csc` compiles with nullable annotations enabled. The managed-only reference set succeeds; the final real-wrapper reference set fails to provide the required nullable compiler members. Relative to working M1C, M1D added PolytopiaAssembly.dll and UnityEngine.CoreModule.dll to final compilation for the typed native popup boundary.

Read-only inspection finds no NullableAttribute/NullableContextAttribute definitions or forwarders in supplied PolytopiaAssembly.dll, GameLogicAssembly.dll or Il2Cppmscorlib.dll. The supplied Il2CppInterop.Runtime.dll has correctly shaped internal managed attributes. UnityEngine.CoreModule.dll, BepInEx.Core.dll and 0Harmony.dll bytes are unavailable here, although the Mac dependency checks passed. Unity's added reference is a plausible trigger; its exact attribute definitions and the compiler's winning lookup were not observed. Do not falsely identify a particular generated type as proven defective.

## Narrow correction and review

- `src/PolyMod/CompilerMetadata.cs` supplies internal sealed managed NullableAttribute with public byte and byte[] constructors and NullableContextAttribute with a public byte constructor. Both derive System.Attribute and carry the conventional public readonly flag fields. Nullable annotation is disabled only inside this metadata-definition file; compiler nullable checking stays enabled for all gameplay sources.
- This file enters only the final adapter compilation through the existing PolyMod source enumeration. Model and UI fixture sources, hooks, native payment, presentation, action state and transport guards are unchanged.
- `tools/audit_compiler_metadata.py:check` examines the emitted DLL before packaging. It requires the managed base assembly, local internal sealed types, exact public instance constructors/flag fields, nonzero annotation applications and local constructor MethodDef references. A missing, malformed, unused or externally bound substitute prevents packaging.
- `tools/manage.py:build` runs that audit after final compilation; its checksum enters the receipt. `install` requires the matching evidence and DLL hash; `report` includes it. Framework/runtime/reference files are never changed.
- Assembly/content version is 0.2.4; command milestone is M1D-R1. One independent Astra reviewer checked the diagnosis and implementation and authored focused tests for the metadata guard. The lead owns production/build integration and packaging.

## Validation limits

Uploaded Mac passes apply to the original M1D managed suites. Those gameplay/fixture sources remain byte-identical in R1. The new metadata guard is tested against the supplied, already compiled Il2CppInterop.Runtime.dll as a known managed-attribute fixture and deliberately invalid metadata variants; this does not compile the new shim or run the game. The exact reference is read-only and is not redistributed.

R1 final adapter compilation, its emitted-DLL audit and actual game behavior remain NOT RUN here. The Mac rebuild must prove that the original CS0656 failure is resolved. No build gate was removed or weakened to produce a success claim.

Full current validation results are recorded in `VALIDATION_M1D_R1.md`; initial M1D validation remains preserved as historical evidence.
