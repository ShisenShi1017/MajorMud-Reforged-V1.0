using System.Globalization;

namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private sealed record RuntimeClassicShareOutcome(
        long RequestedAmount,
        long PerRecipient,
        long DistributedAmount,
        long Remainder,
        string Denomination,
        IReadOnlyList<string> RecipientIds);

    /// <summary>
    /// MajorMUD SHARE evidence recovered from WCCMMHLP message 227:
    /// SHARE &lt;amount&gt; &lt;currency&gt; [WITH &lt;ROOM/PARTY&gt;], PARTY by default;
    /// divide by eligible recipients, keep the integer remainder, and exclude hidden
    /// recipients or recipients whose RECEIVE state blocks incoming transfers.
    /// Exact failure and success prose remains unclaimed.
    /// </summary>
    public string ExecuteClassicShare(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor sourceActor)
            return "A persistent character is required.";

        var parsed = ParseClassicShare(command.Arguments);
        var candidateSessions = ResolveClassicShareCandidates(session, parsed.Scope);
        var candidateIds = candidateSessions
            .Where(target => target.Actor is RuntimeCharacterActor)
            .Select(target => target.Actor.Id)
            .Where(id => !id.Equals(sourceActor.Id, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidateIds.Length == 0)
            return parsed.Scope == "party"
                ? "There are no other connected party members eligible for SHARE."
                : "There are no other connected characters here eligible for SHARE.";

        var transactionIds = candidateIds.Prepend(sourceActor.Id).ToArray();
        var outcome = _characters.ExecuteCharacterBatchTransaction(transactionIds, characters =>
        {
            var mutable = characters.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            var source = mutable[sourceActor.Id];
            var eligible = candidateIds
                .Where(id => mutable.TryGetValue(id, out var target)
                    && !IsClassicShareHidden(target)
                    && IsClassicReceiveEnabled(target))
                .ToArray();

            if (eligible.Length == 0)
                return (mutable, new RuntimeClassicShareOutcome(parsed.Amount, 0, 0, parsed.Amount, parsed.Denomination, eligible));

            var sourceState = source.ItemState ?? EmptyItemState();
            var sourceCurrency = RuntimeBundleCInventoryAuthority.ReadCurrency(sourceState);
            var available = ReadDenomination(sourceCurrency, parsed.Denomination);
            // Native validation order/message are still unresolved. Requiring the requested
            // exact denomination prevents the compatibility runtime from silently making change.
            if (available < parsed.Amount)
                throw new InvalidOperationException($"You do not have {parsed.Amount} {parsed.Denomination} to share.");

            var perRecipient = parsed.Amount / eligible.Length;
            var distributed = checked(perRecipient * eligible.Length);
            var remainder = parsed.Amount - distributed;
            if (distributed == 0)
                return (mutable, new RuntimeClassicShareOutcome(parsed.Amount, 0, 0, remainder, parsed.Denomination, eligible));

            var sourceAfter = AdjustDenomination(sourceCurrency, parsed.Denomination, -distributed);
            mutable[sourceActor.Id] = source with
            {
                ItemState = RuntimeBundleCInventoryAuthority.WriteCurrency(sourceState, sourceAfter),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            foreach (var id in eligible)
            {
                var target = mutable[id];
                var targetState = target.ItemState ?? EmptyItemState();
                var targetCurrency = RuntimeBundleCInventoryAuthority.ReadCurrency(targetState);
                mutable[id] = target with
                {
                    ItemState = RuntimeBundleCInventoryAuthority.WriteCurrency(
                        targetState,
                        AdjustDenomination(targetCurrency, parsed.Denomination, perRecipient)),
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
            }

            return (mutable, new RuntimeClassicShareOutcome(
                parsed.Amount, perRecipient, distributed, remainder, parsed.Denomination, eligible));
        });

        foreach (var id in outcome.RecipientIds)
        {
            var target = _sessions.ActiveSessions.FirstOrDefault(active => active.Actor.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (target is not null && outcome.PerRecipient > 0)
                target.EnqueueMessage($"{session.Actor.DisplayName} shares {outcome.PerRecipient} {outcome.Denomination} with you.");
        }

        if (outcome.RecipientIds.Count == 0)
            return "No eligible recipients were included in the SHARE distribution.";
        if (outcome.PerRecipient == 0)
            return $"The amount does not divide into at least one {outcome.Denomination} per eligible recipient; you retain all {outcome.Remainder}.";

        return outcome.Remainder == 0
            ? $"You share {outcome.DistributedAmount} {outcome.Denomination} among {outcome.RecipientIds.Count} recipient(s), {outcome.PerRecipient} each."
            : $"You share {outcome.DistributedAmount} {outcome.Denomination} among {outcome.RecipientIds.Count} recipient(s), {outcome.PerRecipient} each, and retain {outcome.Remainder}.";
    }

    private (long Amount, string Denomination, string Scope) ParseClassicShare(IReadOnlyList<string> arguments)
    {
        if (arguments.Count is not (2 or 4))
            throw new InvalidOperationException("Use: SHARE <amount> <currency> [WITH <ROOM/PARTY>].");
        if (!long.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            throw new InvalidOperationException("SHARE amount must be a positive whole number.");

        var denomination = CanonicalShareDenomination(arguments[1]);
        var scope = "party";
        if (arguments.Count == 4)
        {
            if (!arguments[2].Equals("with", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use: SHARE <amount> <currency> [WITH <ROOM/PARTY>].");
            scope = arguments[3].Trim().ToLowerInvariant();
            if (scope is not ("room" or "party"))
                throw new InvalidOperationException("SHARE scope must be ROOM or PARTY.");
        }
        return (amount, denomination, scope);
    }

    private IReadOnlyList<RuntimeSession> ResolveClassicShareCandidates(RuntimeSession source, string scope)
    {
        if (scope == "room")
        {
            return _sessions.ActiveSessions
                .Where(target => target.World.Definition.Id.Equals(source.World.Definition.Id, StringComparison.OrdinalIgnoreCase)
                    && target.CurrentRoom.Id.Equals(source.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)
                    && !target.Actor.Id.Equals(source.Actor.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        SynchronizeClassicPartyActiveLocations();
        var party = _partyGameplay.FindByMember(source.Actor.Id);
        if (party is null) return Array.Empty<RuntimeSession>();
        return party.Members
            .Where(member => member.Connected && !member.ActorId.Equals(source.Actor.Id, StringComparison.OrdinalIgnoreCase))
            .Select(member => _sessions.ActiveSessions.FirstOrDefault(active => active.Actor.Id.Equals(member.ActorId, StringComparison.OrdinalIgnoreCase)))
            .Where(active => active is not null && active.World.Definition.Id.Equals(source.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
            .Cast<RuntimeSession>()
            .ToArray();
    }

    private static bool IsClassicShareHidden(RuntimeCharacterRecord character)
    {
        foreach (var key in new[] { "hidden", "state.hidden", "combat.hidden" })
        {
            if (!character.Conditions.TryGetValue(key, out var raw)) continue;
            if (raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("on", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static RuntimeItemState EmptyItemState() =>
        new(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());

    private static string CanonicalShareDenomination(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "runic" or "runics" or "rune" or "runes" => "runic",
        "platinum" or "platinums" or "plat" => "platinum",
        "gold" or "golds" or "crown" or "crowns" => "gold",
        "silver" or "silvers" or "noble" or "nobles" => "silver",
        "copper" or "coppers" or "farthing" or "farthings" => "copper",
        _ => throw new InvalidOperationException($"Unknown SHARE currency '{raw}'.")
    };

    private static long ReadDenomination(RuntimeClassicInventoryCurrency value, string denomination) => denomination switch
    {
        "runic" => value.Runic,
        "platinum" => value.Platinum,
        "gold" => value.Gold,
        "silver" => value.Silver,
        "copper" => value.Copper,
        _ => throw new InvalidOperationException("unknown-share-denomination")
    };

    private static RuntimeClassicInventoryCurrency AdjustDenomination(RuntimeClassicInventoryCurrency value, string denomination, long delta)
    {
        var updated = denomination switch
        {
            "runic" => value with { Runic = checked(value.Runic + delta) },
            "platinum" => value with { Platinum = checked(value.Platinum + delta) },
            "gold" => value with { Gold = checked(value.Gold + delta) },
            "silver" => value with { Silver = checked(value.Silver + delta) },
            "copper" => value with { Copper = checked(value.Copper + delta) },
            _ => throw new InvalidOperationException("unknown-share-denomination")
        };
        if (updated.Runic < 0 || updated.Platinum < 0 || updated.Gold < 0 || updated.Silver < 0 || updated.Copper < 0)
            throw new InvalidOperationException("insufficient-share-denomination");
        return updated;
    }
}
