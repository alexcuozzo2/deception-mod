# Deception

**Bluff armies. Steal information. Turn your opponent’s units into liabilities.**

Deception adds a new technology branch to **The Battle of Polytopia**, built around disguises, espionage and information warfare. A frightening army might be a handful of fragile Mimics. An enemy unit might be working for you. Even explored territory can disappear behind fog again.

<p align="center">
  <img src="project/content/unit-art/propaganda_mask.png" width="210" alt="Deception's multicoloured disguise mask artwork">
  <img src="project/content/unit-art/intelligence_icon.png" width="210" alt="Surveillance tower artwork">
</p>

## What it adds

### Mimics and disguises

Recruit **1-star Mimics** with just **1 HP** and no damaging attacks. Use **Regroup** to copy a nearby unit’s appearance, with the option to swap positions. Mimics inherit their disguise’s movement through **Mimicry**—but looking powerful does not make them powerful.

Research **Propaganda** to unlock **Disinformation**, allowing otherwise eligible appearances without their technology, and **Disguise**, which transfers an appearance to an allied unit. Ordinary disguised units retain their real movement capabilities; an incompatible move sheds the disguise. You can also shed it deliberately.

Mimics can **Smuggle** resources for their home city and imitate the presentation of support effects through feints. Aquarion gets the amphibious **Mermimic**.

### Spies and Blackmail

**Spies** use **Subvert** to temporarily command an enemy unit as a Rogue. They have 2 movement, but no Dash or innate invisibility: choose whether to move or act. Aquarion recruits **Spymaids**.

**Blackmail** a visible enemy unit for **2 stars**. Each application redirects **1 star per turn** from its supporting city to you. Apply it again on later turns to increase the pressure, up to the city’s available income. The opponent must decide whether keeping that unit is worth the cost.

Blackmail persists when the unit enters fog without revealing its location. Losing or converting the unit, changing its supporting city’s owner, or making peace ends the effect.

### Reconnaissance and Surveillance

**Reconnaissance** marks technologies known to opponents you have met on your technology tree. **Surveillance** lets you toggle a view of an opponent’s fog, using a translucent tribe-coloured overlay.

### Foggers and Trade Secrets

**Foggers** are flying support units with Dash, Scout and **Blackout**. Move into position, then throw up to two smoke grenades to cover tiles with fog again. They cannot deal damage.

**Trade Secrets** earns **1 / 2 / 3 stars per turn** for each tier 1 / 2 / 3 technology a met opponent owns that you do not. Income stacks across met opponents.

## Technology branch

```text
Deception — Mimics
├── Propaganda — Disinformation + Disguise
│   └── Espionage — Spies + Blackmail
└── Intelligence — Reconnaissance + Surveillance
    └── Counter Intelligence — Foggers + Trade Secrets
```

## Download and installation

**[Download Deception 0.3.105 (.polymod)](https://github.com/alexcuozzo2/deception-mod/releases/download/v0.3.105/Deception-0.3.105-mac-windows.polymod)** — one shared **Windows/macOS WIP release**.

1. Install Polytopia **2.17.3.16375** and obtain **PolyMod 1.2.18** from [its official release](https://github.com/PolyModdingTeam/PolyMod/releases/tag/v1.2.18). You need a working PolyMod setup for your platform first; this mod does not install the loader or Mac compatibility runtime.
2. Quit Polytopia and download the `.polymod` above. **Do not extract it** or use GitHub's source ZIP as the mod.
3. Put the file in your game's `Mods` folder. Replace your previous Deception file and keep only one Deception version enabled.
4. Launch through your existing PolyMod setup.

[Release notes and checksum](https://github.com/alexcuozzo2/deception-mod/releases/tag/v0.3.105) identify the exact tested file. No loader, game or runtime libraries are bundled. You do not need the multiplayer beta for local play.

## Status and compatibility

**Work in progress, released for community testing and PolyMod review.**

| Platform / loader | Evidence and scope |
| --- | --- |
| Public PolyMod **1.2.18** | Local/pass-and-play target. Exact release reached the Mac menu; Windows startup and user-reported gameplay checks passed. |
| Multiplayer beta **1.3.0-pre-android-multi-1** | Compatible profile included. Exact release passed Windows startup and user-reported smoke testing. Earlier 0.3.104 passed Mac gameplay; 0.3.105 Mac beta startup stalled during one test. |
| Windows | Tested x64 game/runtime with stock Dobby, including on Windows ARM64 emulation. Version-specific dependency checks remain. |
| macOS | Existing Intel/Rosetta PolyMod compatibility setup required. This download is not a native Apple Silicon loader. |
| Linux | Unsupported by this release. |
| Multiplayer / resign | Real Mac–Windows multiplayer and explicit multiplayer resignation are not yet verified. Multiplayer requires the supported beta and identical mod versions on all clients. |

**Known issue:** startup can occasionally stall on a black screen. Quit normally and retry. The cause remains unresolved; do not replace runtime libraries or bypass compatibility checks to work around it. Report persistent failures.

See [compatibility and review details](docs/PUBLIC-POLYMOD-REVIEW.md) and [Windows validation results](docs/WINDOWS-SHARED-0.3.105-RESULTS.md). Automated checks and user playtesting do not cover every gameplay interaction.

## Feedback

Bug reports and balance feedback are welcome. Include:

- Game, PolyMod and Deception versions; operating system.
- What you did, what you expected, and what happened.
- Whether it was single-player, pass-and-play or online.
- A screenshot or short recording when useful.

Share only the relevant log excerpt after checking it for personal information. Do not post credentials, private match data or full diagnostic archives publicly.

## Development and technical review

Deception is a compiled C# PolyMod mod with custom art/audio, model tests and platform-specific native helpers. Those helpers currently handle parts of command execution and persistent game state. The clean beta DLL is not modified on disk; an exact-version runtime filter handles one incompatible serializer registration.

We are investigating supported PolyMod/Polibrary APIs to reduce that custom integration. See the [helper-library audit](docs/HELPER-LIBRARY-AUDIT.md) for concrete alternatives and remaining gaps.

- [Contributing and build workflow](CONTRIBUTING.md)
- `project/src/` — gameplay, adapters and native helper source
- `project/content/` — mod definitions, localization and assets
- `project/tests/` — model, adapter and native-boundary tests
- `project/spec/` — design decisions and amendments
- `project/tools/` — guarded build and installation tooling
- `distribution/` — setup tooling and platform notes; historical instructions may be superseded

## Credits and rights

Mod design, supplied artwork and audio: **nanobot**. Implementation developed with AI assistance.

The Battle of Polytopia belongs to its respective rights holders. PolyMod and other dependencies belong to their authors. Deception is an unofficial community project, not an official Midjiwan or PolyMod release.

No blanket open-source license has been selected for this repository. Public visibility alone does not grant permission to redistribute third-party material. See [publication notes](PUBLICATION.md) for omitted native catalogs, local build requirements and asset provenance.
