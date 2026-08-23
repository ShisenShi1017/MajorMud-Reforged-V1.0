# MajorMUD Plus Runtime Projection - CodeUpdate0139

## Evidence

The projection is derived from the local native `WCCMMPLS.DOC`, `WCCMMPLS.msg`,
`WCCMMPLS.REL`, `WCCMMPLS.mcv`, and `WCCMMPLS.MDF` files. The native DLL remains
reference evidence and is not executed.

## Generated contract and consumers

`Librarium/Objects/majormud_plus/majormud_plus_records_0139.json` generates
`Data/Classic/majormud_plus.json`. The runtime catalog consumes `product_name`,
`persistence_authority`, `settings`, `player_features`, `sysop_features`,
`action_editor_fields`, `action_variables`, `contest_modes`, and the native
`contest_contract`.

The settings consumed are `description_credit_cost`, `description_copper_cost`,
`description_membership_day_cost`, `life_credit_cost`, `life_copper_cost`,
`life_membership_day_cost`, `life_purchase_enabled`, `maximum_lives`,
`minutes_per_age_year`, `starting_age`, `maximum_description_lines`,
`description_approval_required`, `approval_notification_user`,
`registry_value_ansi`, and `global_registry_command`.

## Executable coverage

- Player custom descriptions with configured charging, line limits, approval state,
  persistence, and approved-description display during `EXAMINE`.
- Registry create/edit, search, lookup, ANSI value presentation, and decoded
  playtime-derived default age.
- Additional-life purchase with configured credit, membership-day, and Classic
  carried-currency settlement plus the native nine-life cap.
- Authenticated Sysop/Administrator pending-list, approve, view, description delete,
  and registry delete operations.
- Persistent Plus action editor with create/change/view/list/delete behavior,
  board-specific/WCC restoration metadata, native substitution variables, and live
  standalone/player/monster/carried-item/floor-item target execution. The 64 native
  WCC actions and all 500 nonempty messages are decoded from `wccacts2.dat`; all
  eleven documented presentation slots are represented, and native printf operands
  are normalized to `%i/%p/%P/%t` with no unresolved placeholders.
- Explicitly confirmed Plus-data reset, limited exactly to descriptions and
  registries; actions and contest state are not incorrectly erased.
- Native visible/hidden contest placement and start/status/finish controls. The
  utility's exact 17 range rows place 40 `item.965` runes with at most 50 attempts
  each, exclude room attribute `0x10`, and preserve the native 17-visible / 15-hidden
  per-room capacities. Status and finish enumerate living-world and saved-character
  ownership locations; finish then removes the enumerated rune item from world and
  character state like the native destructive result scan. Hidden runes use the
  normal SEARCH discovery authority.

## Native evidence boundary

`WCCMMUTL.EXE` routines `0x00404D80-0x00404FA1`,
`0x00404CC4-0x00404D7F`, and `0x00404FA2-0x0040521C` provide the exact placement,
room admission, and result-scan contracts. Native behavior tabulates locations; it
does not contain an automatic prize or ranking mutation, so Reforged does not invent
one.

## Verification

- Classic generation: 34 families, 103,135 Librarium objects, 39 files.
- Server Release warnings-as-errors build: PASS.
- `--smoke-majormud-plus`: PASS, 22/22 checks.
- Compiler regression: PASS, 107,182 records across 43 families.
- Decode-consumption audit: MajorMUD Plus PASS, 34/34 scalar paths connected,
  zero pending markers, and zero unresolved markers.
