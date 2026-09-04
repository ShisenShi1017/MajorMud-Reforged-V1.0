using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicGangMember(string CharacterId, string DisplayName, string Role, DateTimeOffset JoinedAtUtc);

public sealed record RuntimeClassicGangRecord(
    string GangId,
    string Name,
    string LeaderCharacterId,
    IReadOnlyList<RuntimeClassicGangMember> Members,
    int TheoreticalSize,
    long Experience,
    bool ToptenEnabled,
    bool Disbanded,
    IReadOnlyList<string> PendingDisbandNotificationCharacterIds,
    string? GangHouseId,
    long Revision,
    DateTimeOffset UpdatedAtUtc)
{
    public IReadOnlyList<string> PendingInviteCharacterIds { get; init; } = [];
    public string? GangHouseDeedHolderCharacterId { get; init; }
    public DateTimeOffset? GangHouseUpkeepPaidThroughUtc { get; init; }
    public DateTimeOffset? LastGangHouseUpkeepPaidAtUtc { get; init; }
    public string? LastGangHouseSundownCycleId { get; init; }
}

public sealed class RuntimeClassicGangStateService
{
    private readonly string _folder;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object InvitationMigrationGate = new();
    private static readonly HashSet<string> MigratedInvitationScopes = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeClassicGangStateService(string projectRoot)
    {
        _folder = Path.Combine(RuntimeSavePaths.RealmRoot(Path.GetFullPath(projectRoot), "classic"), "gangs");
        MigrateLegacyPersistedInvitationsOnce();
    }

    public IReadOnlyList<RuntimeClassicGangRecord> List() => Directory.Exists(_folder)
        ? Directory.EnumerateFiles(_folder, "*.json", SearchOption.TopDirectoryOnly).Select(LoadPath).OrderBy(gang => gang.Name, StringComparer.OrdinalIgnoreCase).ToArray()
        : [];

    public RuntimeClassicGangRecord? FindExact(string name)
    {
        var matches = List().Where(gang => gang.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public RuntimeClassicGangRecord Save(RuntimeClassicGangRecord gang)
    {
        Validate(gang);
        Directory.CreateDirectory(_folder);
        // Native invitations are process-local and must never survive in the gang record.
        var normalized = gang with
        {
            PendingInviteCharacterIds = [],
            Revision = checked(gang.Revision + 1),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var path = Path.Combine(_folder, RuntimeIdentity.Normalize(normalized.GangId, "gang") + ".json");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(normalized, JsonOptions));
        File.Move(temporary, path, true);
        return normalized;
    }

    public RuntimeClassicGangRecord Create(string name, RuntimeCharacterRecord leader)
    {
        if (leader.Experience < 100000) throw new InvalidOperationException("Creating a gang requires 100000 experience.");
        name = name.Trim();
        if (name.Length is < 1 or > 19) throw new InvalidOperationException("Gang names must contain 1 to 19 characters.");
        if (List().Any(gang => gang.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("That gang name already exists.");
        if (FindByMember(leader.CharacterId) is not null) throw new InvalidOperationException("You already belong to a gang.");
        var now = DateTimeOffset.UtcNow;
        return Save(new RuntimeClassicGangRecord(
            "gang." + RuntimeIdentity.Normalize(name, "gang"), name, leader.CharacterId,
            [new RuntimeClassicGangMember(leader.CharacterId, leader.DisplayName, "Leader", now)],
            1, 0, true, false, [], null, 0, now));
    }

    public RuntimeClassicGangRecord? FindByMember(string characterId) =>
        List().SingleOrDefault(gang => !gang.Disbanded && gang.Members.Any(member => member.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase)));

    public bool Invite(RuntimeClassicGangRecord gang, string actorId, string targetId, bool leaderOnline)
    {
        RequireInviteAuthority(gang, actorId, leaderOnline);
        if (FindByMember(targetId) is not null) throw new InvalidOperationException("That character already belongs to a gang.");
        // _invite_to_gang returns false for an exact duplicate target+gang node and does not
        // mutate the persistent gang record.
        return RuntimeClassicGangInvitationRegistry.Add(_folder, targetId, gang.Name);
    }

    public RuntimeClassicGangRecord Uninvite(RuntimeClassicGangRecord gang, string actorId, string targetId, bool leaderOnline, bool targetOnline)
    {
        RequireInviteAuthority(gang, actorId, leaderOnline);
        // The live-user native path calls _remove_from_gang, whose first operation is
        // _clear_gang_invitations(target, gangName), even when the target is only invited.
        // The offline-record path has no such transient-list call.
        if (targetOnline)
            RuntimeClassicGangInvitationRegistry.ClearForCharacter(_folder, targetId, gang.Name);

        var members = gang.Members.Where(member => !member.CharacterId.Equals(targetId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (members.Length == gang.Members.Count)
            throw new InvalidOperationException("That character is not a member of your gang.");
        return Save(gang with { Members = members, TheoreticalSize = Math.Max(0, gang.TheoreticalSize - 1) });
    }

    /// <summary>Native _clear_gang_invitations(leavingUsr, NULL).</summary>
    public int ClearPendingInvitationsForCharacter(string characterId) =>
        RuntimeClassicGangInvitationRegistry.ClearForCharacter(_folder, characterId);

    public RuntimeClassicGangRecord Join(RuntimeClassicGangRecord gang, RuntimeCharacterRecord character)
    {
        if (FindByMember(character.CharacterId) is not null) throw new InvalidOperationException("You already belong to a gang.");
        if (!RuntimeClassicGangInvitationRegistry.Contains(_folder, character.CharacterId, gang.Name))
            throw new InvalidOperationException("You have not been invited to that gang.");

        // Native _join_gang does not inspect the disband marker after resolving the gang record;
        // a still-live transient invitation can therefore join a marked record until its normal
        // drain/cleanup path removes it. Preserve that edge rather than inventing an early reject.
        var members = gang.Members.Append(new RuntimeClassicGangMember(character.CharacterId, character.DisplayName, "Member", DateTimeOffset.UtcNow)).ToArray();
        var pendingDisband = gang.Disbanded
            ? gang.PendingDisbandNotificationCharacterIds.Append(character.CharacterId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : gang.PendingDisbandNotificationCharacterIds;
        var updated = Save(gang with
        {
            Members = members,
            TheoreticalSize = checked(gang.TheoreticalSize + 1),
            PendingDisbandNotificationCharacterIds = pendingDisband
        });
        // Native successful JOIN consumes all outstanding gang invites for the user through the
        // same lifecycle clear owner, not only the selected gang invite.
        RuntimeClassicGangInvitationRegistry.ClearForCharacter(_folder, character.CharacterId);
        return updated;
    }

    public RuntimeClassicGangRecord Leave(RuntimeClassicGangRecord gang, string characterId)
    {
        if (gang.LeaderCharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The gang leader must disband the gang rather than leave it.");
        var members = gang.Members.Where(member => !member.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (members.Length == gang.Members.Count) throw new InvalidOperationException("You do not belong to that gang.");
        return Save(gang with { Members = members, TheoreticalSize = Math.Max(0, gang.TheoreticalSize - 1) });
    }

    public RuntimeClassicGangRecord SetMemberRole(RuntimeClassicGangRecord gang, string leaderId, string targetId, string role)
    {
        if (!gang.LeaderCharacterId.Equals(leaderId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Only the gang leader may promote or demote members.");
        if (targetId.Equals(leaderId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The gang leader role cannot be changed.");
        if (role is not ("Member" or "Lieutenant")) throw new InvalidOperationException("Unsupported gang member role.");
        var found = false;
        var members = gang.Members.Select(member =>
        {
            if (!member.CharacterId.Equals(targetId, StringComparison.OrdinalIgnoreCase)) return member;
            found = true;
            return member with { Role = role };
        }).ToArray();
        if (!found) throw new InvalidOperationException("That character is not a member of your gang.");
        return Save(gang with { Members = members });
    }

    public void RemoveCharacterForReroll(string characterId)
    {
        foreach (var gang in List().Where(gang => !gang.Disbanded).ToArray())
        {
            if (gang.LeaderCharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase))
            {
                Disband(gang);
                continue;
            }

            var members = gang.Members.Where(member => !member.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var invites = gang.PendingInviteCharacterIds.Where(id => !id.Equals(characterId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var notices = gang.PendingDisbandNotificationCharacterIds.Where(id => !id.Equals(characterId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (members.Length != gang.Members.Count || invites.Length != gang.PendingInviteCharacterIds.Count || notices.Length != gang.PendingDisbandNotificationCharacterIds.Count)
                Save(gang with
                {
                    Members = members,
                    PendingInviteCharacterIds = invites,
                    PendingDisbandNotificationCharacterIds = notices,
                    TheoreticalSize = Math.Max(members.Length, gang.TheoreticalSize)
                });
        }
    }

    public IReadOnlyList<string> ConsumeDisbandNotifications(string characterId)
    {
        var notifications = new List<string>();
        foreach (var gang in List().Where(gang => gang.Disbanded && gang.PendingDisbandNotificationCharacterIds.Contains(characterId, StringComparer.OrdinalIgnoreCase)))
        {
            notifications.Add(gang.Name);
            var members = gang.Members.Where(member => !member.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase)).ToArray();
            Save(gang with
            {
                Members = members,
                TheoreticalSize = Math.Max(0, gang.TheoreticalSize - 1),
                PendingDisbandNotificationCharacterIds = gang.PendingDisbandNotificationCharacterIds.Where(id => !id.Equals(characterId, StringComparison.OrdinalIgnoreCase)).ToArray()
            });
        }
        return notifications;
    }

    private static void RequireInviteAuthority(RuntimeClassicGangRecord gang, string actorId, bool leaderOnline)
    {
        var member = gang.Members.SingleOrDefault(member => member.CharacterId.Equals(actorId, StringComparison.OrdinalIgnoreCase));
        if (member is null) throw new InvalidOperationException("You do not belong to that gang.");
        if (member.Role.Equals("Leader", StringComparison.OrdinalIgnoreCase)) return;
        if (member.Role.Equals("Lieutenant", StringComparison.OrdinalIgnoreCase) && !leaderOnline) return;
        throw new InvalidOperationException("Only the gang leader may manage invitations while the gang leader is online.");
    }

    public RuntimeClassicGangRecord Disband(RuntimeClassicGangRecord gang) => Disband(gang, Array.Empty<string>());

    /// <summary>
    /// Native _remove_from_gang leader path: mark the gang disbanded, strip every currently live
    /// member immediately, and retain only offline former members in the native member count until
    /// their next character load consumes the disband marker. No successor leader is elected.
    /// </summary>
    public RuntimeClassicGangRecord Disband(RuntimeClassicGangRecord gang, IEnumerable<string> onlineCharacterIds)
    {
        var online = onlineCharacterIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remainingOffline = gang.Members.Where(member => !online.Contains(member.CharacterId)).ToArray();
        return Save(gang with
        {
            Members = remainingOffline,
            TheoreticalSize = remainingOffline.Length,
            Disbanded = true,
            ToptenEnabled = false,
            PendingDisbandNotificationCharacterIds = remainingOffline.Select(member => member.CharacterId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    /// <summary>Native cleanup deletes gang records whose member count is zero (or force-delete bit is set).</summary>
    public IReadOnlyList<string> CleanupDisbandedGangs()
    {
        var deleted = new List<string>();
        foreach (var gang in List().Where(gang => gang.Disbanded && gang.TheoreticalSize == 0))
        {
            var path = Path.Combine(_folder, RuntimeIdentity.Normalize(gang.GangId, "gang") + ".json");
            if (File.Exists(path)) File.Delete(path);
            deleted.Add(gang.Name);
        }
        return deleted;
    }

    public RuntimeClassicGangRecord SetTopten(RuntimeClassicGangRecord gang, bool enabled) =>
        Save(gang with { ToptenEnabled = enabled });

    public RuntimeClassicGangRecord SetTheoreticalSize(RuntimeClassicGangRecord gang, int size)
    {
        if (size < gang.Members.Count) throw new InvalidOperationException($"Gang size cannot be smaller than the persisted roster ({gang.Members.Count}).");
        return Save(gang with { TheoreticalSize = size });
    }

    public RuntimeClassicGangRecord AwardMemberExperience(string characterId, long amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var gang = FindByMember(characterId) ?? throw new InvalidOperationException("Character does not belong to an active gang.");
        return Save(gang with { Experience = checked(gang.Experience + amount) });
    }

    public RuntimeClassicGangRecord AssignHouse(RuntimeClassicGangRecord gang, string leaderCharacterId, string houseId)
    {
        if (gang.Disbanded) throw new InvalidOperationException("A disbanded gang cannot acquire a house.");
        if (!gang.LeaderCharacterId.Equals(leaderCharacterId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the gang leader may activate a gang-house deed.");
        if (!string.IsNullOrWhiteSpace(gang.GangHouseId))
            throw new InvalidOperationException($"{gang.Name} already owns a gang house.");
        if (List().Any(other => !other.Disbanded && !other.GangId.Equals(gang.GangId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(other.GangHouseId, houseId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("That gang house is already owned by another active gang.");
        return Save(gang with
        {
            GangHouseId = houseId,
            GangHouseDeedHolderCharacterId = leaderCharacterId,
            GangHouseUpkeepPaidThroughUtc = null,
            LastGangHouseUpkeepPaidAtUtc = null,
            LastGangHouseSundownCycleId = null
        });
    }

    public RuntimeClassicGangRecord RecordHouseUpkeep(RuntimeClassicGangRecord gang, string cycleId, DateTimeOffset paidAtUtc) =>
        Save(gang with
        {
            LastGangHouseSundownCycleId = cycleId,
            LastGangHouseUpkeepPaidAtUtc = paidAtUtc,
            GangHouseUpkeepPaidThroughUtc = null
        });

    public RuntimeClassicGangRecord RepossessHouse(RuntimeClassicGangRecord gang, string cycleId) => Save(gang with
    {
        GangHouseId = null,
        GangHouseUpkeepPaidThroughUtc = null,
        LastGangHouseUpkeepPaidAtUtc = null,
        LastGangHouseSundownCycleId = cycleId
    });

    public IReadOnlyList<RuntimeClassicGangRecord> Top(int count) => List()
        .Where(gang => !gang.Disbanded && gang.ToptenEnabled)
        .OrderByDescending(gang => gang.Experience)
        // Stable ordering keeps the existing source order for equal experience,
        // matching WCCMMUD!_generate_top_list's JAE non-displacement rule.
        .Take(Math.Clamp(count, 1, 100))
        .ToArray();

    private void MigrateLegacyPersistedInvitationsOnce()
    {
        lock (InvitationMigrationGate)
        {
            if (!MigratedInvitationScopes.Add(_folder) || !Directory.Exists(_folder)) return;
            foreach (var path in Directory.EnumerateFiles(_folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                var legacy = JsonSerializer.Deserialize<RuntimeClassicGangRecord>(File.ReadAllText(path), JsonOptions);
                if (legacy is null || legacy.PendingInviteCharacterIds.Count == 0) continue;
                foreach (var targetId in legacy.PendingInviteCharacterIds.Distinct(StringComparer.OrdinalIgnoreCase))
                    RuntimeClassicGangInvitationRegistry.Add(_folder, targetId, legacy.Name);

                // Compatibility migration is not a gameplay save: keep revision/timestamp exact
                // while removing the obsolete persisted projection from disk.
                var normalized = legacy with { PendingInviteCharacterIds = [] };
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(normalized, JsonOptions));
                File.Move(temporary, path, true);
            }
        }
    }

    private static RuntimeClassicGangRecord LoadPath(string path)
    {
        var gang = JsonSerializer.Deserialize<RuntimeClassicGangRecord>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Gang save is empty: {path}");
        Validate(gang);
        return gang;
    }

    private static void Validate(RuntimeClassicGangRecord gang)
    {
        if (string.IsNullOrWhiteSpace(gang.GangId) || string.IsNullOrWhiteSpace(gang.Name)) throw new InvalidDataException("Gang identity and name are required.");
        if (gang.TheoreticalSize < gang.Members.Count) throw new InvalidDataException("Gang theoretical size cannot be smaller than its roster.");
        if (gang.Members.Select(member => member.CharacterId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != gang.Members.Count) throw new InvalidDataException("Gang roster contains duplicate characters.");
        if (!gang.Disbanded && gang.Members.Count > 0 && !gang.Members.Any(member => member.CharacterId.Equals(gang.LeaderCharacterId, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Gang leader must belong to the roster.");
    }
}
