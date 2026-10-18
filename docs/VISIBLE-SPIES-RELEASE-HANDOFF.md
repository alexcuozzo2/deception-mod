# Visible Spies / shared release continuation (0.3.102)

Source includes the Windows shared setup branch through e17b1f1 and UI-A65.
Spy/Spymaid no longer gain Hide from movement, nor do their copied appearances.
Ordinary Cloak disguises remain supported; historical saved Invisible effects
remain until native reveal or existing reveal-on-ability handling clears them.

The published v0.3.101-windows-mac.1 setup is unchanged and still contains old
Spy behavior. Do not describe it as 0.3.102. The ordinary Mac build cannot replace
the Windows-enabled shared candidate: candidate compilation uses a separate
symbol, native resources and online identity.

## Native Windows release steps

For managed-only changes, the new [Mac shared-build workflow](SHARED-BUILD-FROM-MAC.md) reuses the pinned Windows helpers with source and emitted-import checks. The original native rebuild path below remains available when those compatibility checks require a new baseline.

1. Fetch and merge origin/main; preserve local work. Build/test a fresh shared
   candidate with the existing Windows native pack/probe inputs. Record frozen
   source hashes and exact candidate hash. Run native playtests with Alex.
2. Run `python distribution/test_windows_installer.py` under installed PowerShell.
   The updater now stages and verifies the mod, then atomically replaces it with
   no old-mod backup. Mac performed only its static check (PowerShell unavailable).
3. Update the exact candidate hash, source commit, version, asset name and release
   metadata in the shared builder/manifest/launcher/docs. Preserve game, beta and
   Dobby compatibility pins; never substitute a normal Mac-only archive.
4. Supply a clean verified Mac donor kit. The existing builder verifies listed
   manifest inputs but copies the whole donor directory: inspect the complete
   input file set and exclude incidental files. It currently lacks an automated
   frozen-source receipt gate; compare the candidate receipt with the committed
   source before publishing. This remains a release-tooling limitation.
5. Build into a new output directory and verify nested Mac integrity checks,
   Windows installer checks, ZIP checksums and byte-identical mod on both platforms.
   Publish a new private prerelease, not a replacement for the historical ZIP.
6. Install the same archive on both peers using platform-specific updaters.
   Keep Mac libraries on Mac and Windows libraries on Windows. Cross-platform
   multiplayer remains unverified until a real match passes.

## Mac validation

Full guarded build `m0-vf9hsedb` passed 339 model and 168 UI checks plus retained native/compiler gates. Focused cloak readiness94/276, movement137/375, catalog21/188 and ordinary actions114/285 passed. Windows build-tool4 and upgrade-wrapper5 passed; installer static check passed, executable PowerShell test skipped because PowerShell is unavailable. No installation or gameplay acceptance claimed.
