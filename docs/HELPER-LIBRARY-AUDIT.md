# PolyMod / Polibrary migration audit

Read-only source audit, 18 October 2026. No library installed, no production adapter replaced, and no native or multiplayer acceptance inferred from source inspection.

## Conclusion

There is a concrete supported-library alternative worth prototyping for custom commands and reactions. It is not established that Deception needs its present native bridges forever. Equally, replacing them wholesale now would discard save/replay and failure-handling guarantees without evidence of an equivalent replacement.

Deception is already a compiled C# mod. “PolyScript” is not a restriction to data-only mods. The distinction to investigate is framework/library APIs versus Deception's low-level engine interception, not C# versus PolyScript.

## Examined upstream sources

- [PolyMod v1.2.18 PolyScript](https://github.com/PolyModdingTeam/PolyMod/blob/v1.2.18/src/Api/PolyScript.cs): load/unload lifecycle, configuration and logger.
- [PolyMod v1.2.18 audio manager](https://github.com/PolyModdingTeam/PolyMod/blob/v1.2.18/src/Managers/Audio.cs): custom sound registry hooks exist, but `BuildAudioClip(byte[])` is explicitly a placeholder. Do not replace working smoke audio with that method.
- [Polibrary README](https://github.com/Polibrary-Team/Polibrary/blob/3cc8e9fd26946f362f5c0df302f28bbda23bea1c/README.md): source identifies version 2.1.4; users must install Polibrary alongside the mod and declare it as a required dependency. This is a separate dependency, not automatically part of PolyMod.
- [Polibrary CAR](https://github.com/Polibrary-Team/Polibrary/blob/3cc8e9fd26946f362f5c0df302f28bbda23bea1c/src/PolyScript%20API/CAR/CAR.cs): `RegisterCommand`, `RegisterAction`, `AssignReaction`, JSON enum mappings, class injection, game command/action factories, command serialization hooks and reaction factory hooks.
- [Command base](https://github.com/Polibrary-Team/Polibrary/blob/3cc8e9fd26946f362f5c0df302f28bbda23bea1c/src/PolyScript%20API/CAR/PolibCommandBase.cs): custom `ExecuteNew`, `SerializeNew`, `DeserializeNew`; implementers must supply validation and payload handling.
- [Action base](https://github.com/Polibrary-Team/Polibrary/blob/3cc8e9fd26946f362f5c0df302f28bbda23bea1c/src/PolyScript%20API/CAR/PolibActionBase.cs): custom action execution and binary serialization.
- [VFX manager](https://github.com/Polibrary-Team/Polibrary/blob/3cc8e9fd26946f362f5c0df302f28bbda23bea1c/src/PolyScript%20API/VFX/VFXManager.cs): custom puff registration, sprite/size mappings and camera shake.
- [Save_Obsolete.cs](https://github.com/Polibrary-Team/Polibrary/blob/3cc8e9fd26946f362f5c0df302f28bbda23bea1c/src/Save_Obsolete.cs): the historical sidecar-save implementation is commented out. It is not evidence of a current supported arbitrary game-state persistence API.

Polibrary commit was pinned during this audit. This does not establish that its source, verified catalogue binary and the supplied multiplayer beta are mutually compatible.

## Coverage and concrete migration candidates

| Area | Evidence / proposed direction | Remaining proof |
|---|---|---|
| Stats, names, costs, icons, unit/technology definitions | Standard PolyMod data and registry facilities are the natural first choice; current mod already uses loader data hooks. | Preserve all-tribe overrides, enum identity and old saves. |
| Custom action/command construction | Polibrary CAR provides real registration and injected command/action types. Prototype one isolated action through CAR before changing the current Build/Train carrier approach. | Native invocation, factory round trips, replay, stable mapping across peers, validation failures, and compatibility on both loader versions. |
| Effects and reactions | CAR reactions and VFX puff helpers could reduce custom presentation glue. | Visibility/replay guards, impact-time sound, disposal and ownership secrecy remain Deception responsibilities. |
| Smoke sound | Official public audio decoder is unfinished. Current `SmokeGrenadeAssets.cs` decodes PCM and calls Unity's generated injected methods. | Test ordinary Unity audio entry points on the supported interop version before removing the span workaround. A native DLL is not intrinsically necessary for a sound. |
| Persistent disguises, Blackmail, Rogue state | Current `NativeGameStateEnvelope` attaches a versioned ledger to the exact native state and frames serialization through `NativeStateBridge`. | No equivalent active whole-game persistence API established in inspected Polibrary files. Ask maintainers for their current approach; do not substitute the obsolete seed-keyed sidecar. |
| Native execution / exception boundaries | `NativeStateBridge` deliberately leaves native trampolines and native exception propagation to a native library, while C# callbacks return status. | A library action that succeeds once does not prove failure paths, rollback, cloning and save/replay are safe. Retain these gates during migration. |
| Multiplayer | CAR command serialization is relevant but is not an online protocol compatibility guarantee. | Inspect the supplied clean beta and validate command/state round trips on two matching clients. Public-loader support should remain local unless upstream explicitly supports online. |

## Recommended implementation sequence

1. Keep public and clean-beta loader profiles separate, identified by exact DLL hashes. Do not patch or redistribute the beta to obtain compatibility.
2. Build a separate disposable CAR proof mod: one command with actor/target identifiers, one deterministic state mutation and one reaction. Do not install Polibrary alongside production Deception's competing serializer hooks.
3. Exercise execute, reject, serialize/deserialize, save/reload, replay and two-client beta execution. Run the same proof with public PolyMod for local play. Confirm supported Polibrary/game/interop versions with maintainers.
4. Obtain a supported arbitrary game-state extension API, or retain our narrowly scoped envelope until one exists. Define migration for existing Deception saves before switching formats.
5. Move one Deception action at a time behind an adapter, comparing canonical model outcomes. Remove the associated native seam only after equivalent failure and lifecycle checks pass.
6. Evaluate VFX/audio simplification independently; these should not block the command/persistence audit.

## Questions for PolyMod / Polibrary reviewers

- Which API should own per-match custom state, including game clones, replay and multiplayer uploads?
- Is CAR the recommended API on the supplied 1.3 preview? Are injected custom action types and enum IDs transmitted/reconstructed deterministically?
- What is the supported behavior for validation/execution exceptions crossing IL2CPP calls?
- Is there a working supported custom audio loader replacing the v1.2.18 placeholder?
- Which public runtime/interops are supported on macOS and Windows, and can the compatibility fixes be upstreamed separately from Deception?

These are specific migration questions, not claims that the gameplay features are impossible in PolyMod.
