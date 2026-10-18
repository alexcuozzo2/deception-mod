# Official public PolyMod startup observations — 2026-10-18

Candidate: Deception-0.3.102-public-review.polymod, SHA256 fe3d7fd08894255b152853672756b62e9bbbe808fc98ed8e6368c4e2cba5f527. Source commit 436b46b.

Tested in an isolated APFS game copy with unmodified official PolyMod 1.2.18 (SHA256 db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d), retaining this Mac's existing Intel/Rosetta compatibility runtime. The everyday game and launcher were not replaced.

- Initial attempt: native CLR JIT crash before Deception initialization; cause unresolved.
- Loader-only baseline: main menu visibly reached, PolyMod 1.2.18 label visible; normal quit exit 0.
- Deception attempt: initialization completed but remained on black intro screen for about two minutes. Read-only process sample saved. Normal quit exit 0.
- Identical retry: main menu visibly reached with PolyMod 1.2.18 label. Logs confirm Deception Load, registry-ready, verified IDs and all mods loaded (5483ms). Normal quit exit 0.
- No existing match was opened, and no new match was created. An attempted Hub inspection was not verified because computer control reported a window change/no window; no Hub acceptance claimed.

The successful retry proves startup is possible; it does not establish a fix for intermittent startup failures. Gameplay, save/load, Windows, Linux and multiplayer acceptance are not established. This is a developer review candidate, not a stable community release. Public-loader custom online integration is disabled by its existing exact-loader admission guard.

Build evidence: isolated m0-4okaxqfm passed 339 model / 168 UI checks plus retained metadata/compiler gates. shared-vs_30i4p passed native-source/external-import candidate gates. Review archive contains only manifest, localization, Deception DLL, development status and review-loader metadata; no PolyMod or runtime distribution.

Local evidence directories: run-output/public-polymod-1.2.18/{with-mod-crash-logs,without-mod-success-logs,with-mod-black-screen-logs,with-mod-menu-success-logs}. Stack sample: with-mod-startup.sample.txt.
