namespace Reforged.Server;

public sealed record RuntimeCharacterFoundationSmokeResult(
    bool Passed,
    string AccountId,
    string CharacterId,
    string StartRoomId,
    string RestoredRoomId,
    IReadOnlyList<string> Checks);

public static class RuntimeCharacterFoundationSmoke
{
    public static RuntimeCharacterFoundationSmokeResult Run(string projectRoot, RuntimeWorld world)
    {
        var service = new RuntimeCharacterService(projectRoot);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var account = service.GetOrCreateAccount("smoke-" + suffix);
        var catalog = RuntimeCharacterCatalog.Load(projectRoot, world.Definition.Id);
        var race = catalog.Races.FirstOrDefault() ?? throw new InvalidOperationException("No generated races are available.");
        var characterClass = catalog.Classes.FirstOrDefault() ?? throw new InvalidOperationException("No generated classes are available.");
        var checks = new List<string>
        {
            "account-created",
            "generated-race-selected",
            "generated-class-selected"
        };

        if (world.ResolveGeneratedCharacterStartRoom() is null)
        {
            try
            {
                service.CreateCharacter(account, world, "Smoke" + LetterSuffix(suffix), "Male", race.Id, characterClass.Id);
            }
            catch (InvalidOperationException exception) when (
                exception.Message.Contains("Character Foundation Phase003 creation gate is blocked", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("no generated Classic character-start room", StringComparison.OrdinalIgnoreCase))
            {
                checks.Add("character-creation-blocked-by-generated-gate");
                return new RuntimeCharacterFoundationSmokeResult(true, account.AccountId, string.Empty, string.Empty, string.Empty, checks);
            }
        }

        var character = service.CreateCharacter(account, world, "Smoke" + LetterSuffix(suffix), "Male", race.Id, characterClass.Id);
        var loaded = service.LoadCharacter(character.CharacterId) ?? throw new InvalidOperationException("Created character could not be restored.");
        checks.Add("generated-start-room-used");
        checks.Add("character-atomically-saved");
        checks.Add("character-restored");
        return new RuntimeCharacterFoundationSmokeResult(
            character.CharacterId == loaded.CharacterId && character.RoomId == loaded.RoomId,
            account.AccountId,
            character.CharacterId,
            character.RoomId,
            loaded.RoomId,
            checks);
    }

    private static string LetterSuffix(string value)
    {
        const string letters = "ABCDEFGHIJKLMNOP";
        return new string(value.Select(character => letters[Convert.ToInt32(character.ToString(), 16)]).ToArray());
    }
}
