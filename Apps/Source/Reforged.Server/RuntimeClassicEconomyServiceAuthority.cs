namespace Reforged.Server;

public enum RuntimeClassicHealerServiceKind
{
    Healing,
    Curing
}

public sealed record RuntimeClassicHealerServiceQuote(
    RuntimeClassicHealerServiceKind Kind,
    long CostCopper,
    bool WasPoisoned);

/// <summary>
/// Native ShopType-5 healer authority recovered from WCCMMUD. BUY HEALING costs two copper
/// per missing hit point. BUY CURING / BUY CURE POISON costs 15 silver when the character is
/// not poisoned and 25 silver when poisoned. Both services debit before applying the effect.
/// </summary>
public static class RuntimeClassicHealerServiceAuthority
{
    public const string HealingInsufficientFundsText = "You do not have sufficient funds to buy full healing!";
    public const string CuringInsufficientFundsText = "You do not have sufficient funds to buy curing!";
    public const long HealingCopperPerMissingHitPoint = 2;
    public const long CuringNotPoisonedCopper = 150;
    public const long CuringPoisonedCopper = 250;

    public static bool IsCommand(string argument)
    {
        var normalized = Normalize(argument);
        return normalized is "healing" or "curing" or "cure poison";
    }

    public static long HealingCostCopper(int maximumHitPoints, int currentHitPoints) =>
        checked(Math.Max(0L, (long)maximumHitPoints - currentHitPoints) * HealingCopperPerMissingHitPoint);

    public static long CuringCostCopper(bool poisoned) => poisoned ? CuringPoisonedCopper : CuringNotPoisonedCopper;

    public static RuntimeClassicHealerServiceQuote Quote(RuntimeCharacterRecord character, string argument)
    {
        ArgumentNullException.ThrowIfNull(character);
        var normalized = Normalize(argument);
        if (normalized == "healing")
        {
            return new(RuntimeClassicHealerServiceKind.Healing,
                HealingCostCopper(character.MaximumHitPoints, character.HitPoints),
                RuntimeClassicStatusProjection.Flag(character, "poisoned"));
        }
        if (normalized is "curing" or "cure poison")
        {
            var poisoned = RuntimeClassicStatusProjection.Flag(character, "poisoned");
            return new(RuntimeClassicHealerServiceKind.Curing, CuringCostCopper(poisoned), poisoned);
        }
        throw new InvalidOperationException("Unsupported native healer service command.");
    }

    private static string Normalize(string value) =>
        string.Join(' ', (value ?? string.Empty).Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

public static class RuntimeClassicShopTransactionAuthority
{
    public const string InsufficientFundsFormat = "You cannot afford {0}.";

    public static string CannotAfford(string itemName) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, InsufficientFundsFormat, itemName);
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> ClassicShopTransactionGates =
        new(StringComparer.OrdinalIgnoreCase);
    private string ExecuteClassicType5HealerService(RuntimeSession session, RuntimeCharacterRecord initial, string argument)
    {
        var before = _characters.LoadCharacter(initial.CharacterId)
            ?? throw new InvalidOperationException("Character save was not found for the healer service.");
        var quote = RuntimeClassicHealerServiceAuthority.Quote(before, argument);
        var beforeState = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0,
            new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        if (RuntimeBundleCInventoryAuthority.ReadCurrency(beforeState).CopperValue < quote.CostCopper)
            return quote.Kind == RuntimeClassicHealerServiceKind.Healing
                ? RuntimeClassicHealerServiceAuthority.HealingInsufficientFundsText
                : RuntimeClassicHealerServiceAuthority.CuringInsufficientFundsText;

        var debitedState = RuntimeBundleCEconomyAuthority.DebitCopperValue(beforeState, quote.CostCopper);
        _characters.SaveCharacter(before with { ItemState = debitedState, UpdatedAtUtc = DateTimeOffset.UtcNow },
            $"classic-shoptype5:{quote.Kind.ToString().ToLowerInvariant()}:debit");

        RuntimeSpellState? liveBefore = null;
        try
        {
            if (quote.Kind == RuntimeClassicHealerServiceKind.Healing)
            {
                var afterDebit = _characters.LoadCharacter(before.CharacterId) ?? before;
                _characters.SaveCharacter(afterDebit with
                {
                    HitPoints = afterDebit.MaximumHitPoints,
                    ItemState = debitedState,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }, "classic-shoptype5:healing:effect");
            }
            else
            {
                EnsureLivePowers(session);
                liveBefore = _liveSpellEngine!.Snapshot();
                ApplyRecoveredHealerSpell(session, before.CharacterId, 222);
                PersistLivePowers(session.World.Definition.Id);
            }
            var paid = RuntimeBundleCEconomyAuthority.FormatCopperValue(quote.CostCopper);
            return quote.Kind == RuntimeClassicHealerServiceKind.Healing
                ? $"You hand over {paid} and all your wounds are healed."
                : quote.WasPoisoned
                    ? $"You hand over {paid} and your poisoning is cured."
                    : $"You hand over {paid} and find that you were not poisoned!";
        }
        catch
        {
            if (liveBefore is not null && _liveSpellEngine is not null)
            {
                _liveSpellEngine.RestoreSnapshot(liveBefore);
                PersistLivePowers(session.World.Definition.Id);
            }
            _characters.SaveCharacter(before, $"classic-shoptype5:{quote.Kind.ToString().ToLowerInvariant()}:rollback");
            throw;
        }
    }

    private T ExecuteClassicShopTransaction<T>(
        RuntimeServiceDefinition shop,
        string characterId,
        Func<Dictionary<string, RuntimeShopStockEntry>, T> action)
    {
        var candidate = _characters.LoadCharacter(characterId)
            ?? throw new InvalidOperationException("Character save was not found.");
        var gateKey = $"{candidate.WorldId}\u001f{shop.Id}";
        var gate = ClassicShopTransactionGates.GetOrAdd(gateKey, static _ => new object());
        lock (gate)
        {
            var beforeCharacter = _characters.LoadCharacter(characterId)
                ?? throw new InvalidOperationException("Character save was not found.");
            var store = GameplayStore(beforeCharacter.WorldId);
            var beforeShop = store.Read(shop);
            try
            {
                return store.ExecuteShopTransaction(shop, action);
            }
            catch
            {
                _characters.SaveCharacter(beforeCharacter, "classic-shop-transaction-rollback");
                _ = store.ExecuteExactShopStateTransaction(shop, _ =>
                    (beforeShop with { UpdatedAtUtc = DateTimeOffset.UtcNow }, 0));
                throw;
            }
        }
    }
}
