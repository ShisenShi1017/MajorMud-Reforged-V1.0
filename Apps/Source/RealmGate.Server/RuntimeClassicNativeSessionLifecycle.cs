using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeClassicHangupPunishmentLevel
{
    High = 1,
    Medium = 2,
    Low = 3,
    None = 4
}

public enum RuntimeClassicDepartureKind
{
    Death,
    GracefulLogoff,
    UnexpectedHangup
}

public sealed record RuntimeClassicLifecycleCleanupResult(
    RuntimeClassicDepartureKind Kind,
    int NpcThreatEntriesReleased,
    bool PlayerAutocombatReleased,
    bool PvpEncounterReleased,
    RuntimeClassicPartyDepartureCleanup PartyCleanup,
    int GangInvitationsCleared,
    int AngelSummonsDismissed,
    int PendingCommandsCleared);

public sealed record RuntimeClassicHangupPenaltyDecision(
    RuntimeClassicHangupPunishmentLevel Level,
    bool ApplyPenalty,
    bool InsideAutocombat,
    bool BeingAttacked,
    bool InPvpCombat);

/// <summary>
/// Direct WCCMMUD.DLL lifecycle ownership recovered in CodeUpdate0339.
/// Addresses are image-base VAs for the retained 1.11p executable and are evidence only.
/// </summary>
public static class RuntimeClassicNativeSessionLifecycleAuthority
{
    public const int CleanupWhenUserLeavesVa = 0x00420DDF;
    public const int WorldgroupGracefulLogoffVa = 0x00408D3C;
    public const int WorldgroupUnexpectedHangupVa = 0x00408D9A;
    public const int StopFollowingVa = 0x0041DE75;
    public const int RemoveFromGroupVa = 0x0041DD96;
    public const int DismissUsersAngelsVa = 0x004203DB;
    public const int CleanupPendingCommandsVa = 0x00450E27;
    public const int KillAutocombatVa = 0x0044C8C2;
    public const int KillAutocombatAgainstVa = 0x0044C914;
    public const int ClearGangInvitationsVa = 0x0044FB55;
    public const int SavePlayerVa = 0x0046DF70;
    public const int ClearPlayerVa = 0x00431423;

    public const RuntimeClassicHangupPunishmentLevel HangupPunishmentDefault = RuntimeClassicHangupPunishmentLevel.None;
    public const int HangupMinimumPercentDefault = 0;
    public const int HangupMaximumPercentDefault = 0;
    public const int HangupMaximumItemsDefault = 0;
    public const int NativeAngelMonsterMarker = 37;

    public static RuntimeClassicHangupPenaltyDecision DecideHangupPenalty(
        RuntimeClassicHangupPunishmentLevel level,
        bool insideAutocombat,
        bool beingAttacked,
        bool inPvpCombat)
    {
        var apply = level switch
        {
            RuntimeClassicHangupPunishmentLevel.High => true,
            RuntimeClassicHangupPunishmentLevel.Medium => insideAutocombat || beingAttacked,
            RuntimeClassicHangupPunishmentLevel.Low => inPvpCombat,
            RuntimeClassicHangupPunishmentLevel.None => false,
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };
        return new(level, apply, insideAutocombat, beingAttacked, inPvpCombat);
    }

    public static (int Minimum, int Maximum) HangupHitPointLossBounds(int maximumHitPoints, int minimumPercent, int maximumPercent)
    {
        if (maximumHitPoints < 0) throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));
        if (minimumPercent is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(minimumPercent));
        if (maximumPercent is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(maximumPercent));
        var first = maximumHitPoints * minimumPercent / 100;
        var second = maximumHitPoints * maximumPercent / 100;
        return first <= second ? (first, second) : (second, first);
    }
}

public sealed partial class RuntimeLiveGameplayFacade
{
    /// <summary>
    /// Native QUIT command pre-exit work. _cmd_quit kills the user's active autocombat and
    /// clears gang invitations before the delayed Worldgroup logoff callback performs the full
    /// shared leave cleanup. Full party/follower and persistence cleanup therefore does not run here.
    /// </summary>
    public void PrepareClassicQuit(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        KillClassicPlayerAutocombat(session, "classic-quit-command");
        _gangGameplay.ClearClassicDepartureInvitations(session.Actor.Id);
    }

    /// <summary>
    /// Reforged projection of native _cleanup_when_user_leaves. Order is deliberately kept in
    /// the same sequence as the DLL: kill autocombat against the user, stop dragging, stop
    /// following/group ownership, clear gang invitations, dismiss native angel summons, clear
    /// pending commands, then kill the user's own autocombat.
    /// </summary>
    public RuntimeClassicLifecycleCleanupResult CleanupClassicUserDeparture(RuntimeSession session, RuntimeClassicDepartureKind kind)
    {
        ArgumentNullException.ThrowIfNull(session);
        var nowUtc = DateTimeOffset.UtcNow;

        // 1. _kill_autocombat_against(user)
        var (npcThreatReleased, pvpReleasedAgainst) = KillClassicAutocombatAgainst(session, nowUtc);

        // 2. _stop_dragging_user(user)
        // Reforged has no active Classic dragging owner yet; there is therefore no synthetic
        // drag state to mutate. Future dragging must register with this lifecycle owner.

        // 3. _stop_following(user,-1) + final _remove_from_group(user)
        var partyCleanup = HandleClassicSessionPartyCleanup(session);

        // 4. _clear_gang_invitations(user,0)
        var gangInvitationsCleared = _gangGameplay.ClearClassicDepartureInvitations(session.Actor.Id);

        // 5. _dismiss_users_angels(user)
        var angelsDismissed = Living(session).DismissClassicNativeAngelSummons(session.Actor.Id, session.Actor.DisplayName, nowUtc);

        // 6. _cleanup_pending_commands(user). Worldgroup modal state is also per-session and
        // disappears with the native user/session owner on departure.
        session.ClearClassicModalState();
        var pendingCommandsCleared = session.ClearClassicPendingCommands();

        // 7. _kill_autocombat(user,1)
        var playerAutocombatReleased = KillClassicPlayerAutocombat(session, $"classic-{kind.ToString().ToLowerInvariant()}-cleanup");

        return new(kind, npcThreatReleased, playerAutocombatReleased, pvpReleasedAgainst,
            partyCleanup, gangInvitationsCleared, angelsDismissed, pendingCommandsCleared);
    }

    /// <summary>
    /// Exact Worldgroup graceful logoff callback ownership: clear forget-list state (none is
    /// materialized in Reforged), shared leave cleanup, save player, then clear the live player.
    /// Session removal is told not to perform its legacy convenience save so the native single-save
    /// ordering is preserved.
    /// </summary>
    public RuntimeClassicLifecycleCleanupResult CompleteClassicGracefulLogoff(RuntimeSession session)
    {
        var cleanup = CleanupClassicUserDeparture(session, RuntimeClassicDepartureKind.GracefulLogoff);
        _characters.SaveSession(session);
        _sessions.Remove(session.Id, saveCharacter: false);
        return cleanup;
    }

    /// <summary>
    /// Exact Worldgroup unexpected-hangup persistence skeleton: preliminary save, native hangup
    /// consequence decision, shared leave cleanup, final save, then clear player. The retained
    /// stock configuration is NONE / 0 / 0 / 0, so the default live path has no HP/item penalty.
    /// Non-default consequence arithmetic is retained by the authority but is not silently enabled
    /// without an explicit Reforged configuration owner.
    /// </summary>
    public RuntimeClassicLifecycleCleanupResult CompleteClassicUnexpectedHangup(RuntimeSession session)
    {
        _characters.SaveSession(session); // native preliminary _save_player
        var insideAutocombat = _liveCombats.ContainsKey(session.Id);
        var beingAttacked = HasClassicNpcThreatAgainst(session);
        var inPvp = _livePvpEncounterBySession.ContainsKey(session.Id);
        var decision = RuntimeClassicNativeSessionLifecycleAuthority.DecideHangupPenalty(
            RuntimeClassicNativeSessionLifecycleAuthority.HangupPunishmentDefault,
            insideAutocombat, beingAttacked, inPvp);
        if (decision.ApplyPenalty)
            throw new InvalidOperationException("native-hangup-nondefault-penalty-configuration-not-connected");

        var cleanup = CleanupClassicUserDeparture(session, RuntimeClassicDepartureKind.UnexpectedHangup);
        _characters.SaveSession(session); // native post-cleanup _save_player
        _sessions.Remove(session.Id, saveCharacter: false);
        return cleanup;
    }

    public RuntimeClassicLifecycleCleanupResult CleanupClassicCharacterDeath(RuntimeSession session) =>
        CleanupClassicUserDeparture(session, RuntimeClassicDepartureKind.Death);

    private (int NpcThreatReleased, bool PvpReleased) KillClassicAutocombatAgainst(RuntimeSession session, DateTimeOffset nowUtc)
    {
        var released = 0;
        var living = Living(session);
        foreach (var npc in living.Capture().Npcs)
        {
            var targetsActor = npc.Metadata.TryGetValue("current_target_id", out var target)
                && target.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase);
            var ledgerContainsActor = false;
            if (npc.Metadata.TryGetValue("threat_ledger_json", out var raw) && !string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    ledgerContainsActor = JsonSerializer.Deserialize<Dictionary<string, long>>(raw)
                        ?.ContainsKey(session.Actor.Id) == true;
                }
                catch (JsonException) { }
            }
            if ((targetsActor || ledgerContainsActor) && living.ReleaseNpcCombatant(npc.InstanceId, session.Actor.Id, nowUtc))
                released++;
        }

        var pvpReleased = false;
        if (_livePvpEncounterBySession.TryGetValue(session.Id, out var pvpId)
            && _livePvpEncounters.TryGetValue(pvpId, out var pvp))
        {
            var opponentId = pvp.AttackerSessionId.Equals(session.Id, StringComparison.OrdinalIgnoreCase)
                ? pvp.DefenderSessionId : pvp.AttackerSessionId;
            EndLivePvpEncounter(pvp);
            _sessions.ActiveSessions.FirstOrDefault(active => active.Id.Equals(opponentId, StringComparison.OrdinalIgnoreCase))
                ?.EnqueueMessage("*Combat Off*");
            pvpReleased = true;
        }
        return (released, pvpReleased);
    }

    private bool KillClassicPlayerAutocombat(RuntimeSession session, string reason)
    {
        var released = false;
        if (_liveCombats.TryRemove(session.Id, out var encounter))
        {
            ReleaseCombatEngagement(session, encounter, reason);
            released = true;
        }
        if (_livePvpEncounterBySession.TryGetValue(session.Id, out var pvpId)
            && _livePvpEncounters.TryGetValue(pvpId, out var pvp))
        {
            EndLivePvpEncounter(pvp);
            released = true;
        }
        _combatOwners.TryRemove(session.Id, out _);
        return released;
    }

    private bool HasClassicNpcThreatAgainst(RuntimeSession session)
    {
        foreach (var npc in Living(session).Capture().Npcs)
        {
            if (npc.Metadata.TryGetValue("current_target_id", out var target)
                && target.Equals(session.Actor.Id, StringComparison.OrdinalIgnoreCase)) return true;
            if (!npc.Metadata.TryGetValue("threat_ledger_json", out var raw) || string.IsNullOrWhiteSpace(raw)) continue;
            try
            {
                if (JsonSerializer.Deserialize<Dictionary<string, long>>(raw)?.ContainsKey(session.Actor.Id) == true) return true;
            }
            catch (JsonException) { }
        }
        return false;
    }
}

public sealed partial class RuntimeLivingWorldService
{
    /// <summary>
    /// Exact native angel-cleanup projection. The DLL only deletes the special summon marker 37
    /// whose stored owner name equals the leaving player; it does not prove that every generic pet,
    /// hireling, escort, or charm is deleted on exit. Reforged therefore requires an explicit
    /// classic-native angel marker instead of conflating all companion ownership with this call.
    /// </summary>
    public int DismissClassicNativeAngelSummons(string ownerId, string ownerDisplayName, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var ids = _npcs.Values.Where(npc =>
            {
                var marker = npc.Metadata.TryGetValue("classic_native_summon_marker", out var rawMarker)
                    && int.TryParse(rawMarker, out var parsedMarker)
                    && parsedMarker == RuntimeClassicNativeSessionLifecycleAuthority.NativeAngelMonsterMarker;
                var ownerName = npc.Metadata.TryGetValue("classic_native_summon_owner_name", out var rawOwnerName)
                    && rawOwnerName.Equals(ownerDisplayName, StringComparison.OrdinalIgnoreCase);
                var explicitCompanionAngel = npc.Metadata.TryGetValue("npc_companion_relationship_kind", out var relationship)
                    && relationship.Equals("angel", StringComparison.OrdinalIgnoreCase)
                    && npc.Metadata.TryGetValue("npc_companion_owner_id", out var storedOwner)
                    && storedOwner.Equals(ownerId, StringComparison.OrdinalIgnoreCase);
                return npc.Alive && ((marker && ownerName) || explicitCompanionAngel);
            }).Select(npc => npc.InstanceId).ToArray();

            foreach (var id in ids)
            {
                _npcs.Remove(id);
                _npcAi.Remove(id);
            }
            if (ids.Length > 0)
            {
                Save();
                SaveLivingWorldCompletionLocked();
            }
            return ids.Length;
        }
    }
}
