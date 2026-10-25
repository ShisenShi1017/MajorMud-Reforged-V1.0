namespace RealmGate.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string RenderClientPanelData(RuntimeSession session, RuntimeCommand command)
    {
        var panel = command.Arguments.FirstOrDefault()?.Trim().ToLowerInvariant() ?? string.Empty;
        if (panel == "navigation")
            return RenderClientNavigationGraphData(session);
        if (panel == "character")
            return RenderClientCharacterStateData(session);
        if (panel == "itemsearch")
            return RenderClientItemSearchData(session, command);
        if (panel is "automation" or "assistant")
            return RenderClientAutomationStateData(session);
        if (panel is "inventory" or "equipment")
        {
            var visible = panel == "inventory" ? RenderInventory(session) : RenderEquipment(session);
            return RenderClientInventoryStateData(session) + Environment.NewLine +
                   ReforgedClientProtocol.Encode("PANELDATA", visible);
        }
        if (panel == "pvp")
        {
            var worldConfiguration = new RuntimePvpWorldConfigurationStore(_characters.ProjectRoot, session.World.Definition.Id).Capture();
            var safeRoom = RuntimePvpZoneCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id).IsProvenSafe(session.CurrentRoom.Id);
            object state;
            if (_livePvpEncounterBySession.TryGetValue(session.Id, out var encounterId) && _livePvpEncounters.TryGetValue(encounterId, out var encounter))
            {
                var opponentSessionId = encounter.AttackerSessionId.Equals(session.Id, StringComparison.OrdinalIgnoreCase)
                    ? encounter.DefenderSessionId : encounter.AttackerSessionId;
                var opponent = _sessions.ActiveSessions.FirstOrDefault(candidate => candidate.Id.Equals(opponentSessionId, StringComparison.OrdinalIgnoreCase));
                state = new { Active = true, WorldPvpEnabled = worldConfiguration.Enabled, worldConfiguration.MaximumLevelDifference, SafeRoom = safeRoom, encounter.EncounterId, OpponentCharacterId = opponent?.Actor.Id ?? string.Empty,
                    OpponentName = opponent?.Actor.DisplayName ?? string.Empty, encounter.RoomId, encounter.RoundNumber, encounter.StartedAtUtc };
            }
            else state = new { Active = false, WorldPvpEnabled = worldConfiguration.Enabled, worldConfiguration.MaximumLevelDifference, SafeRoom = safeRoom, EncounterId = string.Empty, OpponentCharacterId = string.Empty,
                OpponentName = string.Empty, RoomId = session.CurrentRoom.Id, RoundNumber = 0, StartedAtUtc = (DateTimeOffset?)null };
            return ReforgedClientProtocol.Encode("PVPSTATE", System.Text.Json.JsonSerializer.Serialize(state));
        }
        var payload = panel switch
        {
            "party" or "par" => RenderClassicParty(session),
            "who" => RenderWho(),
            "stats" or "score" => RenderScore(session),
            "spells" => ExecutePolicyCommand(session, RuntimeCommand.Parse("spells", session.World.CommandCatalog)),
            _ => "Unknown private client panel request."
        };
        var encoded = ReforgedClientProtocol.Encode("PANELDATA", payload);
        if (panel is "stats" or "score" && session.Actor is RuntimeCharacterActor actor)
        {
            lock (_livePowerGate)
            {
                EnsureLivePowers(session);
                var effects = _liveSpellEngine!.EffectsFor(actor.Id).Select(effect =>
                {
                    var spell = _liveSpellCatalog!.Require(effect.SpellId);
                    return new { effect.EffectId, effect.SpellId, spell.Name, effect.AppliedAtUtc, effect.ExpiresAtUtc };
                }).ToArray();
                encoded += Environment.NewLine + ReforgedClientProtocol.Encode("ACTIVEEFFECTS",
                    System.Text.Json.JsonSerializer.Serialize(effects));
            }
        }
        return encoded;
    }
}
