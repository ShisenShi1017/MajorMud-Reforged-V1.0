# Code Update 0304 — Classic native-file, configuration, and failure boundary

Pass 33 corrects three broad parity assumptions using direct operator evidence plus the retained native `WCCMMUD.mcv` setup catalog. It does **not** add a `WCCMMUD.INI` file to Reforged.

## Classic native-file workflow — PASS

Classic MajorMUD does not expose a native in-game world-data import/export/migration subsystem. The parity workflow is:

1. edit the native MajorMUD data file externally (for example with Nightmare Redux);
2. preserve the exact expected native file identity and exact version for the installed MajorMUD build;
3. copy the edited file into the MajorMUD installation;
4. start/restart the server.

Wrong/corrupt native files or version mismatches are hard startup/load failures, commonly crashing Worldgroup and requiring a restart. Pass 33 therefore removes the prior speculative Classic migration/import path. `RuntimeImportExportCompatibilityService` now rejects Classic `Import`, `Export`, and `Migrate`; Classic validation requires exact source/target versions and, for WCC/WBB validation, exact file identity. Custom/AI migration support remains a Reforged extension.

Exact native crash text, log output, and binary version/header encoding are not invented.

## Reforged configuration boundary — PASS-narrow

Native MajorMUD configuration is sourced from `WCCMMUD.INI`; retained `WCCMMUD.mcv` evidence contains **44 exact setup prompts/defaults**. Reforged itself has no `WCCMMUD.INI` and must not create one merely for parity.

`RuntimeClassicConfigurationAuthority` now centralizes the retained native defaults as evidence-backed constants/catalog rows while Reforged continues to use its own internal configuration/runtime owners. `RuntimeWorldRulesConfigurationService` is explicitly documented as Reforged-owned typed storage, not an INI parser.

`SYSOP RELOAD` remains a no-literal-file boundary in Reforged rather than being wired to a nonexistent INI.

## PvP default correction — PASS

The native setup prompt is exact:

`Maximum level difference for PVP? 100`

Reforged previously created an unconfigured PvP state with `MaximumLevelDifference = 0` (unrestricted). Pass 33 corrects the default to **100**. Persisted Reforged SYSOP overrides continue to take precedence.

The broader PvP policy remains UNKNOWN.

## Gameplay failure boundary — PASS-narrow

Operator evidence establishes that an ordinary rejected gameplay action generally leaves game state unchanged instead of invoking a synthetic rollback/recovery subsystem. `RuntimeErrorRecoveryService` now exposes an explicit `NoOp` disposition for `GameplayActionRejected`.

Exact command-specific failure text versus silent no-op behavior, malformed/missing native reference behavior, and catastrophic save rollback ordering remain UNKNOWN.

## Native configuration defaults retained

The 44-prompt catalog includes the already-active 60-second player save, 300-second buffer-save prompt, `MAXEPDAY=18`, `EPCYCLE=60`, `EPAMOUNT=1`, plus exact defaults such as PvP level difference 100, inactivity 5000 seconds, TOPTEN list size 30, final-death experience retention YES, Lawful choice YES, gender-change cost 5000 gold, and the coin-sweep settings. Pass 33 catalogs these exactly but does not invent undocumented allowed ranges or live mutability.

## Score

Before Pass 33: **88 PASS / 76 STRONG / 28 UNKNOWN = 192**.

After Pass 33: **93 PASS / 76 STRONG / 27 UNKNOWN = 196**.

- strict parity: **47.4%**
- evidence-backed: **86.2%**

The broad import/export UNKNOWN is closed because its premise was wrong: Classic uses exact native file replacement, not migration/import/export semantics. The world-rules and error-handling UNKNOWNs remain, but are materially narrower.
