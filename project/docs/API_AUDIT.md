# M1D native API audit

235 unique method/accessor/constructor signatures match the exact supplied generated references, including the 149 baseline entries. See `evidence/native-api-inspection-m1d.json` and `tools/native-wheel-api.json`. This is read-only CLI metadata inspection; no assembly was loaded or native method executed.

The pinned references include the IL2CPP native clone, UInt32 unit identity/lookup, UInt16 current health, native UI/input/popups, and ordinary capture/ruins/disband eligibility. Generic CLI signature syntax is used only in the metadata contract; runtime reflection binds the constructed popup-button array's CLR FullName.

Two inherited Unity visibility methods were unavailable in the hosted reference set: `UnityEngine.Component.get_gameObject()` and `UnityEngine.GameObject.SetActive(bool)`. They are explicitly recorded as not checked here. The Mac build requires matching signatures in the existing `BepInEx/interop/UnityEngine.CoreModule.dll` and records its observed hash; install requires it unchanged. No expected historical hash was invented.

The queued popup's array constructor resides in `Il2CppInterop.Runtime.dll`, pinned to the already working compatibility file. Exported references are flat, but the Mac audit resolves this file from its existing `BepInEx/core` location. No dependency is moved, downloaded or replaced. The existing framework, config, API and launcher boundaries remain.

Production hooks use exact typed by-ref UnitState and UnitPopupData boundaries, plus reflection for native members. Detached IL2CPP display copies reject matching native pointers before any write. A managed wrapper clone would alias live state and is refused.

Metadata agreement is not C# compilation, Harmony ABI execution, Unity rendering or gameplay acceptance. The model and synthetic adapter suites are separate Mac build gates, followed by actual game tests. See `VALIDATION_M1D.md`.
