# Runtime Milestone 1 Validation Plan

Run after applying the update package:

```bash
dotnet --info
dotnet restore Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj -- .
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj -- . --run-local
```

For network validation:

```bash
dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj -- . --run-network
```

Connect separately:

```bash
telnet 127.0.0.1 4000
```

Expected network behavior:

1. Host prints telnet listener startup.
2. Client receives host banner.
3. Client enters a name.
4. Client is placed into Classic world.
5. `look`, `where`, `world`, `worlds`, `sessions`, and movement commands respond.
6. `quit` closes the client session without stopping the host.
