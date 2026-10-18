# Native-facing API audit — recorded inputs, not game execution

The source is aimed at the exact previously supplied PolyMod 1.2.18 and generated game assemblies. Their hashes and the eleven play-tested compatibility files are pinned in `tools/host-contract.json`. No binaries from that reference set are redistributed here.

## Verified from the supplied managed assemblies

1. PolyMod's assembly loader recognizes a public static `Load(BepInEx.Logging.ManualLogSource)` entry point in a mod-contained DLL.
2. `PolyMod.Loader.ProcessGameLogicData(Polytopia.Data.GameLogicData, Newtonsoft.Json.Linq.JObject)` is a managed method. Its pipeline performs custom mappings after our prefix point; `idx = -1` is assigned a unique custom index by `CreateMappings`.
3. The raw game-data categories include `techData` and `tribeData`. Tribe-specific `techOverrides` can map Basic to another root. Generated `TribeData.Category` constants are Hidden=0, Human=1, Special=2; the adapter also accepts the explicit Human string form in source JSON.
4. Managed `PolyMod.Managers.Main.TechView_CreateNode(TechView,TechData,TechItem,float)` computes root spacing from 360 / effective Basic-root child count, rather than a literal five. It walks the supplied root's children, which is why the new managed prefix normalizes an incoming raw Basic root to the same effective override used for spacing.
5. The exact wrapper accessors used are `GameManager.GameState`, `GameManager.LocalPlayer`, `GameState.GameLogicData`, `PlayerState.tribe`, `TribeData.category`, `TechData.type`, `GameLogicData.GetTribeData(TribeType)` and `GetOverride(TechData,TribeData)`.
6. Generated native JSON proxies expose `JObject.Parse(string)`, the string `Item` indexer and an inherited/new-slot `ToString`. `Il2CppSystem.Object.ToString` is a new virtual slot; using ordinary `System.Object.ToString()` on a proxy may yield a type name. `NativeJson.Read` explicitly finds and calls the proxy slot instead of assuming an override.
7. The supplied Harmony library has the six-parameter `Harmony.Patch` overload, `HarmonyMethod(MethodInfo)` and `UnpatchSelf`. This package patches the managed PolyMod helpers with Harmony, not another native game function.

These findings establish names and implementation paths, not actual success of a new hook under the user's runtime. Reflection target contracts are checked at load time. The existing Mac signature/ABI limitations have not been declared globally repaired by this project.

## Primary documentation consulted as a cross-check

- PolyMod: 4. PolyScripts — https://github.com/PolyModdingTeam/PolyMod/wiki/4.-PolyScripts
- PolyMod: 2.2 TechData — https://github.com/PolyModdingTeam/PolyMod/wiki/2.2-TechData
- PolyMod: 3. GLD patching — https://github.com/PolyModdingTeam/PolyMod/wiki/3.-GLD-patching

Documentation is supporting API context only. The attached DF-1/v1.5 spec remains authoritative for gameplay. No unsupported game rule was filled in from a wiki.
