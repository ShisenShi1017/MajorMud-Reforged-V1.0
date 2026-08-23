# WCCMMUD SYSOP Command Closure — CodeUpdate0139

## Outcome

The decoded WCCMMUD online SYSOP/GAMEOP command surface is now a Librarium family, generated into `Data/Classic/sysop_commands.json`, and consumed through one authenticated `SYSOP` runtime entry point.

- 20 native command roots retained.
- 10 command roots retain decoded GAMEOP permission.
- The user-confirmed `code_of_twinks_78` value is recorded as the legacy `SYSOPKEY` configuration value, not exposed as a player command.
- Runtime authority is the authenticated account role: Moderator for GAMEOP-allowed commands; Sysop or Administrator for the complete catalog.
- Six safe roots are executable: `BUFFERS`, `STATUS`, `MAP`, room `RESET`, realm `SPEAK`, and `ARENA`.
- Unsupported roots return their exact catalog disposition and make no mutation.

## Fail-closed boundaries

- `DISBAND`, `DISABLE`, `ENABLE`, `GANGSIZE`, and `UNWRAP` require the absent native gang runtime. No guild subsystem was invented.
- All named GOTO destinations are live and persistent: NewHaven resolves to `room.map1.2150`, SilverMere to `room.map1.2189`, Rhudaur to `room.map2.2519`, Khazarad to `room.map6.1249`, Lost City to `room.map16.426`, and Support to `room.map1.164`. The final four mappings are user-confirmed exact Classic destinations and all six resolve in generated runtime data.
- All six invasion phrases and the complete available textblock 2454 parent payload were recovered: `orc invasion`, `cult invasion`, `red invasion`, `green invasion`, `duergar invasion`, and `saracen invasion`. Their item gates, random-table references, messages, support-room return, once-per-cleanup behavior, and recovery cleanup semantics are retained in generated data. The referenced random child tables themselves are absent.

## Support-room projection follow-up

All six phrases are generated as room-scoped interactions at `room.map1.164` and recognized by the live decoded-command dispatcher. The final installed MDB and WCCTEXT2 catalogs omit their children, but the exact source records remain in `ImportedDatabases/WCCDefaultInstall/wcc default/wccupda2.dat`. `Tools/Promote-WccInvasionScripts.ps1` reproducibly recovers the 25 placement children (`4369-4372`, `4647-4655`, `4659-4666`, and `4669-4672`) plus six referenced random NPC-mixture tables and promotes them to `Librarium/Objects/invasion_scripts/wcc_update_invasion_script_records_0139.json`; compilation emits one `invasion_scripts.json` runtime file.

Runtime execution requires an enabled SYSOP or Administrator account in the Support Chamber, validates every generated room and NPC before mutation, consumes the decoded one-use gate item, and summons through the authoritative living-world service. Board cleanup removes surviving `wcc-invasion:` NPC instances and restores the generated gate items. The focused SYSOP/invasion smoke passes 15/15.
- `RESET AREA` remains blocked until authoritative generated area boundaries exist.
- Other capability-mapped commands remain visible but non-mutating until their owning runtime services exist.

## Verification

- Classic generated-data regeneration: PASS, 33 runtime families, 105,621 objects, 38 files.
- Compiler tests: PASS, 107,202 records across 44 families.
- Server Release build with warnings as errors: PASS, 0 warnings and 0 errors.
- `--smoke-wcc-sysop`: PASS, 15/15, including six generated support-room commands and proof that all 25 required random child records are absent.
- `--smoke-majormud-plus`: PASS, 15/15.
- Classic retirement ledger regenerated: 3,650 decoded/imported source files, 38 runtime files, retirement ready `NO`.

This closes catalog generation, authentication, safe dispatch, and explicit blocker reporting. It does not claim complete behavioral parity for commands whose native evidence or owning subsystem is still missing.
