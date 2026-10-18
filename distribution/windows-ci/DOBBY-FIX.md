# Windows tribe-selection crash

The full local crash capture identifies `TileData.HasImprovement`, called by
`Tile.Render` while the tribe popup takes its background snapshot. Dobby expands
an x64 short conditional branch from two to six bytes but does not adjust the
relative displacement for the extra four bytes. The captured trampoline targets
GameAssembly RVA `0x4fe78c` instead of `0x4fe788`, hitting INT3 padding.
Its short unconditional jump also truncates the new displacement to eight bits.
A follow-up audit found near Jcc (0F 8x rel32) copied without relocation; the
patch now adjusts that displacement too.

This fix changes the Windows hook library, not the multiplayer beta or Deception
archive. It does not establish gameplay or multiplayer acceptance.

## Build

Use an x64 VS 2022 Native Tools prompt with CMake, Git and Windows SDK installed.
Set `DECEPTION_REPO` to this checkout's absolute path. Use a new Dobby checkout
outside this repository; generated binaries stay out of Git.

```bat
git clone https://github.com/BepInEx/Dobby.git Dobby
cd Dobby
git checkout --detach 9d271803881f467aabfc24593c7dcaeb710fe861
git apply --check "%DECEPTION_REPO%\distribution\windows-ci\dobby-short-branches.patch"
git apply "%DECEPTION_REPO%\distribution\windows-ci\dobby-short-branches.patch"
cmake -DCMAKE_SYSTEM_PROCESSOR=amd64 -DCMAKE_SYSTEM_NAME=Windows -DCMAKE_BUILD_TYPE=Release -DDynamicBinaryInstrument=OFF -DPlugin.SymbolResolver=OFF -A x64 -B build64
cmake --build build64 --config Release --target dobby
cl /nologo /O2 /EHsc /MT "%DECEPTION_REPO%\distribution\windows-ci\dobby-branch-test.cpp" /Fe:dobby-branch-test.exe
dobby-branch-test.exe "%CD%\build64\Release\dobby.dll"
```

The synthetic test checks all 16 short and 16 near conditional branches and short JMP in both
directions, compares original/trampoline/hooked results for eight input pairs,
and verifies restoration after removing each hook. It checks branch targets
before executing them so the defective baseline can be tested without jumping
into arbitrary memory. This covers external branch targets; it is not a general
proof of every instruction relocation supported by Dobby.

Observed on this Windows ARM64 PC running x64 code: stock library **0/34 passed**;
first short-branch patch **34/34 passed**. The expanded test caught 32 failing
near-branch cases in that first patch. The current library passes **66/66**. MSVC 19.44, Release x64. No game data in the test.

## Install and test

Quit Polytopia. Preserve the current `BepInEx/core/dobby.dll` outside the game,
then copy `build64/Release/dobby.dll` to that exact location. Keep PolyMod.dll
and the Deception archive unchanged. Launch normally through Steam and playtest
tribe selection, then a fresh game with Deception research, then the private
Windows–Mac match. Mac keeps its existing native runtime and must **not** install
this Windows DLL. Both peers still use the same Deception archive.

Local stock SHA-256:
`8015de7d867245a1095d13947a63763878e4cf5fd3d3089b63cc39200b055ded`

Local patched SHA-256 (build timestamps can change a rebuild's hash):
`d9c38d52ffb4a06a1f807705d855b0a0b4b6578a68820d247f4db290790e4bfe`

The supplied test launcher pins the latter local artifact. Rebuilders must run
the regression test and deliberately update the launcher pin for their artifact.
To roll back, quit the game and restore the saved library; the known crash returns.
Redistributed builds must include Dobby's Apache-2.0 LICENSE.
