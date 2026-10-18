# Native bridge platform boundary

These three helpers contain only own-mod C++ control flow, POD arguments and supplied function pointers. They do not locate the game, install a detour, serialize game state or implement the CLR. Their native exception boundary is required: managed callbacks return status, then the native bridge calls the original native function and raises/catches native exceptions outside a CLR callback.

`deception-platform.h` isolates compiler export spelling and accepts x64 compiler macros (`__x86_64__` or `_M_X64`). It changes no function signature, POD offset, site count, callback lifetime or exception rule. Windows libraries use `__declspec(dllexport)`; harness consumers use `DCP_BRIDGE_IMPORT` / `__declspec(dllimport)`. Clang/GNU use explicit default symbol visibility. Other architectures still fail compilation.

## Reproducible native build kit (A39)

The new `CMakeLists.txt` builds three shared helpers and their three existing native harnesses. `project/tools/native-platform-build.py` configures Release, builds, runs CTest, checks complete test summaries, then audits each produced binary's format/architecture, exact exports, allowed system dependencies and SHA256. Only after those checks does it publish a fresh helper pack. It refuses existing output directories and linked artifact paths, and checks that source inputs remained unchanged during the build. It does not install anything, inspect saves, load a game library, download dependencies or admit a runtime profile.

Requirements already installed on the test machine:

- Python **3.11+**, CMake/CTest **3.21+**.
- Windows: x64 Visual Studio2022 C++ build tools and Windows SDK. Run in an **x64 Native Tools Command Prompt**, so `dumpbin` and the compiler environment are available. The default generator is `Visual Studio 17 2022`; an explicit `--generator` can select another installed MSVC-compatible generator. MinGW and cross-OS builds are rejected.
- Linux: x64 GCC or Clang with a C++17 standard library, build tool, `nm` and `readelf`.
- Mac: existing Apple Clang/Command Line Tools, `nm` and `otool`; x64 execution must work (Rosetta on an ARM Mac). The runner explicitly targets x86_64 and macOS11.0.

From the source-kit root (preserving its `project/` layout), run one target command. Both destination directories must be new:

```bat
py -3.11 project/tools/native-platform-build.py build --rid win-x64 --build-dir native-work/win-x64 --output native-packs/win-x64
```

```sh
python3 project/tools/native-platform-build.py build --rid linux-x64 --build-dir native-work/linux-x64 --output native-packs/linux-x64
python3 project/tools/native-platform-build.py build --rid osx-x64 --build-dir native-work/osx-x64 --output native-packs/osx-x64 --cxx /usr/bin/clang++
```

The standalone CMake equivalent is `cmake -S project/src/Native -B <fresh>`, `cmake --build <fresh> --config Release`, then `ctest --test-dir <fresh> -C Release --output-on-failure`. On Windows add `-G "Visual Studio 17 2022" -A x64` to configure; on Mac add `-DCMAKE_OSX_ARCHITECTURES=x86_64 -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0`. Use the Python runner to produce a verified pack and provenance, rather than copying a library from an unchecked configuration.

Expected CTest results: **state67 cases, execution85 assertions, command117 checks**, all passing. Windows abort checks launch isolated copies of the state harness up to a specific deterministic probe; they require the same deliberate abort outcome as the existing POSIX `fork` checks. These are mock C++ callbacks/originals, never Unity/IL2CPP or CLR callbacks.

### Artifacts to return from a Windows test

Return the generated `native-packs/win-x64` directory, and these files from `native-work/win-x64`: `build.log`, `ctest.xml`, `artifact-index-Release.json`, `CMakeCache.txt` and `Testing/Temporary/LastTest.log`. If a build fails, return the build log and CMake configure log (when present). No game/save files are needed for this first helper test. A separate, read-only target-game survey is needed before runtime admission.

Pack layout:

```text
native-pack.json
runtimes/<rid>/native/<helper filename>
runtimes/<rid>/native/<helper filename>.sha256
```

Each of the three helper filenames is `deception-<kind>-bridge.dll` on Windows, `libdeception-<kind>-bridge.so` on Linux, and `libdeception-<kind>-bridge.dylib` on Mac. The manifest records compiler, target, source hashes, native test summaries, exports and dependencies. `runtimeAdmitted` is always **false**. A portable read-only content check is `python3 project/tools/native-platform-build.py audit <pack-directory>`; this verifies integrity/shape, not publisher authenticity or IL2CPP compatibility. No game hashes/seam profiles or framework binaries are copied into the pack.

### Exception and runtime constraints

MSVC builds use `/EHs /EHc-` on both helpers and harnesses, removing CMake's default `/EHsc`. `/EHc` assumes `extern "C"` functions cannot throw; these exported entries intentionally propagate original native C++ exceptions. Dynamic Release CRT (`/MD`) is used consistently; neither a private static CRT nor Debug CRT is packaged. GNU/Clang use `-fexceptions`, native threads and hidden implementation symbols, with only the specified C entry points exported. The actual target game's C++ runtime/exception representation must still be verified independently. Do not use `/EHa` or swallow OS faults to conceal an incompatible exception boundary. [Microsoft exception handling options](https://learn.microsoft.com/en-us/cpp/build/reference/eh-exception-handling-model?view=msvc-170), [CMake runtime-library selection](https://cmake.org/cmake/help/latest/prop_tgt/MSVC_RUNTIME_LIBRARY.html).

## What has been verified

- macOS x64: actual existing Clang builds, exports/dependencies and all three standalone native control-flow harnesses; A39 additionally exercises CMake/CTest and the audited helper-pack pipeline.
- Windows x64: Clang's actual Windows-target parser accepts the header's declaration syntax only.
- Linux x64: the 2026-09-23 cloud run compiled all three helpers with GCC 13.3.0
  on Ubuntu 24.04 x86-64 using CMake/CTest 4.4.3. Native mock harnesses passed
  67 state cases, 85 execution assertions and 117 command checks. Exact exports,
  allowed ELF dependencies and the unadmitted helper pack passed their checks.
  See the returned results package for full logs and the initial GCC failures.
- ARM: the header deliberately rejects it.

No Windows compiler/runtime or actual Windows/Linux target-game installation
was available for this cloud task. Windows CI is prepared but unexecuted.
Linux helper execution does not establish IL2CPP exception compatibility,
gameplay, save/load, multiplayer, or support for other Linux runtime images.
Header syntax and synthetic binary tests do not supply that missing evidence.

## One-package direction

The managed `NativePlatformContract` selects one explicitly admitted OS/process-architecture profile before extracting or loading native code. A profile owns the exact game path/hash and bridge resource/seam identities. `NativePlatformLibrary` performs bounded digest checks, platform/content-addressed own-helper extraction, and native module identity checks. The existing three embedded resource names are retained for the admitted Mac profile; future profiles require distinct resource identities inside the same Deception assembly/archive.

Only the existing `osx-x64` profile is admitted. Windows/Linux image-resolver implementations are inactive preparation, not runtime support. They cannot bypass the profile registry. No game binaries, loader repairs or framework DLLs should be copied into the mod archive.

Before admitting another platform, obtain its actual game and generated-wrapper metadata, build own helpers with its toolchain, verify exported symbols/dependencies and native exception propagation through its IL2CPP runtime, pin exact game hashes and serializer/command prologues, then test detached/native action paths and saves/load on a clean working PolyMod installation. Mac Rosetta process architecture is X64 even on an ARM host; native ARM execution remains unsupported.

Do not relax a hash, ABI or unknown-platform failure merely to make the mod load.
Online synchronization source exists from 0.3.83, but actual two-Mac/server and
cross-platform acceptance remain unverified. `NativeOnlinePreview.cs` retains
its Mac profile and loader hash guard; helper tests cannot admit other targets.
