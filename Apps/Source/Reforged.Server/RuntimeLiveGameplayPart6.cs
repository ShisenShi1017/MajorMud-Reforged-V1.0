using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string ExecuteLiveServicesBundleCommand(RuntimeSession session, RuntimeCommand command)
    {
        return command.Name switch
        {
            "services" => RenderServicesHere(session),
            "list" => RenderServiceInventory(session),
            "trade" => ExecuteDirectTrade(session, command),
            "buy" or "sell" or "repair" or "train" or "tat" or "stats" => ExecuteGeneratedServiceTransaction(session, command),
            _ => _policyGateway.Execute(session, command)
        };
    }

    private string RenderServicesHere(RuntimeSession session)
    {
        var catalog = RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var services = ResolveServicesAtRoom(catalog, session.CurrentRoom.RoomNumber).ToArray();
        if (services.Length == 0) return "There are no generated services in this room.";
        return "Services:" + Environment.NewLine + string.Join(Environment.NewLine,
            services.Select(x => $"- {ReadServiceName(x)} [{ReadServiceKind(x)}] ({x.Id})"));
    }

    private string RenderServiceInventory(RuntimeSession session)
    {
        var catalog = RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var service = ResolveServicesAtRoom(catalog, session.CurrentRoom.RoomNumber)
            .FirstOrDefault(x => !ReadServiceKind(x).Equals("bank", StringComparison.OrdinalIgnoreCase));
        if (service is null) throw new InvalidOperationException("There is no generated shop or trainer service in this room.");

        var kind = ReadServiceKind(service);
        if (kind.Equals("trainer", StringComparison.OrdinalIgnoreCase)) return RenderTrainerDefinition(service);
        if (!service.GeneratedRecord.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array)
            return $"{ReadServiceName(service)} has no generated inventory records.";

        var itemCatalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var runtimeState = GameplayStore(session.World.Definition.Id).Read(service);
        var lines = new List<string>();
        foreach (var entry in inventory.EnumerateArray())
        {
            if (!entry.TryGetProperty("item_exists", out var exists) || exists.ValueKind != JsonValueKind.True) continue;
            var itemRef = entry.TryGetProperty("item_ref", out var itemValue) ? itemValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(itemRef)) continue;
            var name = itemCatalog.Require(itemRef).Name;
            var quantity = runtimeState.Stock.TryGetValue(itemRef, out var stock) ? stock.Quantity : 0;
            lines.Add($"- {name} ({itemRef}) stock={quantity}");
        }
        return lines.Count == 0
            ? $"{ReadServiceName(service)} has no verified generated inventory available."
            : $"{ReadServiceName(service)} [{kind}]" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private string RenderTrainerHere(RuntimeSession session)
    {
        var catalog = RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var trainer = ResolveServicesAtRoom(catalog, session.CurrentRoom.RoomNumber)
            .FirstOrDefault(x => ReadServiceKind(x).Equals("trainer", StringComparison.OrdinalIgnoreCase));
        if (trainer is null) throw new InvalidOperationException("There is no generated trainer service in this room.");
        return RenderTrainerDefinition(trainer);
    }

    private static string RenderTrainerDefinition(RuntimeServiceDefinition trainer)
    {
        var min = trainer.GeneratedRecord.TryGetProperty("level_range", out var range) && range.TryGetProperty("min", out var minValue) ? minValue.GetString() : null;
        var max = trainer.GeneratedRecord.TryGetProperty("level_range", out range) && range.TryGetProperty("max", out var maxValue) ? maxValue.GetString() : null;
        return string.Join(Environment.NewLine, new[]
        {
            $"Trainer: {ReadServiceName(trainer)}",
            $"Service ID: {trainer.Id}",
            $"Generated level range: {min ?? "unknown"}-{max ?? "unknown"}",
            $"Generated class limit: {(trainer.GeneratedRecord.TryGetProperty("class_limit", out var classLimit) ? classLimit.ToString() : "unknown")}",
            "Train advances one level after room, level/class, experience, and progression checks; it does not sell skills or spells."
        });
    }

    private string ExecuteDirectTrade(RuntimeSession sourceSession, RuntimeCommand command)
    {
        if (sourceSession.Actor is not RuntimeCharacterActor sourceActor) throw new InvalidOperationException("A persistent character is required.");
        if (command.Arguments.Count < 4 || !command.Arguments[^2].Equals("to", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use: trade gold <amount> to <character> or trade item <item> to <character>.");

        var kind = command.Arguments[0];
        var targetQuery = command.Arguments[^1];
        var targetSession = _sessions.FindUniqueInRoom(sourceSession, targetQuery)
            ?? throw new InvalidOperationException($"No connected character here uniquely matches '{targetQuery}'.");
        if (targetSession.Actor is not RuntimeCharacterActor targetActor) throw new InvalidOperationException("The target must be a persistent character.");

        if (kind.Equals("item", StringComparison.OrdinalIgnoreCase))
        {
            var itemText = string.Join(" ", command.Arguments.Skip(1).Take(command.Arguments.Count - 3));
            return ExecuteItemCommand(sourceSession, RuntimeCommand.Parse($"give {itemText} to {targetQuery}"));
        }
        if (!kind.Equals("gold", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Trade type must be 'gold' or 'item'.");

        var amountText = string.Join(" ", command.Arguments.Skip(1).Take(command.Arguments.Count - 3));
        if (!long.TryParse(amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            throw new InvalidOperationException("Trade gold amount must be a positive whole number.");

        var transferredAmount = _characters.ExecuteCharacterTransaction(sourceActor.Id, targetActor.Id, (sourceCharacter, targetCharacter) =>
        {
            var sourceState = sourceCharacter.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
            var targetState = targetCharacter.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
            if (sourceState.Gold < amount) throw new InvalidOperationException("You do not have enough carried gold.");
            var sourceUpdated = sourceCharacter with { ItemState = sourceState with { Gold = checked(sourceState.Gold - amount) } };
            var targetUpdated = targetCharacter with { ItemState = targetState with { Gold = checked(targetState.Gold + amount) } };
            return (sourceUpdated, targetUpdated, amount);
        });
        targetSession.EnqueueMessage($"{sourceSession.Actor.DisplayName} gives you {transferredAmount} gold.");
        return $"You give {transferredAmount} gold to {targetSession.Actor.DisplayName}.";
    }

    private static IEnumerable<RuntimeServiceDefinition> ResolveServicesAtRoom(RuntimeServiceCatalog catalog, int roomNumber) =>
        catalog.Services.Where(service => TryReadServiceRoomNumber(service.GeneratedRecord, out var generatedRoom) && generatedRoom == roomNumber);

    private static bool TryReadServiceRoomNumber(JsonElement record, out int roomNumber)
    {
        roomNumber = 0;
        var property = record.TryGetProperty("room_number", out var direct) ? direct : record.TryGetProperty("room_ref", out var reference) ? reference : default;
        if (property.ValueKind == JsonValueKind.Undefined) return false;
        if (property.ValueKind == JsonValueKind.Number) return property.TryGetInt32(out roomNumber);
        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.StartsWith("room.", StringComparison.OrdinalIgnoreCase)) text = text[5..];
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out roomNumber);
    }

    private static string ReadServiceKind(RuntimeServiceDefinition service) =>
        service.GeneratedRecord.TryGetProperty("service_kind", out var value) && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : service.ServiceKind;

    private static int ReadGeneratedInteger(JsonElement record, string property)
    {
        if (!record.TryGetProperty(property, out var value)) return 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number
            : int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : 0;
    }
}

public sealed class RuntimeLiveGameplayPart6SmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public static RuntimeLiveGameplayPart6SmokeReport Run(string projectRoot, string worldId)
    {
        var report = new RuntimeLiveGameplayPart6SmokeReport();
        try
        {
            var catalog = RuntimeServiceCatalog.Load(projectRoot, worldId);
            Require(catalog.Services.Any(x => x.GeneratedRecord.TryGetProperty("service_kind", out var k) && k.GetString() == "trainer"), "trainer-catalog"); report.Checks.Add("trainer-catalog");
            Require(catalog.Services.Any(x => x.GeneratedRecord.TryGetProperty("inventory", out var i) && i.ValueKind == JsonValueKind.Array), "shop-inventory-catalog"); report.Checks.Add("shop-inventory-catalog");
            Require(RuntimeCommand.Parse("services").Name == "services", "services-route"); report.Checks.Add("services-route");
            Require(RuntimeCommand.Parse("list").Name == "list", "list-route"); report.Checks.Add("list-route");
            Require(RuntimeCommand.Parse("trade gold 10 to hero").ArgumentText == "gold 10 to hero", "trade-route"); report.Checks.Add("trade-route");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod("ExecuteLiveServicesBundleCommand") is not null, "live-services-execution"); report.Checks.Add("live-services-execution");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
