# M0.1 report review and M0.2 correction

## Source report

`deception-m0-report.z8mwwxw0.zip`, supplied by Alex after the first research test.
The user reported blocked purchases behind `dcp_root_imperius` and no crashes.
The recorded game process exited normally (status 0); config hashes before/after
were identical. This log does not record each research-button click.

`deception/native-registry-before.json` records:

```json
"basic": { "idx": 0, "cost": 0,
  "techUnlocks": ["organization", "climbing", "fishing", "hunting", "riding"],
  "other_fields": "present in source; omitted from this excerpt, not changed by fix"
}
```

Imperius starts with `["basic", "organization"]`. Every one of the 16 playable
Human/Special tribes in the capture starts with `basic`, and none overrides that
root. Their child-technology overrides differ and must be preserved.

The old mod's own tree audit lists 12 newly added `dcp_root_*` IDs. The game log
specifically says:

```text
Created mapping for Polytopia.Data.TechData+Type with id dcp_root_imperius and index 1004
```

`src/Core/TechTree.cs` in M0.1 cloned Basic, changed its index to -1 for custom ID
allocation, and redirected each ordinary tribe's Basic root to that clone. It did
not add that distinct root to the player's owned research. The synthetic tests
checked six children but did not check whether the root prerequisite was already
owned. This is an implementation error, not a problem with Rosetta or the loader.

## M0.2 source change

- No new `dcp_root_*` IDs or root overrides.
- One added research node, `dcp_deception`; one appended edge on original Basic.
- Original Basic ID/index/cost and all non-edge fields preserved.
- Original entire tribeData, all original technology entries other than Basic's
  child list, unit definitions, resource data, modifiers and starting conditions
  preserved.
- All 16 playable tribes covered through the native shared root; original special
  child overrides remain effective.
- No free starting research; an unexpected missing native root is rejected rather
  than patched over by gifting research.
- M0.1 render-helper prefix removed. Only the existing managed GLD prefix remains.
- Reentry validated to avoid a duplicate edge; fresh base data can be processed again.

The updated managed build suite includes the captured registry, a reproduction of
the old unowned-root invariant, all-tribe starting-root checks and unchanged native
fields/modifiers. These are model/data checks, NOT execution of the game's native
purchase command. The corrected research behavior still needs Alex's next game test.

See `evidence/root-diagnosis.json` for extracted registry and log values, and
`evidence/VALIDATION.md` for exactly which tests ran in which environment.
