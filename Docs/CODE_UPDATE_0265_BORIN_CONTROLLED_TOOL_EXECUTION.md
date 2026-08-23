# Code Update 0265 — Borin Controlled Tool Execution

## Outcome

Borin now has a non-shell controlled execution boundary connecting validated structured decisions to selected existing Player, Developer, and Test Sysop services.

`BorinControlledToolExecutor` always calls `IBorinActionValidator` before dispatch. Rejected, stale, unauthorized, unverified, or duplicate decisions never reach a gateway.

## Player bindings

`BorinPlayerToolGateway` delegates to the existing `IBorinPlayerResearch` and canonical client session:

- connect and login
- disconnect
- look
- verified directional movement
- inventory
- equipment
- attack with a typed target

Commands continue through `SendSupervisedCommandAsync`; the external BBS/Reforged Host remains authoritative. Cast, general interaction, shop, quest, and pathfinding intents remain unbound until richer typed arguments prevent ambiguous command construction.

## Developer bindings

`BorinDeveloperToolGateway` delegates to existing Forge services:

- code/reference search
- bounded file read
- bounded directory read
- latest log/error/result evidence
- isolated workspace build
- isolated workspace test
- workspace diff preview
- apply the latest fenced patch to the isolated workspace

It does not expose arbitrary process or shell execution. Main-repository apply, rollback, focused command construction, test-server lifecycle, and repair-loop orchestration remain separate adapters.

## Repository read boundary

`BorinRepositoryReadTools` enforces:

- repository-relative paths only
- canonical root containment
- no `..` escape
- no absolute paths
- no reparse-point/symbolic-link traversal
- approved text extensions only
- 64,000-character file cap
- 250-entry directory cap
- `.git` directory exclusion
- read-only file sharing
- cancellation

## Sysop and Test Realm bindings

`BorinWorldsmithSysopToolGateway` delegates to Guardian Hall's existing Worldsmith service for:

- isolated realm monitoring/inspection
- Test Realm proposal generation
- staging of an already human-approved proposal

It cannot generate the human approval itself. It requires approval mode to remain enabled and rejects targets that are not Test, Development, or Offline Projection realms. The current Worldsmith apply path stages files to its isolated workspace and does not mutate runtime, Librarium, or production data.

Room/NPC/item/script/trigger-specific Sysop adapters remain unbound until their existing runtime/editor authorities have typed gateway contracts.

## Structured observations

Every tool result records success, code, intent, target, local authority, bounded redacted output, whether state changed, and completion time. Provider reasoning is not recorded as hidden chain-of-thought.

## Files

- `Apps/Source/Reforged.App/Brains/BorinControlledTools.cs`
- `Apps/Source/Borin.AiProviderTests/Program.cs`

## Verification

- Provider/context/action/routing/capability/tool tests: 28/28 pass.
- Tests prove repository containment, validation-before-dispatch, deterministic player translation, developer dispatch without shell access, fail-closed missing Sysop bindings, isolated realm inspection, and live-realm rejection.
- Existing Borin regression and full Release solution build are the final gates for this bundle.

The controlled executor is not yet registered in the active Forge composition root. No model-generated action was executed during verification.
