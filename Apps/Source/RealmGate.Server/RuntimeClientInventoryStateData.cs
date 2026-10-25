using System.Text.Json;

namespace RealmGate.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private string RenderClientInventoryStateData(RuntimeSession session)
    {
        var state = LoadItemState(session)
                    ?? new RuntimeItemState([], 0, new Dictionary<string, string>(), []);
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var services = ResolveServicesAtRoom(
                RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id),
                session.CurrentRoom.RoomNumber)
            .ToArray();
        var rows = state.Items
            .Where(item => string.Equals(item.OwnerId, session.Actor.Id, StringComparison.OrdinalIgnoreCase) &&
                           (string.Equals(item.LocationKind, RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(item.LocationKind, RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)))
            .Select(item =>
            {
                var definition = catalog.Require(item.CanonicalItemId);
                return new
                {
                    item.InstanceId,
                    ItemId = item.CanonicalItemId,
                    definition.Name,
                    definition.ItemType,
                    Slot = item.EquippedSlot ?? catalog.Equipment(item.CanonicalItemId)?.Slot ?? string.Empty,
                    Quantity = Math.Max(1, item.StackQuantity),
                    Charges = Math.Max(0, item.Charges),
                    item.Durability,
                    Equipped = item.EquippedSlot is not null
                };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var payload = new
        {
            WorldId = session.World.Definition.Id,
            CharacterId = session.Actor.Id,
            CharacterName = session.Actor.DisplayName,
            RoomId = session.CurrentRoom.Id,
            CurrencyCopper = RuntimeBundleCInventoryAuthority.ReadCurrency(state).CopperValue,
            ShopAvailable = services.Any(service =>
                ReadServiceKind(service).Contains("shop", StringComparison.OrdinalIgnoreCase) ||
                ReadServiceKind(service).Contains("merchant", StringComparison.OrdinalIgnoreCase)),
            BankAvailable = services.Any(service =>
                service.ServiceKind.Equals("bank", StringComparison.OrdinalIgnoreCase)),
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Inventory = rows.Where(item => !item.Equipped).ToArray(),
            Equipment = rows.Where(item => item.Equipped).ToArray()
        };
        return ReforgedClientProtocol.Encode("INVENTORYSTATE", JsonSerializer.Serialize(payload));
    }
}
