namespace Reforged.Server;

public sealed record RuntimeSessionSpawnResolution(
    RuntimeRoom Room,
    IRuntimeSessionActor Actor,
    string Source,
    string? Diagnostic = null);

public static class RuntimeSessionSpawnResolver
{
    private static readonly RuntimeRoom SyntheticDeveloperRoom = new()
    {
        Id = "developer.test-room",
        MapId = 0,
        RoomNumber = 0,
        Name = "Developer Test Room",
        Description = "An isolated runtime-only diagnostic room. It is not part of the Librarium or generated Classic world data.",
        Exits = Array.Empty<RuntimeExit>()
    };

    public static RuntimeSessionSpawnResolution Resolve(
        RuntimeWorld world,
        string requestedName,
        RuntimeCharacterState? savedCharacter,
        RuntimeDevelopmentOptions development)
    {
        if (savedCharacter is not null &&
            string.Equals(savedCharacter.WorldId, world.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            var savedRoom = world.FindRoomById(savedCharacter.RoomId)
                ?? (savedCharacter.RoomNumber > 0 ? world.FindRoomByNumber(savedCharacter.RoomNumber) : null);
            if (savedRoom is not null)
            {
                var character = new RuntimeCharacterActor(
                    savedCharacter.CharacterId,
                    savedCharacter.DisplayName,
                    savedCharacter.Race,
                    savedCharacter.CharacterClass,
                    savedCharacter.Level,
                    "Unspecified");
                return new RuntimeSessionSpawnResolution(savedRoom, character, "saved-character-room");
            }
        }

        if (!development.Enabled || !development.CreateRuntimeTestAvatar)
        {
            throw new InvalidOperationException(
                $"Session entry could not be resolved for world '{world.Definition.Id}'. " +
                "No valid saved character exists and the developer test avatar is disabled. " +
                "Normal new-character creation is not implemented in CodeUpdate0133.");
        }

        var displayName = string.Equals(requestedName, "LocalConsole", StringComparison.OrdinalIgnoreCase)
            ? development.TestAvatarName
            : requestedName;
        var avatar = new RuntimeDeveloperAvatar(
            $"developer-avatar:{Guid.NewGuid():N}",
            string.IsNullOrWhiteSpace(displayName) ? development.TestAvatarName : displayName.Trim());

        var configuredDiagnostic = world.ResolveDiagnosticRoom();
        if (configuredDiagnostic is not null)
        {
            return new RuntimeSessionSpawnResolution(
                configuredDiagnostic,
                avatar,
                "developer-diagnostic-room",
                "A runtime-only developer avatar was attached to the session. It is not a Classic character and is not persisted.");
        }

        return new RuntimeSessionSpawnResolution(
            SyntheticDeveloperRoom,
            avatar,
            "synthetic-developer-test-room",
            "No diagnostic room was configured. The developer avatar was placed in an isolated runtime-only room; configure diagnosticRoom to test generated-room movement.");
    }
}
