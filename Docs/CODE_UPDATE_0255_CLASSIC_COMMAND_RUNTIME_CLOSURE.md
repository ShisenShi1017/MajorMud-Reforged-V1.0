# Code Update 0255 — Classic Command Runtime Closure

## Scope

This pass audits the complete Classic command surface and makes the parser, runtime dispatcher, policy gateway, contextual command catalog, help output, and session lifecycle use one command authority.

## Runtime command inventory

- 95 built-in canonical commands.
- 44 exact aliases after adding `g` for `get`.
- 25 generated evidence-backed contextual action records.
- 18 contextual verbs are additional canonical commands beyond the built-in set.
- 113 unique active Classic commands in the generated world catalog.
- 17 explicit runtime route families.
- `Docs/CLASSIC_COMMAND_RUNTIME_MATRIX.md` records every built-in command, exact alias, lowest accepted input, route family, and live consumer.

## Corrections

- Added exact `g` → `get` routing.
- Preserved exact `go` as a separate contextual room action.
- Added a central runtime route catalog covering every built-in command.
- Replaced the stale 83-command sidecar expectation with runtime-derived authority for all 95 registrations.
- Removed the dependency on the missing `Data/Classic/command_runtime_authority.json` file.
- Made the policy gateway consume the central route family rather than a second hand-maintained command list.
- Connected `tat <stat>` to the generated service/TAT character-point runtime; it previously fell through to the blocked policy gateway.
- Made `quit`, `qui`, `logout`, and `exit` use parser-based session termination in both Telnet and local-console hosts.
- Added all active contextual verbs to `COMMANDS` and `HELP <verb>` output.
- Added explicit usage text for all 95 built-in commands.
- Made `SupportedCommands` derive directly from the canonical registration catalog so movement, character, wealth, hide, and sneak cannot drift out of the audit set.

## Fail-closed behavior

- A built-in command with a route but no executable dispatcher branch now throws an explicit command-route failure instead of silently entering contextual fallback.
- Only commands not present in the built-in route catalog enter decoded world-command routing.
- Unknown or unbound contextual commands remain non-mutating and report that nothing in the current room responds.

## Validation gates

The Bundle F Phase F11 audit now verifies:

- canonical command count and alias count;
- exact token conflicts;
- every canonical command and alias resolves to the intended command;
- route coverage equals registration coverage;
- no orphan runtime routes;
- all built-in commands have help usage;
- generated command evidence is loaded and activated;
- all additional contextual verbs resolve through the generated catalog;
- the decoded world-command consumer exists;
- `g` resolves to `get` while `go` remains contextual;
- all session-exit aliases resolve to `quit`;
- `eq`, `ba`, and `sys` compatibility aliases remain intact.
