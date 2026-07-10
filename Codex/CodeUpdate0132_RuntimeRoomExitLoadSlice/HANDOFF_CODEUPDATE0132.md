# Codex Handoff — CodeUpdate 0132 Runtime Room/Exit Load Slice

## Goal
Verify the server can enter a real generated Classic room and move through at least one generated exit.

## Commands

```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --smoke-room-exits
python Tools/Validation/validate_codeupdate0132_runtime_room_exit_slice.py
printf "look\nnorth\nlook\nquit\n" | dotnet run --project Apps/Source/Reforged.Server -- --run-local
```

## Expected
- Build passes.
- `--smoke-generated-data` passes.
- `--smoke-room-exits` passes.
- Local console starts in `Town Gates` with exits.
- `north` moves to `Estwall Street`.

## Known Boundary
This is a room/movement slice only. Do not treat it as combat-ready.
