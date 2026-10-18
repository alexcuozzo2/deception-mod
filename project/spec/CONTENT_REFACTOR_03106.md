# Declarative content refactor — 0.3.106

Requested by Alex after PolyMod maintainer review: use ordinary patch.json definitions and separate assets so reviewers can inspect content without decompiling a DLL. Follow the official PolyMod wiki, checking public 1.2.18 and the supplied clean beta implementation where the wiki describes an older game.

No balance changes. Preserve all unit stats, ordinary ability lists, research unlocks, native identifiers, saved numeric mappings, custom commands and Mac/Windows fixes. User performs native gameplay tests on both platforms before promotion. Do not replace the current public release automatically.

patch.json owns all 64 custom unit rows, five new technologies and Aquarion replacements. The guarded Basic root insertion keeps the established branch position. Dynamic Smuggle and command carrier improvements still retain native-derived fields; they are internal transport, not new playable units. Custom mechanics remain code. All author PNG/WAV assets are loose files in the archive; no production artwork/audio embedding. Native helper binaries and ABI contracts are unchanged.

First GLD parse must validate the loader-merged patch, reconstruct historical declaration order and let the existing native autoindex adapter allocate normally. Reentry must validate contents and reuse already verified indices without repeating prefab registration. Fixtures cover initial parse, repeated patch merge, reload, all saved IDs, and conflicting definitions.
