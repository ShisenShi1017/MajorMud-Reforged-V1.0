using System.Text.Json;

namespace RealmGate.Server;

[Flags]
public enum RuntimeOrganizationPermission
{
    None = 0,
    Invite = 1,
    RemoveMember = 2,
    Promote = 4,
    Demote = 8,
    ManageRanks = 16,
    TransferLeadership = 32,
    Disband = 64,
    Speak = 128
}

public enum RuntimeOrganizationKind
{
    Guild,
    Clan,
    Faction,
    Order,
    House,
    Allegiance,
    ClassGuild,
    RaceGuild,
    QuestOrganization,
    AdministrativeGroup
}

public sealed record RuntimeOrganizationPolicy(
    int MaximumMembers,
    TimeSpan InvitationLifetime,
    bool PlayerCreationEnabled,
    bool MemberMayLeave,
    bool PersistAcrossRestart)
{
    public static RuntimeOrganizationPolicy ClassicSafeDefault { get; } = new(
        MaximumMembers: 256,
        InvitationLifetime: TimeSpan.FromMinutes(5),
        PlayerCreationEnabled: false,
        MemberMayLeave: true,
        PersistAcrossRestart: false);
}

public sealed record RuntimeOrganizationRank(
    string RankId,
    string DisplayName,
    int Authority,
    RuntimeOrganizationPermission Permissions);

public sealed record RuntimeOrganizationMember(string ActorId, string RankId, DateTimeOffset JoinedAtUtc);
public sealed record RuntimeOrganizationInvitation(string InvitationId, string OrganizationId, string InviterActorId, string TargetActorId, DateTimeOffset ExpiresAtUtc);
public sealed record RuntimeOrganizationSnapshot(
    string OrganizationId,
    string DisplayName,
    RuntimeOrganizationKind Kind,
    string LeaderActorId,
    IReadOnlyList<RuntimeOrganizationRank> Ranks,
    IReadOnlyList<RuntimeOrganizationMember> Members,
    IReadOnlyList<RuntimeOrganizationInvitation> Invitations,
    RuntimeOrganizationPolicy Policy);
public sealed record RuntimeOrganizationResult(bool Succeeded, string? FailureCode, RuntimeOrganizationSnapshot? Snapshot);
public sealed record RuntimeOrganizationRouteResult(bool Succeeded, string? FailureCode, IReadOnlyList<string> RecipientActorIds);

public sealed class RuntimeOrganizationService
{
    private sealed class OrganizationState
    {
        public required string OrganizationId { get; init; }
        public required string DisplayName { get; init; }
        public required RuntimeOrganizationKind Kind { get; init; }
        public required string LeaderActorId { get; set; }
        public required RuntimeOrganizationPolicy Policy { get; init; }
        public Dictionary<string, RuntimeOrganizationRank> Ranks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, RuntimeOrganizationMember> Members { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, OrganizationState> _organizations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _guildMembership = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeOrganizationInvitation> _invitations = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeOrganizationResult CreateDefinedOrganization(
        string organizationId,
        string displayName,
        RuntimeOrganizationKind kind,
        string leaderActorId,
        DateTimeOffset nowUtc,
        RuntimeOrganizationPolicy? policy = null)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(leaderActorId))
                return Fail("organization-required-field-missing");
            if (_organizations.ContainsKey(organizationId)) return Fail("organization-id-exists");
            if (IsExclusiveGuildKind(kind) && _guildMembership.ContainsKey(leaderActorId)) return Fail("actor-already-in-guild");
            var effective = policy ?? RuntimeOrganizationPolicy.ClassicSafeDefault;
            if (effective.MaximumMembers < 1) return Fail("organization-member-limit-invalid");
            var state = new OrganizationState
            {
                OrganizationId = organizationId,
                DisplayName = displayName,
                Kind = kind,
                LeaderActorId = leaderActorId,
                Policy = effective
            };
            state.Ranks["leader"] = new("leader", "Leader", 1000, RuntimeOrganizationPermission.Invite | RuntimeOrganizationPermission.RemoveMember | RuntimeOrganizationPermission.Promote | RuntimeOrganizationPermission.Demote | RuntimeOrganizationPermission.ManageRanks | RuntimeOrganizationPermission.TransferLeadership | RuntimeOrganizationPermission.Disband | RuntimeOrganizationPermission.Speak);
            state.Ranks["member"] = new("member", "Member", 0, RuntimeOrganizationPermission.Speak);
            state.Members[leaderActorId] = new(leaderActorId, "leader", nowUtc);
            _organizations[organizationId] = state;
            IndexMembership(state, leaderActorId);
            return Success(state);
        }
    }

    public RuntimeOrganizationResult CreatePlayerOrganization(string organizationId, string displayName, RuntimeOrganizationKind kind, string leaderActorId, DateTimeOffset nowUtc, RuntimeOrganizationPolicy policy)
    {
        if (!policy.PlayerCreationEnabled) return Fail("organization-player-creation-disabled");
        return CreateDefinedOrganization(organizationId, displayName, kind, leaderActorId, nowUtc, policy);
    }

    public RuntimeOrganizationResult DefineRank(string actorId, string organizationId, RuntimeOrganizationRank rank)
    {
        lock (_gate)
        {
            if (!_organizations.TryGetValue(organizationId, out var state)) return Fail("organization-not-found");
            if (!HasPermission(state, actorId, RuntimeOrganizationPermission.ManageRanks)) return Fail("organization-permission-denied");
            if (string.IsNullOrWhiteSpace(rank.RankId) || string.IsNullOrWhiteSpace(rank.DisplayName)) return Fail("organization-rank-invalid");
            if (rank.Authority >= 1000 || rank.RankId.Equals("leader", StringComparison.OrdinalIgnoreCase)) return Fail("organization-leader-rank-reserved");
            state.Ranks[rank.RankId] = rank;
            return Success(state);
        }
    }

    public RuntimeOrganizationResult Invite(string inviterActorId, string organizationId, string targetActorId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_organizations.TryGetValue(organizationId, out var state)) return Fail("organization-not-found");
            if (!HasPermission(state, inviterActorId, RuntimeOrganizationPermission.Invite)) return Fail("organization-permission-denied");
            if (state.Members.ContainsKey(targetActorId)) return Fail("organization-target-already-member");
            if (state.Members.Count >= state.Policy.MaximumMembers) return Fail("organization-full");
            if (IsExclusiveGuildKind(state.Kind) && _guildMembership.ContainsKey(targetActorId)) return Fail("actor-already-in-guild");
            RemoveExpired(nowUtc);
            var existing = _invitations.Values.FirstOrDefault(x => x.OrganizationId.Equals(organizationId, StringComparison.OrdinalIgnoreCase) && x.TargetActorId.Equals(targetActorId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) return Fail("organization-invitation-exists");
            var invite = new RuntimeOrganizationInvitation(Guid.NewGuid().ToString("N"), organizationId, inviterActorId, targetActorId, nowUtc + state.Policy.InvitationLifetime);
            _invitations[invite.InvitationId] = invite;
            return Success(state);
        }
    }

    public RuntimeOrganizationResult Accept(string targetActorId, string invitationId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            RemoveExpired(nowUtc);
            if (!_invitations.TryGetValue(invitationId, out var invite) || !invite.TargetActorId.Equals(targetActorId, StringComparison.OrdinalIgnoreCase)) return Fail("organization-invitation-not-found");
            var state = _organizations[invite.OrganizationId];
            if (state.Members.Count >= state.Policy.MaximumMembers) return Fail("organization-full");
            if (IsExclusiveGuildKind(state.Kind) && _guildMembership.ContainsKey(targetActorId)) return Fail("actor-already-in-guild");
            state.Members[targetActorId] = new(targetActorId, "member", nowUtc);
            IndexMembership(state, targetActorId);
            _invitations.Remove(invitationId);
            return Success(state);
        }
    }

    public RuntimeOrganizationResult Decline(string targetActorId, string invitationId)
    {
        lock (_gate)
        {
            if (!_invitations.TryGetValue(invitationId, out var invite) || !invite.TargetActorId.Equals(targetActorId, StringComparison.OrdinalIgnoreCase)) return Fail("organization-invitation-not-found");
            _invitations.Remove(invitationId);
            return _organizations.TryGetValue(invite.OrganizationId, out var state) ? Success(state) : new(true, null, null);
        }
    }

    public RuntimeOrganizationResult SetRank(string actorId, string organizationId, string targetActorId, string rankId)
    {
        lock (_gate)
        {
            if (!_organizations.TryGetValue(organizationId, out var state)) return Fail("organization-not-found");
            if (!state.Members.TryGetValue(actorId, out var actor) || !state.Members.TryGetValue(targetActorId, out var target)) return Fail("organization-member-not-found");
            if (!state.Ranks.TryGetValue(rankId, out var requested)) return Fail("organization-rank-not-found");
            var current = state.Ranks[target.RankId];
            var permission = requested.Authority > current.Authority ? RuntimeOrganizationPermission.Promote : RuntimeOrganizationPermission.Demote;
            if (!HasPermission(state, actorId, permission)) return Fail("organization-permission-denied");
            if (requested.Authority >= state.Ranks[actor.RankId].Authority || requested.RankId.Equals("leader", StringComparison.OrdinalIgnoreCase)) return Fail("organization-rank-authority-denied");
            state.Members[targetActorId] = target with { RankId = rankId };
            return Success(state);
        }
    }

    public RuntimeOrganizationResult Remove(string actorId, string organizationId, string targetActorId)
    {
        lock (_gate)
        {
            if (!_organizations.TryGetValue(organizationId, out var state)) return Fail("organization-not-found");
            if (!state.Members.ContainsKey(targetActorId)) return Fail("organization-member-not-found");
            if (state.LeaderActorId.Equals(targetActorId, StringComparison.OrdinalIgnoreCase)) return Fail("organization-leader-cannot-be-removed");
            if (!HasPermission(state, actorId, RuntimeOrganizationPermission.RemoveMember)) return Fail("organization-permission-denied");
            RemoveMember(state, targetActorId);
            return Success(state);
        }
    }

    public RuntimeOrganizationResult Leave(string actorId, string organizationId)
    {
        lock (_gate)
        {
            if (!_organizations.TryGetValue(organizationId, out var state) || !state.Members.ContainsKey(actorId)) return Fail("organization-member-not-found");
            if (!state.Policy.MemberMayLeave) return Fail("organization-leaving-disabled");
            if (state.LeaderActorId.Equals(actorId, StringComparison.OrdinalIgnoreCase)) return Fail("organization-leader-must-transfer-or-disband");
            RemoveMember(state, actorId);
            return Success(state);
        }
    }

    public RuntimeOrganizationResult TransferLeadership(string actorId, string organizationId, string targetActorId)
    {
        lock (_gate)
        {
            if (!_organizations.TryGetValue(organizationId, out var state)) return Fail("organization-not-found");
            if (!state.LeaderActorId.Equals(actorId, StringComparison.OrdinalIgnoreCase) || !HasPermission(state, actorId, RuntimeOrganizationPermission.TransferLeadership)) return Fail("organization-permission-denied");
            if (!state.Members.TryGetValue(targetActorId, out var target)) return Fail("organization-member-not-found");
            state.Members[actorId] = state.Members[actorId] with { RankId = "member" };
            state.Members[targetActorId] = target with { RankId = "leader" };
            state.LeaderActorId = targetActorId;
            return Success(state);
        }
    }

    public RuntimeOrganizationResult Disband(string actorId, string organizationId)
    {
        lock (_gate)
        {
            if (!_organizations.TryGetValue(organizationId, out var state)) return Fail("organization-not-found");
            if (!state.LeaderActorId.Equals(actorId, StringComparison.OrdinalIgnoreCase) || !HasPermission(state, actorId, RuntimeOrganizationPermission.Disband)) return Fail("organization-permission-denied");
            foreach (var member in state.Members.Keys.ToArray()) RemoveMembershipIndex(state, member);
            foreach (var inviteId in _invitations.Values.Where(x => x.OrganizationId.Equals(organizationId, StringComparison.OrdinalIgnoreCase)).Select(x => x.InvitationId).ToArray()) _invitations.Remove(inviteId);
            _organizations.Remove(organizationId);
            return new(true, null, null);
        }
    }

    public RuntimeOrganizationRouteResult RouteGuildMessage(string actorId)
    {
        lock (_gate)
        {
            if (!_guildMembership.TryGetValue(actorId, out var organizationId) || !_organizations.TryGetValue(organizationId, out var state)) return new(false, "organization-guild-membership-not-found", Array.Empty<string>());
            if (!HasPermission(state, actorId, RuntimeOrganizationPermission.Speak)) return new(false, "organization-guild-speak-denied", Array.Empty<string>());
            return new(true, null, state.Members.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }

    public string? ResolveGuildId(string actorId) { lock (_gate) return _guildMembership.TryGetValue(actorId, out var id) ? id : null; }
    public IReadOnlyList<RuntimeOrganizationSnapshot> CaptureAll() { lock (_gate) return _organizations.Values.OrderBy(x => x.OrganizationId, StringComparer.OrdinalIgnoreCase).Select(Capture).ToArray(); }

    public IReadOnlyList<string> Validate()
    {
        lock (_gate)
        {
            var failures = new List<string>();
            foreach (var state in _organizations.Values)
            {
                if (!state.Members.ContainsKey(state.LeaderActorId)) failures.Add("organization-leader-not-member:" + state.OrganizationId);
                if (!state.Ranks.ContainsKey("leader") || !state.Ranks.ContainsKey("member")) failures.Add("organization-required-rank-missing:" + state.OrganizationId);
                if (state.Members.Count > state.Policy.MaximumMembers) failures.Add("organization-capacity-exceeded:" + state.OrganizationId);
                foreach (var member in state.Members.Values)
                    if (!state.Ranks.ContainsKey(member.RankId)) failures.Add("organization-member-rank-missing:" + member.ActorId);
            }
            foreach (var pair in _guildMembership)
                if (!_organizations.TryGetValue(pair.Value, out var state) || !state.Members.ContainsKey(pair.Key)) failures.Add("organization-membership-index-mismatch:" + pair.Key);
            foreach (var invite in _invitations.Values)
                if (!_organizations.ContainsKey(invite.OrganizationId)) failures.Add("organization-orphan-invitation:" + invite.InvitationId);
            return failures;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(CaptureAll(), new JsonSerializerOptions { WriteIndented = true });
    private RuntimeOrganizationSnapshot Capture(OrganizationState state) => new(state.OrganizationId, state.DisplayName, state.Kind, state.LeaderActorId, state.Ranks.Values.OrderByDescending(x => x.Authority).ThenBy(x => x.RankId, StringComparer.OrdinalIgnoreCase).ToArray(), state.Members.Values.OrderBy(x => x.ActorId, StringComparer.OrdinalIgnoreCase).ToArray(), _invitations.Values.Where(x => x.OrganizationId.Equals(state.OrganizationId, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.ExpiresAtUtc).ToArray(), state.Policy);
    private static bool IsExclusiveGuildKind(RuntimeOrganizationKind kind) => kind is RuntimeOrganizationKind.Guild or RuntimeOrganizationKind.Clan or RuntimeOrganizationKind.Order or RuntimeOrganizationKind.House or RuntimeOrganizationKind.Allegiance;
    private bool HasPermission(OrganizationState state, string actorId, RuntimeOrganizationPermission permission) => state.Members.TryGetValue(actorId, out var member) && state.Ranks.TryGetValue(member.RankId, out var rank) && (rank.Permissions & permission) == permission;
    private void IndexMembership(OrganizationState state, string actorId) { if (IsExclusiveGuildKind(state.Kind)) _guildMembership[actorId] = state.OrganizationId; }
    private void RemoveMembershipIndex(OrganizationState state, string actorId) { if (IsExclusiveGuildKind(state.Kind)) _guildMembership.Remove(actorId); }
    private void RemoveMember(OrganizationState state, string actorId) { state.Members.Remove(actorId); RemoveMembershipIndex(state, actorId); }
    private void RemoveExpired(DateTimeOffset nowUtc) { foreach (var id in _invitations.Values.Where(x => x.ExpiresAtUtc <= nowUtc).Select(x => x.InvitationId).ToArray()) _invitations.Remove(id); }
    private RuntimeOrganizationResult Success(OrganizationState state) => new(true, null, Capture(state));
    private static RuntimeOrganizationResult Fail(string code) => new(false, code, null);
}
