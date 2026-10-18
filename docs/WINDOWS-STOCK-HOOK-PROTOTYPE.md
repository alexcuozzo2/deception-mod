# Stock Windows hook prototype

18 October 2026. Branch `codex/windows-stock-hooks`. Experimental local test;
not approval of an unmodified PolyMod stack or multiplayer.

## Findings and implementation

The expanded read-only Windows probe resolves all available non-generic methods
in GameLogicAssembly and PolytopiaAssembly. The offline audit decodes the original
GameAssembly file, not potentially patched process memory. The pinned x64 Dobby
implementation overwrites at least six bytes, rounded to whole instructions.

450 overloads referenced by literal Native.M calls resolved; 29 were flagged for
review, including ordinary calls and short-return getters. This is intentionally
not an installed-hook census. 175 references were unresolved by this game-only
inventory, largely Unity/managed APIs and generated field accessors. Dynamic
targets require separate review. All 16 previously verified native-bridge seam
entry windows passed the known-hazard scan. The C++ helpers install no detours
themselves; their managed bridge owners do.

The known crashing hook is TileData.HasImprovement. The tempting alternative,
intercepting HasAbility(Fortify), does not work on this Windows build: native
GetDefenceBonus inlines that ability-list query.

The Windows-specific Rogue defence prefix therefore replaces only the Rogue
calculation. It preserves the native player terrain-defence query and HasEffect
query. Before game-state version 115 poison returns 7 early; from 115 it halves
the final terrain/base result. Base defence is 10 and terrain defence is 15 when
the native player query exceeds 1. The city bonus is omitted, matching the old
scoped city-query veto. Ordinary units continue through the native method.
No temporary world-state mutation is used. Mac retains its existing path.

NativeWindowsHooks still requires the exact reviewed game image. Its additional
113-entry deny-list refuses the known short-Jcc, near-Jcc, short-JMP and loop
hazards discovered in the full game-method map. It is a fail-closed check for
these known entries, not a general relocator verifier or coverage of engine
methods outside this map. Do not loosen the game pin on update.

## Validation

- Rogue fixtures: 99 cases, 226 assertions passed, including 36 new combinations
  of version, poison, terrain and city ownership plus ordinary-unit bypass.
- Existing rendering fixtures passed with the earlier sprite fix retained and
  additional read-only overlay diagnostics.
- Offline decoder: five tests passed covering actual branch opcodes, immediates
  containing branch-like bytes, instruction boundaries and truncated input.
- Full Windows candidate compilation passed; existing native helpers reused.
- Normal Steam launch with stock Dobby reached the start scene; logs confirm
  Deception Load completed, native state bridges installed and registry ready.
- User playtested the stock-Dobby candidate and reported functionality basically
  working; ready Spy/Rogue whiteout reproduced. Save/reload, resume, resign and
  multiplayer were not individually confirmed.

Private test candidate SHA-256:
`60524e01bbc8bcf22b3fef494526758aa10da13ffa492f860f61ae3fb89509e5`.

Installed stock Dobby SHA-256:
`8015de7d867245a1095d13947a63763878e4cf5fd3d3089b63cc39200b055ded`.

This first isolated test retains the previously adapted beta DLL
`61588813d4b62d133db06fad64a2c440869aaffaf7460ec8bc8e9d82b1be63d7`.
It cannot be described as clean upstream-beta compatibility. A subsequent test
must use the unmodified beta and its matching compatibility path, then the public
loader separately. Shared packaging was not changed; Mac still owns the final
combined artifact.

## Rendering follow-up

The user reported white ready-state Spies/Rogues while still running the previous
sprite-fix archive (14f72044...) with patched Dobby. This is not a stock-Dobby
regression. Its log captured white overlay strength 2.5987892 as well as 0 and
0.4. The candidate logs both property-block and wrapper-cache values to identify
the source before changing rendering behaviour. No speculative clamp was added.

The stock-Dobby playtest then captured strength 4.79392E+19 on both Spy and
Fogger sprite-block paths, with no associated Polytopia wrapper cache. Native
inspection shows ready overlay strength is bounded by fixed status rules, not
such enormous values. The readiness adapter previously obtained strength from
the generated Color/float ValueTuple return of GetOverlayColorAndStrength.
That interop boundary is the leading suspect, not yet a separately isolated
native-versus-wrapper comparison.

The next Windows-only workaround avoids that return boundary: no status = 0;
Boosted = .5, Swift = .4, Poisoned = .3, Frozen = .5, Petrified = .6, with later
entries overriding earlier ones exactly as the pinned native function does.
Native status colours and all ownership/readiness guards remain. Mac retains its
existing generated-wrapper path. This is a calculation replacement, not a clamp
of potentially corrupt values. No new hooks or native helpers are introduced.

Readiness fixtures: 94 cases / 288 assertions passed, including all 128 masks
of effect IDs 0 through 6 and the actual Windows prefix with a failing tuple
wrapper. The rebuilt candidate compiled and was installed with stock Dobby:
`62dafbb28e633a021b738bbcf5f851e356162d5760495f43714729a77e55f071`.
User confirmed normal Spy/Rogue colours before and after moving in this
follow-up playtest (18 October 2026). Fogger was optional in the request and
was not separately confirmed. This supports the Windows readiness workaround;
it does not separately prove the underlying interop defect. The stock-Dobby
functional playtest and this visual check passed. Subsequent clean-beta results
are recorded below; public loader, resign and cross-platform multiplayer still
need their own acceptance checks.

## Unmodified multiplayer beta startup

18 October 2026: replaced only the adapted PolyMod DLL with verified upstream
beta SHA-256 `a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016`.
Retained stock Dobby `8015de7d...055ded` and exactly the playtested mod archive
`62dafbb28e633a021b738bbcf5f851e356162d5760495f43714729a77e55f071`.
Normal Steam launch logs confirm PolyMod 1.3.0-pre-android-multi-1, Deception
candidate armed, clean-beta compatibility installed and the exact incompatible
reader callback filtered once. The DLL itself is unmodified; Deception still
applies its documented guarded runtime shim. This is startup evidence, not
confirmation of all gameplay or multiplayer paths.

User reported the requested clean-beta playtest working overall: fresh gameplay,
Subvert/Rogue combat, End Turn, menu/resume and application restart/save loading.
They explicitly confirmed the multiplayer lobby is reachable. They did not test
resign, explaining this was a single-player match against bots. These are
user-reported results, not an automated or exhaustive gameplay certification.
Resign and a Mac-Windows multiplayer match remain unverified.

The archive approval marker is still in
use for this existing candidate; ordinary marker-free installation is a separate
remaining gate. No shared build or packaging scripts were changed.

## Marker-free installation preparation

Integrated the earlier PR #5 runtime-admission implementation, retaining the
stock-Dobby hook workarounds and deny-list. Admission now accepts only the exact
playtested clean beta, stock Dobby and pinned game/interop/Harmony libraries.
The public loader and historical adapted beta are not admitted by this profile.
The loaded DLL/module hashes are checked before gameplay hooks; helper payload
and seam checks remain. Neither archive filename, approval marker nor
DECEPTION_WINDOWS_PORT_TEST is used to admit the Windows runtime.

Admission tests passed, including wrong dependency hashes, adapted/public loader
rejection, unsupported architecture, no test-flag bypass, normal launch without
opt-in and unchanged Mac resource selection. Full compilation passed.
The unchanged Windows builder uses isolated copies of its verified historical
build dependencies; the actual installed clean-beta stack stays unchanged.
Build-input and runtime-admission profiles are distinct and retained in receipts.
No shared packaging/build scripts or native helper sources were changed.

Marker-free startup passed on 18 October 2026. Preserved the prior archive and
approval marker outside the game, installed only Deception.polymod in Mods, and
launched through Steam. No saved user/machine test opt-in was present; the launch
process opt-in was cleared. The approval marker was confirmed absent afterward.
Logs confirm windows-runtime-compatibility, clean-beta compatibility/filter,
Deception loaded and registry-ready for 16 tribes. The clean beta and stock Dobby
were unchanged. Native helper binaries were reused.

Installed archive SHA-256:
`2000a3b67d12c29b9b8c06b7609834e19fe44f639ffb47e4ed5a8799c176fccc`.

This proves ordinary Mods-folder startup on this existing verified Windows
installation, not a fresh-machine loader setup or cross-platform multiplayer.
User confirmed the brief in-game check passed: Deception research, normal Spy
colours and End Turn. They reported transient Rogue city flames separately.
Both machines
must use these exact bytes or the same subsequent Mac-produced artifact for
the coordinated multiplayer test.

## Rogue city-flame follow-up

Superseded by the persistent-siege correction below. The user confirmed the
initial flame-removal playtest worked, then clarified that flames should remain
visible for the full duration of actual income denial.

The existing suppression used StopFire(false)/StopRainbowFire(false), allowing
particle fade-out, and depended exclusively on the model tile occupant. The
movement reaction can vacate the source while its rendered unit still exists.
Cleanup now requests immediate stop for Rogue cities and recognizes an exact
registered departing Rogue visual when the model tile is empty. A new ordinary
occupant takes precedence; detached visuals, foreign tiles and non-city effects
retain native handling. Existing economic siege rules were not changed.

52 visual cases / 63 assertions passed. Both added StopFire/StopRainbowFire hook
entry windows passed the stock-Dobby known-hazard scan; compilation passed.
Native helpers reused and marker-free installation retained. Native movement
playtest of this change remains pending.
Installed archive SHA-256:
`d1ce359d32f28e04fd0ba9271191925ce69f7aae4e2bcf877be60d6f6d5e9a20`.

## Persistent Rogue siege visuals

User clarified the intended rule: preserve income denial and show continuous
siege flames until the Rogue returns at its original owner's turn. Removed all
four custom flame spawn/stop hooks. The settled-state Rogue visual refresh now
calls Tile.RenderImprovement(), whose native implementation queries the same
TileData.IsBeingCaptured predicate as the siege rules. This keeps native flame
style and lifetime handling, instead of suppressing it after each update.
The existing refresh includes newly acquired and returned Rogue IDs, so the
city is refreshed when the ledger changes. Hidden/replay/stale-state guards
remain; authoritative gameplay and income calculations are unchanged.

44 visual cases / 56 assertions passed, covering sustained stationary siege,
source vacancy, return removing the Rogue effect, and preservation of a real
ordinary enemy siege after return. Full candidate compilation passed; native
helpers reused. Installed without a marker. On 18 October 2026 the user confirmed
the requested playtest passed: flames persist during Rogue city occupation,
clear on departure, and clear when the original owner's turn starts and control
returns. The user then closed the game. This is user-reported visual/gameplay
acceptance on Windows; cross-platform multiplayer and resign remain unverified.
Archive SHA-256:
`509a14380a3e87e4e00900e0d156f44edb6321015202fc08a1bfd0492491e5f1`.

## Reproduction tools

`project/tools/WindowsPortProbe.cs` now includes all_methods in its private JSON.
Compile through the existing windows-probe command. No gameplay hooks are added
by that plugin. Keep the output private; remove the plugin after inventory.

Install Capstone 5.0.9 and pefile 2024.8.26 into an isolated build environment.
Run `project/tools/audit_windows_prologues.py` with --game pointing to the pinned
GameAssembly.dll, --inventory to the full probe JSON, --source to src/PolyMod and
--output to an ignored build path. The committed deny-list was produced by
applying the same inspect function to every all_methods entry and deduplicating
RVAs with relative-branch, loop-family or incomplete-decode hazards. No game
instruction bytes or full inventories are committed.
