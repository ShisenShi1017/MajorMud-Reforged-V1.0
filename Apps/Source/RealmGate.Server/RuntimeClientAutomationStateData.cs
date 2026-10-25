namespace RealmGate.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string RenderClientAutomationStateData(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            return ReforgedClientProtocol.Encode("AUTOMATIONSTATEERROR", "A persistent character is required.");

        var character = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found.");
        if (!character.WorldId.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Character and selected world identities do not match.");

        var effects = Array.Empty<object>();
        var learnedSpells = Array.Empty<string>();
        var canMeditate = false;
        lock (_livePowerGate)
        {
            EnsureLivePowers(session);
            effects = _liveSpellEngine!.EffectsFor(actor.Id)
                .Where(effect => effect.ExpiresAtUtc is null || effect.ExpiresAtUtc > DateTimeOffset.UtcNow).Select(effect =>
            {
                var spell = _liveSpellCatalog!.Require(effect.SpellId);
                return (object)new
                {
                    effect.EffectId,
                    effect.SpellId,
                    spell.Name,
                    effect.AppliedAtUtc,
                    effect.ExpiresAtUtc
                };
            }).ToArray();
            learnedSpells = _liveSpellEngine.Spellbook(actor.Id).Select(known => known.SpellId)
                .Concat(character.Spells).Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(id =>
                {
                    var spell = _liveSpellCatalog!.Require(id);
                    return new[] { spell.Id, spell.Name, spell.ShortName };
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            canMeditate = _liveQuestAbilityCatalog!.CanUseCapability(character,
                RuntimeCharacterQuestAbilityCatalog.MeditateAbilityId, _liveWorldRewardCatalog!);
        }

        var visionBlocked = RuntimeClassicStatusProjection.IsVisionBlocked(character);
        var npcs = LivingNpcsInRoom(session).Where(_ => !visionBlocked).Select(npc =>
        {
            var definition = session.World.Npcs.Values.FirstOrDefault(candidate =>
                candidate.Id.Equals(npc.NpcId, StringComparison.OrdinalIgnoreCase));
            return new
            {
                npc.InstanceId,
                NpcId = npc.NpcId,
                npc.Name,
                npc.CurrentHitPoints,
                npc.MaximumHitPoints,
                npc.Alive,
                Disposition = ResolveLiveNpcDisposition(npc),
                Level = definition?.Level ?? 0,
                ExperienceReward = definition?.ExperienceReward ?? 0L
            };
        }).ToArray();

        var roomPlayers = _sessions.InRoom(session.World.Definition.Id, session.CurrentRoom.Id)
            .Where(active => !active.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase))
            .Where(_ => !visionBlocked)
            .Where(active => active.Actor is not RuntimeCharacterActor visibleActor ||
                RuntimeClassicCharacterAppearance.CanView(
                    new RuntimeClassicAppearanceViewer(ConditionIsTrue(character, "see_hidden"),
                        ConditionIsTrue(character, "detect_invisible"), false),
                    Appearance(_characters.LoadCharacter(visibleActor.Id), visibleActor)))
            .Select(active => new
            {
                CharacterId = active.Actor.Id,
                Name = active.Actor.DisplayName,
                RoomId = active.CurrentRoom.Id,
                Level = active.Actor is RuntimeCharacterActor activeActor ? activeActor.Level : 0
            })
            .ToArray();

        SynchronizeClassicPartyActiveLocations();
        var party = _partyGameplay.CaptureClassicDisplay(actor.Id);
        var partyEntries = party.Entries
            .Select(entry => BuildClientPartyMember(session, entry.ActorId, entry.Invited, entry.Rank,
                party.LeaderId?.Equals(entry.ActorId, StringComparison.OrdinalIgnoreCase) == true))
            .Where(entry => entry is not null)
            .Cast<object>()
            .ToList();
        if (party.LeaderId is { Length: > 0 } leaderId && party.Entries.All(entry =>
                !entry.ActorId.Equals(leaderId, StringComparison.OrdinalIgnoreCase)))
        {
            var leader = BuildClientPartyMember(session, leaderId, false, "Leader", true);
            if (leader is not null) partyEntries.Add(leader);
        }

        var conditions = ClientVisibleConditions(character);

        var snapshot = new
        {
            WorldId = session.World.Definition.Id,
            CharacterId = character.CharacterId,
            CharacterName = character.DisplayName,
            RoomId = session.CurrentRoom.Id,
            character.Level,
            HealthCurrent = character.HitPoints,
            HealthMaximum = character.MaximumHitPoints,
            ManaCurrent = character.Mana,
            ManaMaximum = character.MaximumMana,
            InCombat = _liveCombats.ContainsKey(session.Id) || _livePvpEncounterBySession.ContainsKey(session.Id),
            InPlayerCombat = _livePvpEncounterBySession.ContainsKey(session.Id),
            IsResting = _focusedRegeneration.ContainsKey(character.CharacterId),
            CanMeditate = canMeditate,
            Conditions = conditions,
            LearnedSpells = learnedSpells,
            ActiveEffects = effects,
            Npcs = npcs,
            Players = roomPlayers,
            PartyMembers = partyEntries,
            CapturedAtUtc = DateTimeOffset.UtcNow
        };
        return ReforgedClientProtocol.Encode("AUTOMATIONSTATE",
            System.Text.Json.JsonSerializer.Serialize(snapshot));
    }

    private object? BuildClientPartyMember(
        RuntimeSession source,
        string characterId,
        bool invited,
        string rank,
        bool isLeader)
    {
        var active = _sessions.ActiveSessions.FirstOrDefault(candidate =>
            candidate.Actor.Id.Equals(characterId, StringComparison.OrdinalIgnoreCase) &&
            candidate.World.Definition.Id.Equals(source.World.Definition.Id, StringComparison.OrdinalIgnoreCase));
        var character = _characters.LoadCharacter(characterId);
        if (character is not null && !character.WorldId.Equals(source.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
            return null;
        if (active is null && character is null) return null;
        var hp = character is { MaximumHitPoints: > 0 }
            ? (int)Math.Clamp((long)character.HitPoints * 100 / character.MaximumHitPoints, 0, 100)
            : 0;
        var mana = character is { MaximumMana: > 0 }
            ? (int)Math.Clamp((long)character.Mana * 100 / character.MaximumMana, 0, 100)
            : 100;
        var conditions = character is null ? Array.Empty<string>() : ClientVisibleConditions(character);
        return new
        {
            CharacterId = characterId,
            Name = active?.Actor.DisplayName ?? character?.DisplayName ?? characterId,
            RoomId = active?.CurrentRoom.Id ?? character?.RoomId ?? string.Empty,
            Online = active is not null,
            InCombat = active is not null && _liveCombats.ContainsKey(active.Id),
            Invited = invited,
            IsLeader = isLeader,
            Rank = rank,
            HealthPercent = hp,
            ManaPercent = mana,
            Conditions = conditions
        };
    }

    private static string[] ClientVisibleConditions(RuntimeCharacterRecord character) =>
        new[] { "poisoned", "blinded", "paralyzed", "immobilized", "muted", "kai_bound", "feared", "dead" }
            .Where(name => RuntimeClassicStatusProjection.Flag(character, name)).ToArray();
}
