# DataDecode0102 — String & Message Decision Pass

Status: Complete.

Scope: static-only string and message ownership decisions. `/Data` was not modified. Runtime validation was not performed.

## Results

- Parsed 625 message rows from `Data/messages.json`.
- Catalogued 191551 non-empty string values from repository `Data/*.json`.
- Catalogued 5916 printable ASCII runs length >= 4 from uploaded `wgserver.exe`.
- Resolved 114 duplicate or same-key message reference groups without merging canonical rows.

## Decisions

- `Data/messages.json` owns canonical runtime message resources for `system`, `combat`, `monsters`, and imported `messages[]` entries.
- Non-message strings inside other `Data/*.json` files remain owned by their enclosing canonical data resources.
- `wgserver.exe` printable strings are evidence candidates only unless corroborated by repository/resource evidence; no canonical ownership was assigned from binary strings alone.
- No separate localization resource file or locale directory was observed in the static repository archive.

## Guardrails

- No runtime execution was performed.
- No `/Data` files were changed.
- Confidence changes are based only on static evidence.
