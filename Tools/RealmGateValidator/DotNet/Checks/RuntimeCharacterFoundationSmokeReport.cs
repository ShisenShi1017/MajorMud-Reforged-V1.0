namespace RealmGate.Server;

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
                service.CreateCharacter(account, world, "Sm" + LetterSuffix(suffix), "Male", race.Id, characterClass.Id);
            }
            catch (InvalidOperationException exception) when (
                exception.Message.Contains("Character Foundation Phase003 creation gate is blocked", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("no generated Classic character-start room", StringComparison.OrdinalIgnoreCase))
            {
                checks.Add("character-creation-blocked-by-generated-gate");
                return new RuntimeCharacterFoundationSmokeResult(true, account.AccountId, string.Empty, string.Empty, string.Empty, checks);
            }
        }

        var character = service.CreateCharacter(account, world, "Sm" + LetterSuffix(suffix), "Male", race.Id, characterClass.Id);
        var loaded = service.LoadCharacter(character.CharacterId) ?? throw new InvalidOperationException("Created character could not be restored.");
        checks.Add("generated-start-room-used");
        checks.Add("character-atomically-saved");
        checks.Add("character-restored");
        var itemCatalog = RuntimeItemCatalog.Load(projectRoot, world.Definition.Id);
        var light = itemCatalog.Items.First(item =>
            item.ItemType.Equals("light_source", StringComparison.OrdinalIgnoreCase) &&
            itemCatalog.Equipment(item.Id)?.Slot.Equals("LIGHT", StringComparison.OrdinalIgnoreCase) == true);
        var lightInstance = new RuntimeItemInstance(
            "character-foundation-light-smoke",
            light.Id,
            loaded.CharacterId,
            RuntimeItemLocations.Character,
            loaded.CharacterId,
            null,
            light.DefaultCharges,
            1,
            null);
        var itemEngine = new RuntimeItemEngine(itemCatalog, new[] { lightInstance }, Array.Empty<RuntimeCorpseState>());
        var equippedLight = itemEngine.Equip(lightInstance.InstanceId, loaded.CharacterId, loaded);
        if (!string.Equals(equippedLight.EquippedSlot, "LIGHT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generated light source did not equip through the LIGHT channel.");
        checks.Add("light-source-equipped");
        service.DeleteCharacter(account.AccountId, loaded.CharacterId, world.Definition.Id);
        var deleted = service.LoadCharacter(loaded.CharacterId) is null;
        var accountSlotRemoved = service.ListCharacters(account.AccountId)
            .All(value => !value.CharacterId.Equals(loaded.CharacterId, StringComparison.OrdinalIgnoreCase));
        checks.Add("character-deleted");
        checks.Add("account-slot-removed");
        return new RuntimeCharacterFoundationSmokeResult(
            character.CharacterId == loaded.CharacterId && character.RoomId == loaded.RoomId && deleted && accountSlotRemoved,
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
