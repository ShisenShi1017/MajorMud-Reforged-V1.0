using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

/// <summary>
/// Reforged-native recovery/buff planner. It consumes authoritative session state,
/// persists only per-character client preferences, and returns at most one approved
/// action per evaluation. It never mutates world data and never guesses an unknown spell.
/// </summary>
public sealed class ReforgedRecoveryAutomationService
{
    private readonly string _profileRoot;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ReforgedRecoveryAutomationService(string? rootPath = null)
    {
        var root = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RealmGate");
        _profileRoot = Path.Combine(root, "Client", "RecoveryAutomation");
    }

    public ReforgedRecoveryAutomationProfile LoadProfile(ClientSessionConnection session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var key = BuildProfileKey(session);
        var path = ProfilePath(key);
        if (!File.Exists(path)) return NewProfile(session, key);
        try
        {
            var profile = JsonSerializer.Deserialize<ReforgedRecoveryAutomationProfile>(File.ReadAllText(path), _json)
                          ?? NewProfile(session, key);
            NormalizeProfile(profile, session, key);
            return profile;
        }
        catch (JsonException)
        {
            BackupCorrupt(path);
            return NewProfile(session, key);
        }
        catch (IOException) { return NewProfile(session, key); }
        catch (UnauthorizedAccessException) { return NewProfile(session, key); }
    }

    public void SaveProfile(ClientSessionConnection session, ReforgedRecoveryAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var key = BuildProfileKey(session);
        NormalizeProfile(profile, session, key);
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(_profileRoot);
        var path = ProfilePath(key);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, _json), new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    public ReforgedRecoveryAutomationPlan Plan(
        ClientSessionConnection session,
        ReforgedRecoveryAutomationProfile profile,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var p = profile.Preferences;
        if (!p.Enabled)
            return None("Enhanced recovery/buff automation is disabled.");

        if (!IsFresh(session.PromptUpdatedAt, now, p.StateMaximumAgeSeconds))
            return Refresh(ReforgedRecoveryActionKind.RefreshVitals, "Refreshing current vitals before recovery automation.", vitals: true);

        if (!IsFresh(session.ActiveEffectsUpdatedAt, now, p.StateMaximumAgeSeconds))
            return Refresh(ReforgedRecoveryActionKind.RefreshEffects, "Refreshing authoritative effects before cure/buff decisions.", effects: true);

        if (RequiresSpellbook(profile) && !IsFresh(session.SpellbookUpdatedAt, now, Math.Max(60, p.StateMaximumAgeSeconds * 10)))
            return Refresh(ReforgedRecoveryActionKind.RefreshSpellbook, "Refreshing learned spells before issuing a cast command.", spells: true);

        if (session.IsMoving)
            return None("Waiting for movement to finish before recovery automation acts.");

        var effects = string.Join(' ', session.ActiveEffects);
        if (session.InCombat && p.AutoRecallAtCriticalHealth && session.HealthPercent <= p.RecallAtPercent)
            return Command(ReforgedRecoveryActionKind.EmergencyRecall, p.RecallCommand,
                $"Critical health ({session.HealthPercent}%): emergency recall.", p.CommandCooldownSeconds);

        if (p.AutoUseEmergencyHealingItem && session.HealthPercent <= p.EmergencyHealingItemAtPercent)
            return Command(ReforgedRecoveryActionKind.EmergencyItem, p.EmergencyHealingItemCommand,
                $"Health {session.HealthPercent}%: configured emergency healing item.", p.CommandCooldownSeconds);

        var cure = PlanSelfCure(session, profile, effects);
        if (cure.HasAction) return cure;

        if (p.AutoHealSelf && (!session.InCombat || p.AllowSelfHealingInCombat))
        {
            var heal = session.HealthPercent <= p.MajorHealAtPercent ? p.MajorHealCommand
                : session.HealthPercent <= p.MinorHealAtPercent ? p.MinorHealCommand
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(heal))
            {
                var validation = ValidateCast(session, profile, heal, p.ReserveManaPercent);
                if (validation.Allowed)
                    return Spell(ReforgedRecoveryActionKind.HealSelf, heal,
                        $"Health {session.HealthPercent}%: casting {validation.SpellName}.", p.CommandCooldownSeconds);
            }
        }

        var party = PlanPartyAction(session, profile, now);
        if (party.HasAction) return party;

        // Never break an active rest/meditation cycle just to refresh a buff.
        if (session.IsResting)
        {
            if (!session.InCombat && session.HealthPercent >= p.FinishRecoveryHealthPercent && session.ManaPercent >= p.FinishRecoveryManaPercent)
                return new ReforgedRecoveryAutomationPlan
                {
                    Kind = ReforgedRecoveryActionKind.FinishRecovery,
                    Command = p.StandCommand,
                    Reason = $"Recovery target reached: HP {session.HealthPercent}% / mana {session.ManaPercent}%.",
                    PostEquipmentSetId = p.PostRecoveryEquipmentSetId,
                    CooldownSeconds = p.CommandCooldownSeconds
                };
            return None($"Recovering: HP {session.HealthPercent}% / mana {session.ManaPercent}%.");
        }

        var buff = PlanBuff(session, profile, now);
        if (buff.HasAction) return buff;

        if (!session.InCombat)
        {
            var lowHealth = p.AutoRest && session.HealthPercent <= p.BeginRecoveryHealthPercent;
            var lowMana = (p.AutoRest || p.AutoMeditate) && session.ManaPercent <= p.BeginRecoveryManaPercent;
            if (lowHealth || lowMana)
            {
                var meditate = p.AutoMeditate && lowMana;
                return new ReforgedRecoveryAutomationPlan
                {
                    Kind = meditate ? ReforgedRecoveryActionKind.BeginMeditate : ReforgedRecoveryActionKind.BeginRest,
                    Command = meditate ? p.MeditateCommand : p.RestCommand,
                    Reason = $"Beginning {(meditate ? "meditation" : "rest")}: HP {session.HealthPercent}% / mana {session.ManaPercent}%.",
                    PreEquipmentSetId = p.PreRecoveryEquipmentSetId,
                    CooldownSeconds = p.CommandCooldownSeconds
                };
            }
        }

        return None("Vitals, effects and maintained buffs are within configured limits.");
    }

    private ReforgedRecoveryAutomationPlan PlanSelfCure(
        ClientSessionConnection session,
        ReforgedRecoveryAutomationProfile profile,
        string effects)
    {
        var p = profile.Preferences;
        var candidates = new[]
        {
            (p.CureSilence, "silenc", p.CureSilenceCommand, "silence"),
            (p.CurePoison, "poison", p.CurePoisonCommand, "poison"),
            (p.CureDisease, "disease", p.CureDiseaseCommand, "disease"),
            (p.CureBlindness, "blind", p.CureBlindnessCommand, "blindness")
        };
        foreach (var c in candidates)
        {
            if (!c.Item1 || !effects.Contains(c.Item2, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(c.Item3)) continue;
            var isCast = IsCast(c.Item3);
            if (isCast && effects.Contains("silenc", StringComparison.OrdinalIgnoreCase))
                continue;
            if (isCast)
            {
                var validation = ValidateCast(session, profile, c.Item3, p.ReserveManaPercent);
                if (!validation.Allowed) continue;
            }
            return new ReforgedRecoveryAutomationPlan
            {
                Kind = ReforgedRecoveryActionKind.CureSelf,
                Command = c.Item3.Trim(),
                Reason = $"Removing detected {c.Item4} effect.",
                CooldownSeconds = p.CommandCooldownSeconds,
                IsSpellCommand = isCast
            };
        }
        return None(string.Empty);
    }

    private ReforgedRecoveryAutomationPlan PlanPartyAction(
        ClientSessionConnection session,
        ReforgedRecoveryAutomationProfile profile,
        DateTimeOffset now)
    {
        var p = profile.Preferences;
        if (!p.AutoHealParty && !p.CurePoisonedPartyMembers) return None(string.Empty);
        if (!IsFresh(session.PartyUpdatedAt, now, p.PartyStateMaximumAgeSeconds))
            return Refresh(ReforgedRecoveryActionKind.RefreshParty, "Refreshing party state before party healing.", party: true);

        if (p.CurePoisonedPartyMembers)
        {
            var poisoned = session.PartyMembers.FirstOrDefault(member => member.Poisoned);
            if (poisoned is not null)
            {
                var command = ApplyTarget(p.CurePartyPoisonCommandTemplate, poisoned.Name);
                var validation = ValidateCast(session, profile, command, p.ReserveManaPercent);
                if (!IsCast(command) || validation.Allowed)
                    return new ReforgedRecoveryAutomationPlan
                    {
                        Kind = ReforgedRecoveryActionKind.CureParty,
                        Command = command,
                        Target = poisoned.Name,
                        Reason = $"Party state marks {poisoned.Name} poisoned; applying configured cure.",
                        CooldownSeconds = p.CommandCooldownSeconds,
                        IsSpellCommand = IsCast(command)
                    };
            }
        }

        if (p.AutoHealParty)
        {
            var wounded = session.PartyMembers
                .Where(member => member.HealthPercent < p.HealPartyBelowPercent)
                .OrderBy(member => member.HealthPercent)
                .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (wounded is not null)
            {
                var command = ApplyTarget(p.HealPartyCommandTemplate, wounded.Name);
                var validation = ValidateCast(session, profile, command, p.ReserveManaPercent);
                if (!IsCast(command) || validation.Allowed)
                    return new ReforgedRecoveryAutomationPlan
                    {
                        Kind = ReforgedRecoveryActionKind.HealParty,
                        Command = command,
                        Target = wounded.Name,
                        Reason = $"Healing {wounded.Name} at {wounded.HealthPercent}% HP.",
                        CooldownSeconds = p.CommandCooldownSeconds,
                        IsSpellCommand = IsCast(command)
                    };
            }
        }
        return None(string.Empty);
    }

    private ReforgedRecoveryAutomationPlan PlanBuff(
        ClientSessionConnection session,
        ReforgedRecoveryAutomationProfile profile,
        DateTimeOffset now)
    {
        foreach (var rule in profile.BuffRules.Where(rule => rule.Enabled).OrderBy(rule => rule.Priority).ThenBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (rule.OnlyOutOfCombat && session.InCombat) continue;
            if (string.IsNullOrWhiteSpace(rule.Command) || string.IsNullOrWhiteSpace(rule.EffectMatch)) continue;
            var active = session.ActiveEffectDetails.FirstOrDefault(effect =>
                effect.Name.Contains(rule.EffectMatch, StringComparison.OrdinalIgnoreCase) ||
                effect.SpellId.Equals(rule.EffectMatch, StringComparison.OrdinalIgnoreCase) ||
                effect.EffectId.Equals(rule.EffectMatch, StringComparison.OrdinalIgnoreCase));
            if (active is not null)
            {
                if (active.ExpiresAtUtc is null) continue;
                if (active.ExpiresAtUtc.Value - now > TimeSpan.FromSeconds(rule.RefreshLeadSeconds)) continue;
            }
            var validation = ValidateCast(session, profile, rule.Command, Math.Max(profile.Preferences.ReserveManaPercent, rule.MinimumManaPercent));
            if (IsCast(rule.Command) && !validation.Allowed) continue;
            return new ReforgedRecoveryAutomationPlan
            {
                Kind = ReforgedRecoveryActionKind.MaintainBuff,
                Command = rule.Command.Trim(),
                Reason = active is null ? $"Maintaining missing buff: {rule.Name}." : $"Refreshing {rule.Name} before expiry.",
                CooldownSeconds = profile.Preferences.CommandCooldownSeconds,
                IsSpellCommand = IsCast(rule.Command)
            };
        }
        return None(string.Empty);
    }

    private static (bool Allowed, string SpellName) ValidateCast(
        ClientSessionConnection session,
        ReforgedRecoveryAutomationProfile profile,
        string command,
        int reserveManaPercent)
    {
        if (!IsCast(command)) return (true, string.Empty);
        if (session.ActiveEffects.Any(effect => effect.Contains("silenc", StringComparison.OrdinalIgnoreCase))) return (false, string.Empty);
        if (!profile.Preferences.RequireLearnedSpellForCastCommands) return (true, "configured spell");
        var spell = session.LearnedSpellDetails
            .Where(candidate => command.Contains(candidate.Name, StringComparison.OrdinalIgnoreCase) ||
                                command.Contains(candidate.CanonicalId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Name.Length)
            .FirstOrDefault();
        if (spell is null) return (false, string.Empty);
        var reserve = session.ManaMaximum > 0
            ? (int)Math.Ceiling(session.ManaMaximum * Math.Clamp(reserveManaPercent, 0, 100) / 100d)
            : 0;
        return (session.ManaCurrent - spell.ManaCost >= reserve, spell.Name);
    }

    private static string ApplyTarget(string template, string target)
    {
        var normalized = template?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return string.Empty;
        return normalized.Contains("{target}", StringComparison.OrdinalIgnoreCase)
            ? normalized.Replace("{target}", target, StringComparison.OrdinalIgnoreCase)
            : $"{normalized} {target}".Trim();
    }

    private static bool RequiresSpellbook(ReforgedRecoveryAutomationProfile profile)
    {
        var p = profile.Preferences;
        if (!p.RequireLearnedSpellForCastCommands) return false;
        var commands = new List<string>();
        if (p.AutoHealSelf) { commands.Add(p.MinorHealCommand); commands.Add(p.MajorHealCommand); }
        if (p.CurePoison) commands.Add(p.CurePoisonCommand);
        if (p.CureDisease) commands.Add(p.CureDiseaseCommand);
        if (p.CureBlindness) commands.Add(p.CureBlindnessCommand);
        if (p.CureSilence) commands.Add(p.CureSilenceCommand);
        if (p.AutoHealParty) commands.Add(p.HealPartyCommandTemplate);
        if (p.CurePoisonedPartyMembers) commands.Add(p.CurePartyPoisonCommandTemplate);
        return commands.Any(IsCast) || profile.BuffRules.Any(rule => rule.Enabled && IsCast(rule.Command));
    }

    private static bool IsCast(string? command) =>
        !string.IsNullOrWhiteSpace(command) && command.TrimStart().StartsWith("cast ", StringComparison.OrdinalIgnoreCase);

    private static bool IsFresh(DateTimeOffset? capturedAt, DateTimeOffset now, int seconds) =>
        capturedAt is not null && now - capturedAt.Value <= TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 3600));

    private static ReforgedRecoveryAutomationPlan None(string reason) => new() { Kind = ReforgedRecoveryActionKind.None, Reason = reason };

    private static ReforgedRecoveryAutomationPlan Refresh(
        ReforgedRecoveryActionKind kind, string reason,
        bool vitals = false, bool effects = false, bool spells = false, bool party = false) => new()
    {
        Kind = kind,
        Reason = reason,
        RefreshVitals = vitals,
        RefreshEffects = effects,
        RefreshSpellbook = spells,
        RefreshParty = party,
        CooldownSeconds = 2
    };

    private static ReforgedRecoveryAutomationPlan Command(
        ReforgedRecoveryActionKind kind, string command, string reason, int cooldown) => new()
    {
        Kind = kind,
        Command = command?.Trim() ?? string.Empty,
        Reason = reason,
        CooldownSeconds = Math.Max(1, cooldown),
        IsSpellCommand = IsCast(command)
    };

    private static ReforgedRecoveryAutomationPlan Spell(
        ReforgedRecoveryActionKind kind, string command, string reason, int cooldown) => new()
    {
        Kind = kind,
        Command = command.Trim(),
        Reason = reason,
        CooldownSeconds = Math.Max(1, cooldown),
        IsSpellCommand = true
    };

    public static string BuildProfileKey(ClientSessionConnection session)
    {
        var raw = string.Join("|",
            session.ServerProfileName?.Trim().ToLowerInvariant() ?? string.Empty,
            session.WorldName?.Trim().ToLowerInvariant() ?? string.Empty,
            session.HostName?.Trim().ToLowerInvariant() ?? string.Empty,
            session.Port.ToString(CultureInfo.InvariantCulture),
            session.CharacterName?.Trim().ToLowerInvariant() ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..24];
    }

    private ReforgedRecoveryAutomationProfile NewProfile(ClientSessionConnection session, string key) => new()
    {
        ProfileKey = key,
        CharacterName = session.CharacterName,
        ServerProfileName = session.ServerProfileName,
        WorldName = session.WorldName,
        BuffRules =
        [
            new ReforgedBuffRule { Priority = 100, Name = "Bless", EffectMatch = "bless", Command = "cast bless", Enabled = false },
            new ReforgedBuffRule { Priority = 200, Name = "Armor", EffectMatch = "armor", Command = "cast armor", Enabled = false }
        ]
    };

    private static void NormalizeProfile(ReforgedRecoveryAutomationProfile profile, ClientSessionConnection session, string key)
    {
        profile.Version = 1;
        profile.ProfileKey = key;
        profile.CharacterName = session.CharacterName ?? string.Empty;
        profile.ServerProfileName = session.ServerProfileName ?? string.Empty;
        profile.WorldName = session.WorldName ?? string.Empty;
        profile.Preferences ??= new ReforgedRecoveryAutomationPreferences();
        profile.BuffRules ??= [];
        var p = profile.Preferences;
        p.StateMaximumAgeSeconds = Math.Clamp(p.StateMaximumAgeSeconds, 5, 600);
        p.PartyStateMaximumAgeSeconds = Math.Clamp(p.PartyStateMaximumAgeSeconds, 5, 600);
        p.CommandCooldownSeconds = Math.Clamp(p.CommandCooldownSeconds, 1, 60);
        p.ReserveManaPercent = Math.Clamp(p.ReserveManaPercent, 0, 100);
        p.MinorHealAtPercent = Math.Clamp(p.MinorHealAtPercent, 1, 100);
        p.MajorHealAtPercent = Math.Clamp(p.MajorHealAtPercent, 1, p.MinorHealAtPercent);
        p.EmergencyHealingItemAtPercent = Math.Clamp(p.EmergencyHealingItemAtPercent, 1, 100);
        p.RecallAtPercent = Math.Clamp(p.RecallAtPercent, 1, 100);
        p.BeginRecoveryHealthPercent = Math.Clamp(p.BeginRecoveryHealthPercent, 1, 100);
        p.BeginRecoveryManaPercent = Math.Clamp(p.BeginRecoveryManaPercent, 0, 100);
        p.FinishRecoveryHealthPercent = Math.Clamp(p.FinishRecoveryHealthPercent, p.BeginRecoveryHealthPercent, 100);
        p.FinishRecoveryManaPercent = Math.Clamp(p.FinishRecoveryManaPercent, p.BeginRecoveryManaPercent, 100);
        p.HealPartyBelowPercent = Math.Clamp(p.HealPartyBelowPercent, 1, 100);
        p.PreRecoveryEquipmentSetId ??= string.Empty;
        p.PostRecoveryEquipmentSetId ??= string.Empty;
        p.MinorHealCommand ??= string.Empty;
        p.MajorHealCommand ??= string.Empty;
        p.EmergencyHealingItemCommand ??= string.Empty;
        p.RecallCommand ??= string.Empty;
        p.CurePoisonCommand ??= string.Empty;
        p.CureDiseaseCommand ??= string.Empty;
        p.CureBlindnessCommand ??= string.Empty;
        p.CureSilenceCommand ??= string.Empty;
        p.RestCommand ??= string.Empty;
        p.MeditateCommand ??= string.Empty;
        p.StandCommand ??= string.Empty;
        p.HealPartyCommandTemplate ??= string.Empty;
        p.CurePartyPoisonCommandTemplate ??= string.Empty;
        foreach (var rule in profile.BuffRules)
        {
            rule.Id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id;
            rule.Name = string.IsNullOrWhiteSpace(rule.Name) ? "Buff" : rule.Name.Trim();
            rule.EffectMatch = rule.EffectMatch?.Trim() ?? string.Empty;
            rule.Command = rule.Command?.Trim() ?? string.Empty;
            rule.Priority = Math.Clamp(rule.Priority, -100000, 100000);
            rule.RefreshLeadSeconds = Math.Clamp(rule.RefreshLeadSeconds, 0, 3600);
            rule.MinimumManaPercent = Math.Clamp(rule.MinimumManaPercent, 0, 100);
        }
    }

    private string ProfilePath(string key) => Path.Combine(_profileRoot, key + ".json");

    private static void BackupCorrupt(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            File.Move(path, path + $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}", true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
