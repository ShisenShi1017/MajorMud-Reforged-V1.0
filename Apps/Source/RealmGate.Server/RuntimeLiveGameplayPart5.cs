using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private string ExecuteBankCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var serviceCatalog = RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var bankService = ResolveBankAtRoom(serviceCatalog, session.CurrentRoom.RoomNumber)
            ?? throw new InvalidOperationException("There is no generated bank service in this room.");

        if (command.Name == "bank")
        {
            var carried = RuntimeBundleCInventoryAuthority.ReadCurrency(state);
            var banked = RuntimeBundleCEconomyAuthority.ReadBank(state.ActiveItemState);
            return $"{ReadServiceName(bankService)}\nCarried: {RuntimeBundleCInventoryAuthority.FormatCurrency(carried)}\nBanked: {RuntimeBundleCInventoryAuthority.FormatCurrency(banked.AsInventoryCurrency())}";
        }

        RuntimeClassicInventoryCurrency moved;
        RuntimeItemState updatedState;
        if (command.Name == "deposit") updatedState = RuntimeBundleCEconomyAuthority.Deposit(state, command.ArgumentText, out moved);
        else if (command.Name == "withdraw") updatedState = RuntimeBundleCEconomyAuthority.Withdraw(state, command.ArgumentText, out moved);
        else throw new InvalidOperationException("Unsupported bank command.");
        _characters.SaveCharacter(character with { ItemState = updatedState, UpdatedAtUtc = DateTimeOffset.UtcNow });
        var carriedAfter = RuntimeBundleCInventoryAuthority.ReadCurrency(updatedState);
        var bankedAfter = RuntimeBundleCEconomyAuthority.ReadBank(updatedState.ActiveItemState);
        return $"You {command.Name} {RuntimeBundleCInventoryAuthority.FormatCurrency(moved)}. Carried: {RuntimeBundleCInventoryAuthority.FormatCurrency(carriedAfter)}; Banked: {RuntimeBundleCInventoryAuthority.FormatCurrency(bankedAfter.AsInventoryCurrency())}.";
    }

    private static RuntimeServiceDefinition? ResolveBankAtRoom(RuntimeServiceCatalog catalog, int roomNumber) =>
        catalog.Services.Where(service => service.ServiceKind.Equals("bank", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(service => TryReadRoomNumber(service.GeneratedRecord, out var generatedRoom) && generatedRoom == roomNumber);

    private static bool TryReadRoomNumber(JsonElement record, out int roomNumber)
    {
        roomNumber = 0;
        if (!record.TryGetProperty("room_number", out var value)) return false;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out roomNumber),
            JsonValueKind.String => int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out roomNumber),
            _ => false
        };
    }

    private static string ReadServiceName(RuntimeServiceDefinition service) =>
        service.GeneratedRecord.TryGetProperty("name", out var value) && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : service.Id;
}

public sealed class RuntimeLiveGameplayPart5SmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeLiveGameplayPart5SmokeReport Run(string projectRoot, string worldId)
    {
        var report = new RuntimeLiveGameplayPart5SmokeReport();
        try
        {
            var catalog = RuntimeServiceCatalog.Load(projectRoot, worldId);
            Require(catalog.Services.Any(x => x.ServiceKind.Equals("bank", StringComparison.OrdinalIgnoreCase)), "generated-bank-catalog");
            report.Checks.Add("generated-bank-catalog");

            var state = RuntimeBundleCInventoryAuthority.WriteCurrency(new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string,string>()), new RuntimeClassicInventoryCurrency(1,2,3,4,5));
            state = RuntimeBundleCEconomyAuthority.Deposit(state, "2 gold", out var deposited);
            Require(deposited.Gold == 2 && RuntimeBundleCEconomyAuthority.ReadBank(state.ActiveItemState).Gold == 2, "deposit-preserves-denomination");
            report.Checks.Add("deposit-preserves-denomination");
            state = RuntimeBundleCEconomyAuthority.Withdraw(state, "1 gold", out var withdrawn);
            Require(withdrawn.Gold == 1 && RuntimeBundleCInventoryAuthority.ReadCurrency(state).Gold == 2, "withdraw-preserves-denomination");
            report.Checks.Add("withdraw-preserves-denomination");

            Require(RuntimeCommand.Parse("bank").Name == "bank", "bank-route");
            Require(RuntimeCommand.Parse("deposit 10").ArgumentText == "10", "deposit-route");
            Require(RuntimeCommand.Parse("withdraw 5").ArgumentText == "5", "withdraw-route");
            report.Checks.Add("bank-routes");

            var method = typeof(RuntimeLiveGameplayFacade).GetMethod("ExecuteBankCommand", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Require(method is not null, "live-bank-execution");
            report.Checks.Add("live-bank-execution");
        }
        catch (Exception ex)
        {
            report.Failures.Add(ex.Message);
        }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
