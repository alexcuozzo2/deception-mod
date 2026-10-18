# UI-A43 — Private online synchronization

Alex requests online play with a friend using the same Apple Silicon/Rosetta
setup and supplied PolyMod 1.3 preview. Implement the integration; do not equate
a working lobby with synchronized Deception gameplay.

## Scope

- Private invited games, matching complete mod checksums, native state 123 and
  the audited preview loader. Preserve Local/PassAndPlay behavior and current
  5 HP/1 defense Spy balance.
- Carry the existing complete state ledger and custom command payloads through
  initial creation, live clients, preview shadow simulation, command relay and
  reconnect. Never reconstruct an empty ledger for a missing online frame.
- Use native ordered command indices, action validation and account/lobby
  authorization. A payload checksum proves integrity, not sender identity.
- Separate synchronous detached simulation authority from live client authority;
  no thread-local scope may remain open across an asynchronous await. A shadow
  state cannot become a live state by pointer or epoch coincidence.
- Exact matching mod files are required. Do not silently admit older saves,
  mismatched versions, unknown loaders or replay/spectator modes.
- Normal logs must not disclose opponents' actual types or private identity
  links. Existing observer, fog, calculator and command guards remain.

## Evidence and boundaries

The preview sends full state/command byte arrays and replays commands on a
detached shadow. Its initial generator bypasses native CreateSession. Both
paths need explicit integration. Server acceptance and two-machine behavior
require an actual private-lobby test; automated codec/lifecycle tests cannot
establish them. No existing backend protocol change is presumed.

Peers receive complete simulation state, as required for this cooperative
client simulation. UI secrecy is not protection against modified clients or
inspection of saved/network state. Do not advertise adversarial anti-cheat.

Alex continues to lead gameplay testing. No automatic game launch or save edits.
Provide a matching package and a concise two-Mac test sequence, with remaining
unobserved behavior clearly identified.
