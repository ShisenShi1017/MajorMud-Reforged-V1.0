namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public string RenderRoom(RuntimeSession session) => RenderRoomInternal(session, forceDescription: false);

    public string RenderRoomVerbose(RuntimeSession session) => RenderRoomInternal(session, forceDescription: true);

    public string SetRoomDescriptionMode(RuntimeSession session, bool brief)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            ["ui.room_description_mode"] = brief ? "brief" : "verbose"
        };
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
        return brief
            ? "Brief mode enabled. Room descriptions will be omitted during normal movement; LOOK still shows the full description."
            : "Verbose mode enabled. Full room descriptions will be shown during normal movement.";
    }

    private bool UsesBriefRoomDescriptions(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return false;
        var character = _characters.LoadCharacter(actor.Id);
        return character?.Conditions.TryGetValue("ui.room_description_mode", out var mode) == true &&
               mode.Equals("brief", StringComparison.OrdinalIgnoreCase);
    }

    private string RenderRoomInternal(RuntimeSession session, bool forceDescription)
    {
        const string reset = "\u001b[0m";
        const string brightCyan = "\u001b[1;36m";
        const string yellow = "\u001b[1;33m";
        const string brightGreen = "\u001b[1;32m";
        const string magenta = "\u001b[1;35m";
        const string gray = "\u001b[1;30m";
        const string white = "\u001b[0;37m";

        RuntimeBundleDSearchRuntime.EnsureLoaded(_characters.ProjectRoot, session.World.Definition.Id);
        EnsureClassicArenaPopulation(session);

        var living = LivingNpcsInRoom(session).ToArray();
        if (session.Actor is RuntimeCharacterActor visionActor
            && _characters.LoadCharacter(visionActor.Id) is { } visionCharacter
            && RuntimeClassicStatusProjection.IsVisionBlocked(visionCharacter))
        {
            if (!_liveCombats.ContainsKey(session.Id))
            {
                var aggressive = living.FirstOrDefault(npc => ResolveLiveNpcDisposition(npc).Equals("hostile", StringComparison.OrdinalIgnoreCase)
                    && !IsNpcAttackSuppressedByItem(npc, visionCharacter));
                if (aggressive is not null)
                {
                    try { _ = BeginLiveCombat(session, aggressive.InstanceId, ClassicAttackType.NormalWeapon); } catch { }
                }
            }
            return RuntimeClassicCombatAnsi.Warning("You are blind.");
        }

        var lines = new List<string>
        {
            $"{brightCyan}{session.CurrentRoom.Name}{reset}"
        };
        if (forceDescription || !UsesBriefRoomDescriptions(session))
            lines.Add($"{white}{IndentClassicDescription(session.CurrentRoom.Description)}{reset}");

        var hostile = living.Where(npc => ResolveLiveNpcDisposition(npc).Equals("hostile", StringComparison.OrdinalIgnoreCase))
            .Select(npc => npc.Name).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        var friendly = living.Where(npc => ResolveLiveNpcDisposition(npc).Equals("friendly", StringComparison.OrdinalIgnoreCase))
            .Select(npc => npc.Name).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        var neutral = living.Where(npc => ResolveLiveNpcDisposition(npc).Equals("neutral", StringComparison.OrdinalIgnoreCase))
            .Select(npc => npc.Name).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (hostile.Length > 0) lines.Add($"{magenta}Also here: {JoinClassicNames(hostile)}.{reset}");
        if (friendly.Length > 0) lines.Add($"{yellow}Also here: {JoinClassicNames(friendly)}.{reset}");
        if (neutral.Length > 0) lines.Add($"{gray}Also here: {JoinClassicNames(neutral)}.{reset}");

        var players = _sessions.InRoom(session.World.Definition.Id, session.CurrentRoom.Id)
            .Where(other => !other.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase))
            .Select(other => other.Actor.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (players.Length > 0) lines.Add($"{magenta}Players: {string.Join(", ", players)}{reset}");

        var roomCurrency = RuntimeClassicRoomCurrencyStore.Get(_characters.ProjectRoot, session.World.Definition.Id, session.CurrentRoom.Id);
        if (!roomCurrency.IsEmpty) lines.Add($"{yellow}You notice {RuntimeClassicRoomCurrencyStore.Format(roomCurrency)} here.{reset}");

        var ground = LoadVisibleGround(session).ToArray();
        if (ground.Length > 0) lines.Add($"{brightGreen}Items: {string.Join(", ", ground.Select(entry => entry.Name))}{reset}");


        var exits = session.CurrentRoom.Exits.Count == 0 ? "none" : string.Join(", ",
            session.CurrentRoom.Exits
                .Where(exit => CanCharacterSeeExit(session, exit))
                .Select(exit => exit.Direction)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(direction => DescribeClassicExit(session, direction)));
        lines.Add($"{brightGreen}Obvious exits: {exits}{reset}");
        if (!_liveCombats.ContainsKey(session.Id))
        {
            var targetCharacter = session.Actor is RuntimeCharacterActor targetActor ? _characters.LoadCharacter(targetActor.Id) : null;
            var aggressive = living.FirstOrDefault(npc => ResolveLiveNpcDisposition(npc).Equals("hostile", StringComparison.OrdinalIgnoreCase)
                && !IsNpcAttackSuppressedByItem(npc, targetCharacter));
            if (aggressive is not null)
            {
                try { lines.Add(BeginLiveCombat(session, aggressive.InstanceId, ClassicAttackType.NormalWeapon).Replace("You attack", aggressive.Name + " attacks")); } catch { }
            }
        }
        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }


    public string LookDirection(RuntimeSession session, string query)
    {
        if (session.Actor is RuntimeCharacterActor visionActor
            && _characters.LoadCharacter(visionActor.Id) is { } visionCharacter
            && RuntimeClassicStatusProjection.IsVisionBlocked(visionCharacter))
            return RuntimeClassicCombatAnsi.Warning("You are blind.");
        var raw = query.Trim();
        var normalized = raw.ToUpperInvariant() switch
        {
            "NORTH" => "N", "SOUTH" => "S", "EAST" => "E", "WEST" => "W",
            "NORTHEAST" => "NE", "NORTHWEST" => "NW", "SOUTHEAST" => "SE", "SOUTHWEST" => "SW",
            "UP" => "U", "DOWN" => "D", "N" => "N", "S" => "S", "E" => "E", "W" => "W",
            "NE" => "NE", "NW" => "NW", "SE" => "SE", "SW" => "SW", "U" => "U", "D" => "D",
            _ => string.Empty
        };
        if (normalized.Length > 0 && VisiblePlayerMatches(session, raw, includeSelf: false).Length > 0) return Examine(session, raw);
        if (normalized.Length == 0) return Examine(session, raw);
        var exit = session.World.FindExit(session.CurrentRoom, normalized);
        if (exit is null || !CanCharacterSeeExit(session, exit)) return "You see nothing in that direction.";
        var target = session.World.ResolveExitDestination(exit);
        if (target is null) return "You cannot see where that exit leads.";
        const string reset = "\u001b[0m", brightCyan = "\u001b[1;36m", white = "\u001b[0;37m", brightGreen = "\u001b[1;32m";
        var exits = target.Exits.Count == 0 ? "none" : string.Join(", ", target.Exits.Select(candidate => candidate.Direction).Where(value => !string.IsNullOrWhiteSpace(value)).Select(ExpandDirection));
        return string.Join(Environment.NewLine, new[] { $"{brightCyan}{target.Name}{reset}", $"{white}{IndentClassicDescription(target.Description)}{reset}", $"{brightGreen}Obvious exits: {exits}{reset}" });
    }


    private static bool CanCharacterSeeExit(RuntimeSession session, RuntimeExit exit)
    {
        if (!exit.GateKind.Equals("hidden", StringComparison.OrdinalIgnoreCase)) return true;
        return RuntimeBundleDSearchRuntime.Registry.IsDiscovered(session.Actor.Id, exit.Id);
    }

    private void EnsureClassicArenaPopulation(RuntimeSession session)
    {
        if (!session.World.Definition.Id.Equals("classic", StringComparison.OrdinalIgnoreCase)) return;
        if (session.CurrentRoom.Id is not ("room.map1.2150" or "room.map1.2155")) return;
        if (LivingNpcsInRoom(session).Any()) return;

        // Original Classic observation: Newhaven Arena rotates low-level combatants
        // into the room, including giant rats and kobold thieves.
        var selector = Math.Abs(DateTimeOffset.UtcNow.Minute + DateTimeOffset.UtcNow.Second) % 2;
        var npcId = selector == 0 ? "1" : "7";
        Living(session).SummonNpc(npcId, session.CurrentRoom.Id, "classic-newhaven-arena-rotation");
    }

    private static string IndentClassicDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        // Imported Classic descriptions can contain fixed-width source line breaks and
        // indentation. Collapse them to a single paragraph and let the Telnet client
        // wrap at its negotiated display width, matching original MajorMUD output.
        var normalized = string.Join(" ", description
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return "    " + normalized;
    }

    private static string JoinClassicNames(IReadOnlyList<string> names) =>
        string.Join(", ", names);

    private string DescribeClassicExit(RuntimeSession session, string direction)
    {
        var description = DescribeLivingExit(session, direction);
        var expanded = ExpandDirection(direction);
        if (description.Contains("closed", StringComparison.OrdinalIgnoreCase)) return $"closed door {expanded}";
        if (description.Contains("locked", StringComparison.OrdinalIgnoreCase)) return $"locked door {expanded}";
        return expanded;
    }

    private static string ExpandDirection(string direction) => direction.Trim().ToUpperInvariant() switch
    {
        "N" => "north", "S" => "south", "E" => "east", "W" => "west",
        "NE" => "northeast", "NW" => "northwest", "SE" => "southeast", "SW" => "southwest",
        "U" => "up", "D" => "down", _ => direction.Trim().ToLowerInvariant()
    };

    public string RenderClassicPrompt(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return ":";
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return ":";
        var resting = RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Resting) ? " (Resting) " : string.Empty;
        return RuntimeClassicCombatAnsi.Prompt(character.HitPoints, character.MaximumHitPoints, resting);
    }

    private static string ResolveLiveNpcDisposition(RuntimeLivingNpcState npc)
    {
        if (npc.Metadata.TryGetValue("disposition", out var disposition) && !string.IsNullOrWhiteSpace(disposition))
        {
            return disposition;
        }

        if (npc.Metadata.TryGetValue("alignment", out var alignment) && alignment.Contains("evil", StringComparison.OrdinalIgnoreCase))
        {
            return "hostile";
        }

        if (npc.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) ||
            npc.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase) ||
            npc.InteractionModel.Contains("trainer", StringComparison.OrdinalIgnoreCase))
        {
            return "friendly";
        }

        return "neutral";
    }

    internal static bool IsNpcAttackSuppressedByItem(RuntimeLivingNpcState npc, RuntimeCharacterRecord? character)
    {
        if (character?.ItemState?.Items is null
            || !npc.Metadata.TryGetValue("non_attack_item_legacy_id", out var raw)
            || !int.TryParse(raw, out var legacyItemId) || legacyItemId <= 0) return false;
        var canonicalItemId = $"item.{legacyItemId}";
        return character.ItemState.Items.Any(item => item.OwnerId is not null
            && item.OwnerId.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase)
            && item.CanonicalItemId.Equals(canonicalItemId, StringComparison.OrdinalIgnoreCase));
    }

    public string RenderStatLine(RuntimeSession session)
    {
        const string status = "\u001b[1;37;44m";
        const string reset = "\u001b[0m";
        if (session.Actor is RuntimeCharacterActor actor)
        {
            var character = _characters.LoadCharacter(actor.Id);
            if (character is not null)
            {
                var items = LoadItemState(session);
                var gold = items?.Gold ?? 0;
                var state = RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Resting) ? "Resting" : "Ready";
                return $"{status} HP:{character.HitPoints}/{character.MaximumHitPoints}  MA:{character.Mana}/{character.MaximumMana}  EXP:{character.Experience}  GP:{gold}  {state} {reset}";
            }
        }
        return $"{status} Ready {reset}";
    }

    public string Examine(RuntimeSession session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Examine what?";
        if (session.Actor is RuntimeCharacterActor visionActor
            && _characters.LoadCharacter(visionActor.Id) is { } visionCharacter
            && RuntimeClassicStatusProjection.IsVisionBlocked(visionCharacter))
            return RuntimeClassicCombatAnsi.Warning("You are blind.");
        var value = query.Trim();
        var observerRecord = session.Actor is RuntimeCharacterActor observerActor ? _characters.LoadCharacter(observerActor.Id) : null;
        var playerMatches = VisiblePlayerMatches(session, value);
        var npcMatches = LivingNpcsInRoom(session)
            .Where(npc => npc.InstanceId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || npc.NpcId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || npc.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
        var itemMatches = LoadVisibleGround(session)
            .Where(item => item.InstanceId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || item.CanonicalId.Equals(value, StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (playerMatches.Length > 1) return $"More than one visible character matches '{value}'; use a longer name abbreviation.";
        if (playerMatches.Length == 1)
        {
            var actor = playerMatches[0].Actor;
            if (actor is RuntimeCharacterActor character)
            {
                var record = _characters.LoadCharacter(character.Id);
                if (record is null) return $"{actor.DisplayName} [{actor.Kind}] id={actor.Id}";
                var pronoun = string.Equals(character.Sex, "Female", StringComparison.OrdinalIgnoreCase) ? "She" : "He";
                var appearance = RuntimeClassicCharacterAppearance.Compose(
                    new RuntimeClassicAppearanceViewer(ConditionIsTrue(observerRecord,"see_hidden"), ConditionIsTrue(observerRecord,"detect_invisible"), character.Id.Equals(session.Actor.Id,StringComparison.OrdinalIgnoreCase)),
                    Appearance(record,character));
                var lines = new List<string> { $"[ {character.DisplayName} ]", string.Empty };
                var native = RuntimeClassicNativeTextblockPayloadAuthority.Load(_characters.ProjectRoot);
                if (RuntimeClassicCharacterAppearance.TryResolveShadowformOverride(record, native, out var shadowform))
                    lines.Add(shadowform);
                else
                    lines.Add(appearance);
                lines.Add(string.Empty);
                lines.Add($"{pronoun} is equipped with:");
                lines.Add(string.Empty);
                if (_majorMudPlus.TryGetApprovedDescription(session.World.Definition.Id, character.Id, out var customDescription))
                {
                    var equipmentHeadingIndex = lines.FindIndex(line => line.Equals($"{pronoun} is equipped with:", StringComparison.Ordinal));
                    lines.InsertRange(equipmentHeadingIndex < 0 ? lines.Count : equipmentHeadingIndex, customDescription.Concat(new[] { string.Empty }));
                }
                var activeDescriptions = ActiveSpellLookMessages(session, character.Id, character.DisplayName);
                if (activeDescriptions.Count > 0)
                {
                    lines.AddRange(activeDescriptions);
                    lines.Add(string.Empty);
                }
                if (record?.ItemState is not null)
                {
                    var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
                    var equipped = record.ItemState.Items.Where(i => i.OwnerId != null && i.OwnerId.Equals(character.Id, StringComparison.OrdinalIgnoreCase) && i.EquippedSlot is not null).OrderBy(i => catalog.Require(i.CanonicalItemId).Name).ToArray();
                    if (equipped.Length == 0) lines.Add("Nothing.");
                    else foreach (var equippedItem in equipped) lines.Add($"{catalog.Require(equippedItem.CanonicalItemId).Name,-30}({ClassicDisplaySlot(catalog, equippedItem)})");
                }
                else lines.Add("Nothing.");
                return string.Join(Environment.NewLine, lines);
            }
            return $"{actor.DisplayName} [{actor.Kind}] id={actor.Id}";
        }
        var total = npcMatches.Length + itemMatches.Length;
        if (total == 0) return $"You do not see '{value}' here.";
        if (total > 1) return $"More than one visible target matches '{value}'; use a stable ID.";
        if (npcMatches.Length == 1)
        {
            var spawn = npcMatches[0];
            var descriptions = RuntimeClassicDescriptionAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id);
            var baseDescription = descriptions.TryNpcDescription(spawn.NpcId, out var npcDescription)
                ? npcDescription
                : $"{spawn.Name} [instance={spawn.InstanceId}; npc={spawn.NpcId}; spawn={spawn.SpawnId}; hp={spawn.CurrentHitPoints}/{spawn.MaximumHitPoints}]";
            var activeDescriptions = ActiveSpellLookMessages(session, spawn.InstanceId, spawn.Name);
            return activeDescriptions.Count == 0
                ? baseDescription
                : baseDescription + Environment.NewLine + string.Join(Environment.NewLine, activeDescriptions);
        }
        var item = itemMatches[0];
        var itemDescriptions = RuntimeClassicDescriptionAuthority.Load(_characters.ProjectRoot, session.World.Definition.Id);
        return itemDescriptions.TryItemDescription(item.CanonicalId, out var itemDescription) ? itemDescription : $"{item.Name} [item={item.CanonicalId}; instance={item.InstanceId}]";
    }

    private RuntimeSession[] VisiblePlayerMatches(RuntimeSession session, string value, bool includeSelf = true)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var observerRecord = session.Actor is RuntimeCharacterActor observerActor ? _characters.LoadCharacter(observerActor.Id) : null;
        return _sessions.InRoom(session.World.Definition.Id, session.CurrentRoom.Id)
            .Append(session)
            .DistinctBy(other => other.Id, StringComparer.OrdinalIgnoreCase)
            .Where(other => includeSelf || !other.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase))
            .Where(other => RuntimeClassicCharacterLookTargeting.Matches(other.Actor.Id, other.Actor.DisplayName, value))
            .Where(other => other.Actor is not RuntimeCharacterActor targetActor || RuntimeClassicCharacterAppearance.CanView(
                new RuntimeClassicAppearanceViewer(ConditionIsTrue(observerRecord,"see_hidden"), ConditionIsTrue(observerRecord,"detect_invisible"), other.Id.Equals(session.Id,StringComparison.OrdinalIgnoreCase)),
                Appearance(_characters.LoadCharacter(targetActor.Id), targetActor)))
            .ToArray();
    }

    private static bool ConditionIsTrue(RuntimeCharacterRecord? record, string key) =>
        record is not null && record.Conditions.TryGetValue(key, out var value)
        && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private static RuntimeClassicAppearanceInput Appearance(RuntimeCharacterRecord? record, RuntimeCharacterActor actor)
    {
        int Stat(int fallback, string? statusModifier, params string[] names)
        {
            if (record is null) return fallback;
            var value = fallback;
            foreach (var name in names)
            {
                if (!record.BaseStats.TryGetValue(name, out value)) continue;
                break;
            }
            if (!string.IsNullOrWhiteSpace(statusModifier))
                value += RuntimeClassicStatusProjection.Numeric(record, statusModifier);
            return value;
        }
        string Condition(string name) => record is not null && record.Conditions.TryGetValue(name, out var value) ? value : string.Empty;
        bool Flag(string name) => ConditionIsTrue(record, name);
        var narrativeName = Condition("given_name").Trim();
        if (narrativeName.Length == 0)
            narrativeName = actor.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? actor.DisplayName;
        return new(narrativeName, actor.Sex, actor.Race, actor.CharacterClass,
            Condition("hair_length"), Condition("hair_color"), Condition("eye_color"),
            Stat(50, "stat_strength", "strength", "str"),
            Stat(50, "stat_agility", "agility", "agi", "agl"),
            Stat(50, "stat_intellect", "intellect", "intelligence", "int"),
            Stat(50, "stat_wisdom", "willpower", "wil"),
            Stat(50, null, "health", "hea"),
            Stat(50, "stat_charm", "charm", "cha"),
            record?.HitPoints ?? 1, record?.MaximumHitPoints ?? 1,
            Flag("hidden"), Flag("sneaking"), Flag("invisible"), Flag("disguised"), Flag("transformed") || Flag("polymorphed"));
    }

    public string Say(RuntimeSession session, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Say what?";
        if (session.Actor is RuntimeCharacterActor actor && _characters.LoadCharacter(actor.Id) is { } character
            && RuntimeClassicStatusProjection.IsSpeechBlocked(character))
            return "You are unable to speak.";
        _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} says, '{text.Trim()}'");
        return $"You say, '{text.Trim()}'";
    }

    public string Tell(RuntimeSession session, IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2) return "Use: tell <character> <message>.";
        if (session.Actor is RuntimeCharacterActor actor && _characters.LoadCharacter(actor.Id) is { } character
            && RuntimeClassicStatusProjection.IsSpeechBlocked(character))
            return "You are unable to speak.";
        var target = arguments[0];
        var message = string.Join(" ", arguments.Skip(1));
        _sessions.SendTell(session, target, message, out var response);
        return response;
    }

    public string DrainMessages(RuntimeSession session)
    {
        var messages = session.DrainMessages();
        return messages.Count == 0 ? string.Empty : string.Join(Environment.NewLine, messages);
    }

    public void BroadcastDeparture(RuntimeSession session, string roomId, string direction)
    {
        foreach (var target in _sessions.ActiveSessions.Where(target =>
            target.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)
            && target.CurrentRoom.Id.Equals(roomId, StringComparison.OrdinalIgnoreCase)
            && !target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase)))
        {
            target.EnqueueMessage($"{session.Actor.DisplayName} leaves {direction}.");
        }
    }

    public void BroadcastArrival(RuntimeSession session)
        => _sessions.BroadcastRoom(session, $"{session.Actor.DisplayName} arrives.");

    private IEnumerable<string> LoadVisibleCorpses(RuntimeSession session)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var active in _sessions.ActiveSessions.Where(active => active.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (active.Actor is not RuntimeCharacterActor actor) continue;
            var state = _characters.LoadCharacter(actor.Id)?.ItemState;
            if (state?.Corpses is null) continue;
            foreach (var corpse in state.Corpses.Where(corpse => !corpse.CleanedUp
                && corpse.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)
                && corpse.CharacterId.StartsWith("npc:", StringComparison.OrdinalIgnoreCase)
                && seen.Add(corpse.CorpseId)))
            {
                var npcId = corpse.CharacterId[4..];
                yield return ResolveNpcName(session, npcId);
            }
        }
    }

    private string ResolveNpcName(RuntimeSession session, string npcId)
    {
        var match = Living(session).Capture().Npcs.FirstOrDefault(npc => npc.NpcId.Equals(npcId, StringComparison.OrdinalIgnoreCase));
        return match?.Name ?? npcId;
    }

    private IEnumerable<VisibleGroundItem> LoadVisibleGround(RuntimeSession session)
    {
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in Living(session).GroundItems(session.CurrentRoom.Id).Where(item=>
            RuntimeBundleDSearchRuntime.CanCharacterSeeGroundItem(session.Actor.Id,item)
            &&seen.Add(item.InstanceId)))
        {
            string name;
            try{name=catalog.Require(item.CanonicalItemId).Name;}
            catch{continue;}
            yield return new VisibleGroundItem(item.InstanceId,item.CanonicalItemId,name);
        }
        foreach (var active in _sessions.ActiveSessions.Where(active => active.World.Definition.Id.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (active.Actor is not RuntimeCharacterActor actor) continue;
            var state = _characters.LoadCharacter(actor.Id)?.ItemState;
            if (state is null) continue;
            foreach (var item in state.Items.Where(item =>
                !RuntimeClassicRoomCurrencyStore.IsLegacyCurrency(item)
                && item.LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)
                && item.LocationId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)
                && RuntimeBundleDSearchRuntime.CanCharacterSeeGroundItem(session.Actor.Id, item)
                && seen.Add(item.InstanceId)))
            {
                string name;
                try { name = catalog.Require(item.CanonicalItemId).Name; }
                catch { continue; }
                yield return new VisibleGroundItem(item.InstanceId, item.CanonicalItemId, name);
            }
        }
    }

    private sealed record VisibleGroundItem(string InstanceId, string CanonicalId, string Name);
}

public sealed class RuntimeLiveGameplayPart3SmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();
    public static RuntimeLiveGameplayPart3SmokeReport Run()
    {
        var report = new RuntimeLiveGameplayPart3SmokeReport();
        try
        {
            Require(RuntimeCommand.Parse("say hello world").ArgumentText == "hello world", "say-route"); report.Checks.Add("say-route");
            Require(RuntimeCommand.Parse("tell Alice hello").Arguments.Count == 2, "tell-route"); report.Checks.Add("tell-route");
            Require(RuntimeCommand.SupportedCommands.Contains("examine"), "examine-route"); report.Checks.Add("examine-route");
            Require(RuntimeCommand.SupportedCommands.Contains("say") && RuntimeCommand.SupportedCommands.Contains("tell"), "communication-catalog"); report.Checks.Add("communication-catalog");
            Require(typeof(RuntimeSession).GetMethod(nameof(RuntimeSession.DrainMessages)) is not null, "message-mailbox"); report.Checks.Add("message-mailbox");
            Require(typeof(RuntimeSessionManager).GetMethod(nameof(RuntimeSessionManager.InRoom)) is not null, "room-presence"); report.Checks.Add("room-presence");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
