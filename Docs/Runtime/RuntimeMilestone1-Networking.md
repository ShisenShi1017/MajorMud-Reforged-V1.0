# Runtime Milestone 1 — Networking and Multi-Session Foundation

## Scope

Runtime Milestone 1 extends the existing `Reforged.Server` host. It does not create a parallel server and does not modify canonical Librarium data.

Implemented foundation:

- Opt-in telnet listener using `--run-network` / `--run-telnet`.
- Multiple host-owned sessions through `RuntimeSessionManager`.
- Login name prompt.
- World selection foundation.
- Classic-first default world loading.
- Host-authoritative command dispatch.
- Local console preserved through `--run-local`.
- Scheduler remains host-owned and runs alongside network mode.
- `global.json`, Codespaces devcontainer, and GitHub Actions build workflow.

## Classic-first boundary

Classic remains the only enabled world in default configuration. Classic protections are enforced at bootstrap:

- `transfersAllowed` must be false.
- `classicProtected` must be true.
- `aiMode` must remain `Off`.

Custom and AI worlds are intentionally not implemented yet. The host is structured so later world definitions can be added through configuration and runtime services instead of hardcoding new server paths.

## Running locally

Bootstrap diagnostics only:

```bash
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj -- .
```

Local console:

```bash
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj -- . --run-local
```

Telnet host:

```bash
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj -- . --run-network
```

Then connect from another terminal:

```bash
telnet 127.0.0.1 4000
```

## Evidence rule

Runtime Milestone 1 only exposes already loaded world rooms and exits. It does not invent combat, magic, NPC, item, economy, quest, dialogue, or script behavior.
