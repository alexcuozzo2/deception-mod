# Offline online-session lifecycle fixture

This executable suite compiles the production `NativeOnlineSession` and integrated `NativeRegroupSession`, including real `NativeGameStateSnapshot`, all Core codecs, real reflection/ABI hook lookup, saved-summary and menu-preview guards, and paid-birth partial declarations.

The copied, disjoint native ports model GameState/Map/Client/ActionManager identity and loader callback signatures. `NativeLedgerState` is a graph-boundary port: it checks real ledger structure and records bootstrap/reconcile calls, but does not simulate the complete native unit graph. `NativeOnlinePreview` is an explicitly offline stub returning one fixed compatibility token. Harmony captures target methods and does not detour them. Tests invoke the actual callback methods in the audited native order.

## Run

From the workspace root with the existing configured SDK and test host:

```
python3 project/tests/online-session-0383/run.py
python3 project/tests/online-session-0383/run.py --local
```

`--local` runs the unchanged `tests/session-0363/SessionTests.cs` cases separately against the same compiled production sources and these ports. It does not change that retained fixture or its expected manifest.

The runner snapshots every input into `run-output/online-sync-0383/lifecycle-fixture/fixture-*`, saves compiler/run logs and JSON results, and records hashes plus whether inputs changed during execution. It neither launches the game nor reads saves, connects to a server, installs a mod or downloads dependencies.

Three optional negative controls change only the isolated source copy and are expected to exit with a test failure:

```
DCP_ONLINE_MUTANT=pending-read python3 project/tests/online-session-0383/run.py
DCP_ONLINE_MUTANT=pending-fault python3 project/tests/online-session-0383/run.py
DCP_ONLINE_MUTANT=local-writer python3 project/tests/online-session-0383/run.py
```

## Build integration

- Key: `online-session`
- Assembly: `Deception.OnlineSessionTests`
- Entry: `Deception.Tests.OnlineSessionTests.Run`
- Source: all `src/Core/*.cs`; `src/PolyMod/{NativeApi,NativeSavedMatchSummary,NativeMenuPreviewScope,NativeRegroupSession,NativeRegroupPaidBirth,NativeAquarionUnits,NativeGameStateSnapshot,NativeOnlineSession}.cs`; `tests/online-session-0383/*.cs`.
- Do not add production `NativeOnlinePreview.cs`: this gate intentionally uses `PreviewPort.cs`; the real hash-pinned preview adapter needs its own binding tests.
- Validate case identities and scalar counts against `expected.json`.

## Evidence boundary

Passing establishes managed lifecycle/frame ownership and rejection behavior for the exercised callback order. It does not establish actual Unity/IL2CPP hook execution, native object lifetime, native graph serialization, multiplayer-service behavior, peer authentication, reconnect timing across awaits, or in-game observer secrecy. Those remain separate native/online acceptance work.
