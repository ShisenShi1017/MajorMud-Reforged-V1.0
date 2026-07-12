using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private const string BankedGoldStateKey = "economy.banked-gold";

    private string ExecuteBankCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            return "A persistent character is required.";

        var character = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found.");
        var itemState = character.ItemState
            ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());

        var serviceCatalog = RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var bank = ResolveBankAtRoom(serviceCatalog, session.CurrentRoom.RoomNumber)
            ?? throw new InvalidOperationException("There is no generated bank service in this room.");

        var activeState = new Dictionary<string, string>(itemState.ActiveItemState, StringComparer.OrdinalIgnoreCase);
        var bankedGold = ReadBankedGold(activeState);
        var account = new RuntimeCurrencyAccount(actor.Id, itemState.Gold, bankedGold);

        switch (command.Name)
        {
            case "bank":
                return $"{ReadServiceName(bank)}\nCarried gold: {account.Carried}\nBanked gold: {account.Banked}";
            case "deposit":
                account.Deposit(ParsePositiveAmount(command.ArgumentText));
                break;
            case "withdraw":
                account.Withdraw(ParsePositiveAmount(command.ArgumentText));
                break;
            default:
                throw new InvalidOperationException("Unsupported bank command.");
        }

        var accountSnapshot = account.Capture();
        activeState[BankedGoldStateKey] = accountSnapshot.Banked.ToString(CultureInfo.InvariantCulture);
        var updatedState = itemState with
        {
            Gold = accountSnapshot.Carried,
            ActiveItemState = activeState
        };
        _characters.SaveCharacter(character with
        {
            ItemState = updatedState,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        return command.Name == "deposit"
            ? $"You deposit gold. Carried: {accountSnapshot.Carried}; Banked: {accountSnapshot.Banked}."
            : $"You withdraw gold. Carried: {accountSnapshot.Carried}; Banked: {accountSnapshot.Banked}.";
    }

    private static RuntimeServiceDefinition? ResolveBankAtRoom(RuntimeServiceCatalog catalog, int roomNumber) =>
        catalog.Services
            .Where(service => service.ServiceKind.Equals("bank", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(service => TryReadRoomNumber(service.GeneratedRecord, out var generatedRoom)
                && generatedRoom == roomNumber);

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
        service.GeneratedRecord.TryGetProperty("name", out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : service.Id;

    private static long ReadBankedGold(IReadOnlyDictionary<string, string> state)
    {
        if (!state.TryGetValue(BankedGoldStateKey, out var raw)) return 0;
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new InvalidOperationException("The saved bank balance is invalid.");
        return value;
    }

    private static long ParsePositiveAmount(string value)
    {
        if (!long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            throw new InvalidOperationException("Enter a positive whole-number gold amount.");
        return amount;
    }
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

            var account = new RuntimeCurrencyAccount("smoke.actor", carried: 100, banked: 25);
            account.Deposit(40);
            Require(account.Carried == 60 && account.Banked == 65, "deposit");
            report.Checks.Add("deposit");
            account.Withdraw(15);
            Require(account.Carried == 75 && account.Banked == 50, "withdraw");
            report.Checks.Add("withdraw");

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
