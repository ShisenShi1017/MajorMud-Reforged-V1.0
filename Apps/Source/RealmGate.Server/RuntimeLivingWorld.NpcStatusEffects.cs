using System.Globalization;

namespace RealmGate.Server;

public sealed record RuntimeNpcTimedStatusResult(
    bool Applied,
    string InstanceId,
    string SpellId,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyDictionary<int, int> AbilityModifiers);

public sealed partial class RuntimeLivingWorldService
{
    private const string NpcStatusPrefix = "combat.status.";
    private DateTimeOffset? _lastNativeMonsterPoisonSlowUtc;

    // Original _slow_update_monster (0x421C69..0x421C7F) subtracts
    // monster +0x14 illness severity from monster +0x18 current HP on each
    // slow tick. Caller uses the existing Classic 30-fast-tick scheduler.
    public int TickNativeClassicMonsterPoison(DateTimeOffset nowUtc)
    {
        if (!_world.Definition.Type.Equals("Classic", StringComparison.OrdinalIgnoreCase))
            return 0;
        lock (_gate)
        {
            if (_lastNativeMonsterPoisonSlowUtc == nowUtc) return 0;
            _lastNativeMonsterPoisonSlowUtc = nowUtc;
            var changed = 0;
            foreach (var monster in _npcs.Values.ToArray())
            {
                if (!monster.Alive) continue;
                var severity = RuntimeClassicNativePoisonAuthority.ReadMonster(monster.Metadata);
                if (severity <= 0) continue;
                var hp = RuntimeClassicNativePoisonAuthority.ApplySlowDamage(
                    monster.CurrentHitPoints, severity);
                if (hp == 0)
                {
                    EnterNpcRespawnWaitLocked(monster.InstanceId, nowUtc,
                        "native-monster-poison-slow", null);
                    PublishLivingWorldEventLocked("npc-died", monster.InstanceId,
                        monster.RoomId, nowUtc);
                }
                else
                {
                    _npcs[monster.InstanceId] = monster with { CurrentHitPoints = hp };
                    PublishLivingWorldEventLocked("npc-poison-damage", monster.InstanceId,
                        monster.RoomId, nowUtc);
                }
                changed++;
            }
            if (changed > 0) { Save(); SaveLivingWorldCompletionLocked(); }
            return changed;
        }
    }

    public RuntimeNpcTimedStatusResult ApplyNpcTimedStatus(
        string instanceId,
        string spellId,
        int durationRounds,
        IReadOnlyDictionary<int, int> abilityModifiers,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("NPC instance is required.", nameof(instanceId));
        if (string.IsNullOrWhiteSpace(spellId)) throw new ArgumentException("Spell id is required.", nameof(spellId));
        if (durationRounds <= 0) throw new ArgumentOutOfRangeException(nameof(durationRounds));
        if (abilityModifiers.Count == 0 || abilityModifiers.Any(pair => pair.Key <= 0))
            throw new InvalidDataException("NPC timed status requires decoded positive ability IDs.");

        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var npc) || !npc.Alive)
                return new(false, instanceId, spellId, nowUtc, new Dictionary<int, int>());
            var metadata = new Dictionary<string, string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            var prefix = NpcStatusPrefix + spellId + ".";
            foreach (var key in metadata.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
                metadata.Remove(key);
            var expiresAtUtc = nowUtc.AddSeconds(checked((long)durationRounds * RuntimeClassicLiveStatusCatalog.SpellRoundSeconds));
            metadata[prefix + "duration_rounds"] = durationRounds.ToString(CultureInfo.InvariantCulture);
            metadata[prefix + "applied_at_utc"] = nowUtc.ToString("O", CultureInfo.InvariantCulture);
            metadata[prefix + "expires_at_utc"] = expiresAtUtc.ToString("O", CultureInfo.InvariantCulture);
            metadata[prefix + "source"] = "decoded_npc_self_spell";
            foreach (var ability in abilityModifiers.OrderBy(pair => pair.Key))
                metadata[prefix + "ability." + ability.Key.ToString(CultureInfo.InvariantCulture)] = ability.Value.ToString(CultureInfo.InvariantCulture);
            _npcs[instanceId] = npc with { Metadata = metadata };
            PublishLivingWorldEventLocked("npc-status-applied", instanceId, npc.RoomId, nowUtc,
                new Dictionary<string, string> { ["spell_id"] = spellId, ["expires_at_utc"] = expiresAtUtc.ToString("O", CultureInfo.InvariantCulture) });
            Save();
            return new(true, instanceId, spellId, expiresAtUtc, new Dictionary<int, int>(abilityModifiers));
        }
    }

    public int ResolveNpcTimedAbilityModifier(string instanceId, int abilityId, DateTimeOffset nowUtc)
    {
        if (abilityId <= 0) throw new ArgumentOutOfRangeException(nameof(abilityId));
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var npc) || !npc.Alive) return 0;
            var total = 0;
            foreach (var expiry in npc.Metadata.Where(pair => pair.Key.StartsWith(NpcStatusPrefix, StringComparison.OrdinalIgnoreCase)
                         && pair.Key.EndsWith(".expires_at_utc", StringComparison.OrdinalIgnoreCase)))
            {
                if (!DateTimeOffset.TryParse(expiry.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAtUtc) || expiresAtUtc <= nowUtc)
                    continue;
                var effectPrefix = expiry.Key[..^"expires_at_utc".Length];
                if (npc.Metadata.TryGetValue(effectPrefix + "ability." + abilityId.ToString(CultureInfo.InvariantCulture), out var raw)
                    && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    total = checked(total + value);
            }
            return total;
        }
    }

    private IReadOnlyList<string> TickNpcTimedStatusesLocked(DateTimeOffset nowUtc)
    {
        var events = new List<string>();
        foreach (var npc in _npcs.Values.ToArray())
        {
            var metadata = new Dictionary<string, string>(npc.Metadata, StringComparer.OrdinalIgnoreCase);
            var expiredPrefixes = metadata.Where(pair => pair.Key.StartsWith(NpcStatusPrefix, StringComparison.OrdinalIgnoreCase)
                    && pair.Key.EndsWith(".expires_at_utc", StringComparison.OrdinalIgnoreCase)
                    && DateTimeOffset.TryParse(pair.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAtUtc)
                    && expiresAtUtc <= nowUtc)
                .Select(pair => pair.Key[..^"expires_at_utc".Length]).ToArray();
            foreach (var prefix in expiredPrefixes)
            {
                foreach (var key in metadata.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray()) metadata.Remove(key);
                var spellId = prefix[NpcStatusPrefix.Length..].TrimEnd('.');
                events.Add("npc-status-expired:" + npc.InstanceId + ":" + spellId);
                PublishLivingWorldEventLocked("npc-status-expired", npc.InstanceId, npc.RoomId, nowUtc,
                    new Dictionary<string, string> { ["spell_id"] = spellId });
            }
            if (expiredPrefixes.Length > 0) _npcs[npc.InstanceId] = npc with { Metadata = metadata };
        }
        return events;
    }

    private static void ClearNpcTimedStatuses(IDictionary<string, string> metadata)
    {
        foreach (var key in metadata.Keys.Where(key => key.StartsWith(NpcStatusPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
            metadata.Remove(key);
        // Monster death/respawn clears the native +0x14 illness accumulator.
        metadata.Remove(RuntimeClassicNativePoisonAuthority.MonsterSeverityKey);
    }
}
