# M1C validation

## Executed in the assistant's Linux environment

- 88 Python file-level checks: all PASS, with the supplied real metadata reference
  directory enabled. Includes input preservation, previous-archive backup,
  duplicate/wrong-version rejection, rollback, archive whitelist, explicit report
  collection and both test-result validators. Some installer failures are
  deliberately simulated. These checks do not execute managed C#.
- 149 actual generated-wrapper method/accessor signatures: MATCH, including the
  position, display-cloning and popup APIs added in M1C.
- Bash syntax and Python byte compilation: PASS.
- Original input and unchanged source comparisons: full Core directory,
  RecruitmentHooks.cs, ResearchAudio.cs and NativeApi.cs equal M1B byte-for-byte.

See packaging-tests.txt and native-api-inspection.json for recorded outputs.

## Written; NOT executed here

The 150 existing model tests, 24 new production UI-adapter fixture tests, the
revised C# adapter and native Unity gameplay/UI. There is no usable .NET SDK or
runtime in this environment, and attempts to obtain one failed. No native
success is asserted on the strength of file-level checks.

The local Mac build must execute both managed suites under the game's bundled
Intel CoreCLR before installation is allowed. The new fixture builds the real
RecruitmentWheelUi.cs, TechPresentation.cs and NativeApi.cs, with synthetic
objects replacing native APIs. It tests a nullable Target failure, placement
reset during clear, corrected Draw, option labeling, Back/cancel, duplicate and
stale requests, confirmation revalidation, one-category preview, ordinary-tech
preservation and rejection of a clone that aliases the live object.

The fixture does NOT emulate native UI animation, IL2CPP marshalling, Harmony
native detours, pointer ownership inside Unity, sprite rendering, actual currency
payment or save/reload. Its stand-in methods have deliberately limited behavior.
Even a passing fixture does not mean the game wheel is known to work.

The compiled fixture is not included in the installed .polymod; it exists only
in the local build directory. Game/framework DLLs are reference inputs, never
redistributed by this source package.
