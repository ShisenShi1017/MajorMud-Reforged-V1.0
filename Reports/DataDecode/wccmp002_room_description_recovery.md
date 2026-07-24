# WCCMP002 Room Description Recovery 0139

## Result

- Canonical room records checked: `26666`
- Existing descriptions before recovery: `3217`
- Source title occurrences matched to canonical names: `26883`
- Accepted title groups: `1551`
- Rejected title groups: `311`
- Known-description validation checks: `1647`
- Known-description validation passes: `1591`
- New room descriptions promoted: `23309`
- From unique-title groups: `555`
- From validated duplicate-title groups: `17417`
- From identical duplicate-title groups: `693`
- From user-rule duplicate-title groups: `4644`
- Total descriptions after recovery: `26526`
- Still unresolved after exhausting local WCCMP evidence: `140`

## Evidence Policy

`wccmp002.dat` is a WCC default-install binary source. Its room-text
layout contains interleaved string fragments, so this pass only promotes
records whose title identity is exact and whose duplicate-name groups are
validated against existing Librarium descriptions. A final fallback uses
the user-approved rule that duplicate rooms share one description and
selects the most common normalized WCCMP002 extraction for that title.
Existing native or reference descriptions are not overwritten.

## Remaining Gap

The remaining unresolved rooms are placeholder-named room records such as
`Room 15/850`. The local WCCMP001/WCCMP002/WBBMP002 binaries and local
OmniVerse room captures do not contain those placeholder names as
recoverable room-description entries. They are listed in the JSON report
as `placeholder_named_room_without_local_wcc_description_evidence`.

## Output

- `Librarium/Objects/room_descriptions/wccmp002_room_description_records_0139.json`
- `Reports/DataDecode/wccmp002_room_description_recovery_0139.json`
