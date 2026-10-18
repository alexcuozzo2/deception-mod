# Windows port development

## Primary acceptance target: Windows multiplayer

User clarification, 2026-10-17: prioritize playable Deception multiplayer on
Windows. The supplied setup includes a special multiplayer-capable PolyMod 1.3
preview. Stock 1.2.18 launch success is only a loader smoke test and must not be
treated as the intended multiplayer stack.

The bundled preview is a Mac-adapted managed DLL. The supplied adaptation removes
two attributes from one Deserialize postfix, disabling that patch registration;
method IL bodies and resources remain unchanged. This exact beta has now loaded
on Windows and initialized modded multiplayer. Do not copy the Mac native runtime.
Deception's online adapter pins preview SHA-256
`61588813d4b62d133db06fad64a2c440869aaffaf7460ec8bc8e9d82b1be63d7`.
The online compatibility fingerprint also contains a Mac-specific profile ID.
Changing only that ID or its hash guard would not establish compatibility.

The test peer is the user's Mac. Completion requires the correct beta loading on Windows, validated Windows
native persistence/command hooks, and a real two-client online match exercising
Deception actions, turn synchronization, save/reload/reconnect, and hidden
information. Do not claim cross-platform acceptance from lobby access. The package itself does not claim a passed
two-Mac/server acceptance test, so beta capability is not end-to-end validation.

The source baseline is 0.3.100, commit `8d0cf46`. The Windows ARM64 PC runs the
x64 game under emulation. On 2026-10-17 the candidate registered its native state
hooks, loaded all mod content, initialized beta multiplayer and reached StartScene.
The user then confirmed reaching the multiplayer lobby. No cross-platform match
has been observed yet. Ask the user to playtest; do not automate gameplay.

## Managed build preparation

Use Python 3.11+ and an existing .NET SDK. The SDK is only the compiler; references
come from the Windows game's bundled runtime, BepInEx and generated interop DLLs.
No `.csproj`, restore, game binaries in Git, or replacement runtime is required.

```powershell
# Optional when Python is not on PATH:
$env:DECEPTION_PYTHON = 'C:\path\to\python.exe'
.\project\run.ps1 windows-check --game 'C:\path\to\The Battle of Polytopia'
.\project\run.ps1 windows-compile --game 'C:\path\to\The Battle of Polytopia' --dotnet 'C:\path\to\sdk\dotnet.exe'
```

These modes go through `tools/manage.py`. Each writes a fresh ignored
`project/build/windows-*` directory. `windows-compile` compiles all production C#
and embeds the existing art/sound resources using the same Roslyn response-file
builder as the Mac build. Native Windows runtime DLLs are filtered using their
PE/CLI headers. Source and reference hashes are checked again after compilation.

The resulting DLL is **not installable**: it intentionally contains no native
bridge payload or admitted Windows profile. No `.polymod` archive or installation
receipt is produced. The original Mac check/build/install and runtime profile
guards remain intact. `windows-build.json` distinguishes inventory, compile
success and failure; compilation does not run gameplay or the model test suite.

## Native validation and private candidate

On 2026-10-17, MSVC 19.44 compiled the three Windows x64 helpers and their
harnesses on this PC. The audited pack passed all 67 state cases, 85 execution
assertions and 117 command checks, including exports and dependencies. This
required correcting one signed/unsigned comparison in the command harness.

`windows-probe` (same arguments as `windows-compile`) builds an optional BepInEx
diagnostic plugin, `Deception.WindowsPortProbe.dll`. It embeds the surveyed game
hash and only the method names from the seam contracts. Installation and game
launch are separate manual actions. It resolves generated-wrapper method
pointers, checks that they lie in the loaded game image, and writes a uniquely
named `BepInEx/deception-windows-probe-*.json` report. It never installs detours,
or invokes gameplay methods. With `--exception-probe`, it loads our hash-checked
diagnostic DLL to verify two native IL2CPP exception catches and three managed
callbacks. That live Windows test passed. Remove/disable the plugin after
collection. The observed run resolved all 16 methods and reached StartScene.
Addresses/prologues are diagnostic evidence, not an admitted runtime contract.

The emulated x64 runtime reports `OSArchitecture=X64` on this ARM64 PC; do not
interpret that field alone as proof of physical CPU architecture.

Build the three helpers with the existing `native-platform-build.py` runner and
MSVC x64 tools (see `project/src/Native/PORTABILITY.md`). Then audit the actual
Windows game method addresses, prologues, object layouts and exception boundaries
before registering a Windows profile and embedding its own seam resources.
Mac prologues and hashes must not be reused.

`windows-candidate` additionally requires `--native-pack`, `--mac-mod`, and
`--probe-report`. It checks the exact Windows game/beta, native source receipt,
16 on-disk method prologues, successful live exception probe and the original
Mac archive. It embeds separate Windows resources plus the verified original
Mac native resources. Both peers must use the **identical new archive**, whose
online fingerprint deliberately differs from the old Mac release. Ordinary
builds do not admit Windows; this candidate requires either the process environment
`DECEPTION_WINDOWS_PORT_TEST=1` or a package-bound installation approval on Windows.
The approval file is `BepInEx/config/deception-windows-candidate.sha256`, containing
the exact installed candidate's lowercase SHA-256. The checked launcher writes it
after validating all pinned inputs, allowing later normal Steam launches. Archive
replacement invalidates approval; all game/beta/native checks remain independent.

Windows startup initially hit a stack overflow. A local crash dump and targeted
trace established MSVC identical-code folding: `RevealCapitalAction.Execute`
and `Unit.set_Depth` share an empty RET, `InteractionBar.DeInit`/`OnDestroy`
share a body, and Disband/Explode share `IsValid`. Independent Harmony patchers
on those addresses produced recursive trampolines. The candidate excludes the
two empty-body hooks, merges the two cleanup registrations under one owner,
and merges validation while recovering the actual native command type. Disband
rules must never apply to Explode. Startup tests allocate isolated native command
objects to verify that dispatch without enqueueing or executing a command.
Unknown shared hook addresses fail closed. These changes are Windows-only.

`windows-tests --test-host <compiled coreclr-probe-windows.exe>` runs the existing
339 model tests and six online suites under the installed game CoreCLR, plus
candidate admission tests. The online suites passed 258/266/88/57/360/99
assertions respectively. Harness success is not gameplay acceptance.

One launch failed before plugins loaded with a BepInEx console StreamWriter
OverflowException; a subsequent launch succeeded. This remains an intermittent
loader/runtime observation, separate from the fixed hook recursion.

## Two-client playtest

Keep the supplied beta on both machines, game 2.17.3.16375, and only the matching
Deception candidate in each Mods folder. Preserve the old Mac archive outside
Mods. Use the existing Mac launcher; use the supplied candidate launcher on
Windows so the opt-in is process-local. Create a fresh private test match.

1. Verify both clients enter the same lobby and start the match.
2. Alternate ordinary moves and end-turns; compare units, stars and turn number.
3. Research Deception, recruit a Mimic, and test regroup/disguise on both peers.
4. Test a Spy/Fogger action and verify hidden information from the opposing view.
5. Quit/reopen each client, resume the match, and verify the same state and actions.
6. Stop on a mismatch or error; record the action and which client observed it.

Do not interpret matching lobby versions or fixture tests as successful hidden
information, command persistence or reconnect validation.

Regression checks:

```powershell
python -B project/tests/test_windows_build.py -v
python -B project/tests/test_native_platform_survey.py -v
python -B project/tests/native-build-0379/test_native_platform_build.py -v
```
