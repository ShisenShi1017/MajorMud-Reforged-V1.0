using System.Text.Json;

namespace RealmGate.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private const int ClientItemSearchLimit = 75;

    private string RenderClientItemSearchData(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            return ReforgedClientProtocol.Encode("ITEMSEARCHERROR", "A persistent character is required.");

        var query = string.Join(' ', command.Arguments.Skip(1)).Trim();
        if (query.Length is < 2 or > 80)
            return ReforgedClientProtocol.Encode("ITEMSEARCHERROR", "Enter 2 to 80 characters to search items.");

        var character = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found.");
        if (!character.WorldId.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Character and selected world identities do not match.");

        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var matches = catalog.Items
            .Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           item.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var equipped = (LoadItemState(session)?.Items ?? [])
            .Where(item => item.OwnerId?.Equals(actor.Id, StringComparison.OrdinalIgnoreCase) == true &&
                           item.EquippedSlot is not null)
            .OrderBy(item => item.EquippedSlot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ClientWorkshopEquippedProjection(
                item.InstanceId, ProjectWorkshopItem(catalog, catalog.Require(item.CanonicalItemId))))
            .ToArray();

        return ReforgedClientProtocol.Encode("ITEMSEARCH", JsonSerializer.Serialize(new
        {
            WorldId = session.World.Definition.Id,
            CharacterId = character.CharacterId,
            RoomId = session.CurrentRoom.Id,
            Query = query,
            TotalMatches = matches.Length,
            Matches = matches.Take(ClientItemSearchLimit).Select(item => ProjectWorkshopItem(catalog, item)).ToArray(),
            Equipped = equipped,
            CapturedAtUtc = DateTimeOffset.UtcNow
        }));
    }

    private static ClientWorkshopItemProjection ProjectWorkshopItem(RuntimeItemCatalog catalog, RuntimeItemDefinition item)
    {
        var equipment = catalog.Equipment(item.Id);
        return new ClientWorkshopItemProjection(
            item.Id, item.Name, item.ItemType, equipment?.Slot ?? string.Empty,
            item.Weight, item.Cost, item.MinimumDamage, item.MaximumDamage,
            item.ArmorClassBonus, item.DamageResistanceBonus, item.AccuracyBonus, item.SpeedBonus,
            equipment?.MinimumLevel ?? 0, equipment?.MaximumLevel ?? 0,
            equipment?.MinimumStrength ?? 0, equipment?.TwoHanded ?? false,
            equipment?.ClassRestricted ?? false, equipment?.RaceRestricted ?? false,
            item.OrderedAbilities.Select(ability => new ClientWorkshopAbilityProjection(
                ability.Name, ability.Value)).ToArray());
    }

    private sealed record ClientWorkshopItemProjection(
        string Id, string Name, string ItemType, string Slot, int Weight, long Cost,
        int MinimumDamage, int MaximumDamage, int ArmorClassBonus, int DamageResistanceBonus,
        int AccuracyBonus, int SpeedBonus, int MinimumLevel, int MaximumLevel,
        int MinimumStrength, bool TwoHanded, bool ClassRestricted, bool RaceRestricted,
        IReadOnlyList<ClientWorkshopAbilityProjection> Abilities);

    private sealed record ClientWorkshopAbilityProjection(string Name, int Value);
    private sealed record ClientWorkshopEquippedProjection(string InstanceId, ClientWorkshopItemProjection Item);
}
