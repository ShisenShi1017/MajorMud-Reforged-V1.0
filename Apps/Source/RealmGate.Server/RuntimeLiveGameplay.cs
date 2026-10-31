namespace RealmGate.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly RuntimeCharacterService _characters;
    private readonly RuntimeSessionManager _sessions;
    private readonly RuntimeMajorMudPlusService _majorMudPlus;
    private readonly RuntimeWccSysopService _wccSysop;
    private readonly RuntimeWccInvasionService _wccInvasions;
    private readonly RuntimeClassicGangGameplayService _gangGameplay;
    private readonly RuntimePartyCoordinationService _partyGameplay;
    private readonly string? _livingWorldPersistenceRoot;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RuntimeClassicNativeTextblockPayloadAuthority> _nativePayloadsByWorld = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RuntimeSpellCatalog> _npcSpellCatalogByWorld = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RuntimeNpcAttackArgumentRecoveryCatalog> _npcAttackArgumentsByWorld = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RuntimeClassicTextblockGraphAuthority> _textblockGraphsByWorld = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RuntimeClassicAbilityAcquisitionAuthority> _abilityAcquisitionsByWorld = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RuntimeClassicNpcDialogueExecutionAuthority> _npcDialogueByWorld = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeGameplayCompletionStateStore> _gameplayStores = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gameplayStoreGate = new();

    public RuntimeLiveGameplayFacade(RuntimeCharacterService characters, RuntimeSessionManager sessions, string? livingWorldPersistenceRoot = null)
    {
        _characters = characters;
        _sessions = sessions;
        _livingWorldPersistenceRoot = livingWorldPersistenceRoot;
        _majorMudPlus = new RuntimeMajorMudPlusService(characters, sessions);
        _wccSysop = new RuntimeWccSysopService(characters, sessions);
        _wccInvasions = new RuntimeWccInvasionService(characters);
        _gangGameplay = new RuntimeClassicGangGameplayService(characters, sessions);
        _partyGameplay = new RuntimePartyCoordinationService();
        _sessions.SessionsChanged += (_, _) => { SynchronizeClassicPartyActiveLocations(); PruneLivePvpConnections(); };
        // World-scoped gameplay catalogs are loaded lazily from the active session world.
        // A Custom/AI world must never inherit a live Classic catalog instance.
    }

    private RuntimeClassicNativeTextblockPayloadAuthority NativePayloads(string worldId) =>
        _nativePayloadsByWorld.GetOrAdd(
            worldId,
            id => RuntimeClassicNativeTextblockPayloadAuthority.Load(_characters.ProjectRoot, id));

    private RuntimeSpellCatalog NpcSpellCatalog(string worldId) =>
        _npcSpellCatalogByWorld.GetOrAdd(
            worldId,
            id => RuntimeSpellCatalog.Load(_characters.ProjectRoot, id));

    private RuntimeNpcAttackArgumentRecoveryCatalog NpcAttackArguments(string worldId) =>
        _npcAttackArgumentsByWorld.GetOrAdd(
            worldId,
            id => RuntimeNpcAttackArgumentRecoveryCatalog.Load(_characters.ProjectRoot, id));

    private RuntimeClassicTextblockGraphAuthority TextblockGraph(string worldId) =>
        _textblockGraphsByWorld.GetOrAdd(worldId, id => RuntimeClassicTextblockGraphAuthority.Load(_characters.ProjectRoot, id));

    private RuntimeClassicAbilityAcquisitionAuthority AbilityAcquisitions(string worldId) =>
        _abilityAcquisitionsByWorld.GetOrAdd(worldId, id => RuntimeClassicAbilityAcquisitionAuthority.Load(_characters.ProjectRoot, id));

    private RuntimeClassicNpcDialogueExecutionAuthority NpcDialogue(string worldId) =>
        _npcDialogueByWorld.GetOrAdd(worldId, id => RuntimeClassicNpcDialogueExecutionAuthority.Load(_characters.ProjectRoot, id));

    public string ExecuteMajorMudPlusCommand(RuntimeSession session, RuntimeCommand command) =>
        _majorMudPlus.Execute(session, command, Living(session));

    public string ExecuteWccSysopCommand(RuntimeSession session, RuntimeCommand command) =>
        _wccSysop.Execute(session, command, Living(session));

    public string RenderClassicParty(RuntimeSession session)
    {
        SynchronizeClassicPartyActiveLocations();
        var party = _partyGameplay.CaptureClassicDisplay(session.Actor.Id);
        if (!party.HasAnyGroupSlot && party.LeaderId is null)
            return "You are not in a party at the present time.";

        var rows = new List<string> { "The following people are in your travel party:" };
        foreach (var entry in party.Entries)
        {
            var active = _sessions.ActiveSessions.FirstOrDefault(candidate =>
                candidate.Actor.Id.Equals(entry.ActorId, StringComparison.OrdinalIgnoreCase));
            var character = _characters.LoadCharacter(entry.ActorId);
            var name = active?.Actor.DisplayName ?? character?.DisplayName ?? entry.ActorId;
            if (entry.Invited)
            {
                rows.Add($"   {name} [Invited]");
                continue;
            }

            var className = (active?.Actor as RuntimeCharacterActor)?.CharacterClass ?? character?.ClassName ?? "Unknown";
            var hp = character is { MaximumHitPoints: > 0 }
                ? Math.Clamp(character.HitPoints * 100 / character.MaximumHitPoints, 0, 100)
                : 0;
            var resource = character is { MaximumMana: > 0 }
                ? Math.Clamp(character.Mana * 100 / character.MaximumMana, 0, 100)
                : (int?)null;
            var resourceKind = character is not null
                && RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId).RequireClass(character.ClassId).Magic?.Type == 5
                ? "K"
                : "M";
            var resourceText = resource is { } percent ? $"[{resourceKind}:{percent,3}%] " : string.Empty;
            rows.Add($"   {name} ({className}) {resourceText}[H:{hp,3}%] - {entry.Rank}");
        }

        if (party.LeaderId is { } leaderId && party.Entries.All(entry =>
                !entry.ActorId.Equals(leaderId, StringComparison.OrdinalIgnoreCase)))
        {
            var leader = _sessions.ActiveSessions.FirstOrDefault(candidate =>
                candidate.Actor.Id.Equals(leaderId, StringComparison.OrdinalIgnoreCase));
            rows.Add($"   Following: {leader?.Actor.DisplayName ?? leaderId}");
        }
        return string.Join(Environment.NewLine, rows);
    }


    public IReadOnlyList<string> AdmitClassicRoomEntryNpcRespawns(RuntimeSession session) =>
        Living(session).AdmitDueNpcRespawnsForRoom(session.CurrentRoom.Id, DateTimeOffset.UtcNow)
            .Transitions.Select(transition => transition.EventCode).ToArray();

    public IReadOnlyList<RuntimeSession> ResolveClassicPartyFollowers(RuntimeSession leader, string originRoomId)
    {
        SynchronizeClassicPartyActiveLocations();
        return _partyGameplay.ResolveClassicFollowerMoves(leader.Actor.Id, originRoomId, leader.CurrentRoom.Id)
            .Select(move => _sessions.ActiveSessions.FirstOrDefault(candidate =>
                candidate.Actor.Id.Equals(move.ActorId, StringComparison.OrdinalIgnoreCase)))
            .Where(candidate => candidate is not null)
            .Cast<RuntimeSession>()
            .ToArray();
    }

    private IReadOnlyList<RuntimeSpellTarget> ResolveClassicPartySpellTargets(RuntimeSession caster)
    {
        SynchronizeClassicPartyActiveLocations();
        var ids = _partyGameplay.ResolveClassicType13ActorIds(caster.Actor.Id);
        return ids
            .Select(id => _sessions.ActiveSessions.FirstOrDefault(active =>
                active.Actor.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .Where(active => active is not null
                && active.World.Definition.Id.Equals(caster.World.Definition.Id, StringComparison.OrdinalIgnoreCase)
                && active.CurrentRoom.Id.Equals(caster.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase))
            .Select(active => new RuntimeSpellTarget("character", active!.Actor.Id))
            .DistinctBy(target => target.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(target => target.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal bool IsClassicPartyHostileRoomSpellTarget(RuntimeSession caster, RuntimeSpellTarget target)
    {
        if (!target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)) return true;
        SynchronizeClassicPartyActiveLocations();
        // Native _is_valid_target target-type 12 rejects self, direct leader/follower links,
        // and every user id in the caster's five +0x6ae group slots.
        return !_partyGameplay.IsClassicHostilePartyTargetExcluded(caster.Actor.Id, target.Id);
    }

    public void StopClassicPartyFollowAfterFailedMove(RuntimeSession follower) =>
        _partyGameplay.StopFollowingAfterFailedClassicMove(follower.Actor.Id);

    private void SynchronizeClassicPartyActiveLocations()
    {
        // The native graph is live-session state. Register every active character so pending
        // one-way invitation slots, leader pointers, rank bytes and room coordinates all share
        // the same user index. Departure itself remains owned by the 0339 cleanup path.
        foreach (var active in _sessions.ActiveSessions.Where(current => current.Actor is RuntimeCharacterActor))
            _partyGameplay.SynchronizeClassicActor(active.Actor.Id, connected: true, active.CurrentRoom.Id);
    }

    public string ExecuteGangCommand(RuntimeSession session, RuntimeCommand command) =>
        command.Name.Equals("disband", StringComparison.OrdinalIgnoreCase)
        && command.Arguments.Count == 1
        && command.Arguments[0].Equals("party", StringComparison.OrdinalIgnoreCase)
            ? DisbandClassicParty(session)
            : _gangGameplay.Execute(session, command);

    public bool TryHandleClassicModalInput(RuntimeSession session, string input, out string result) =>
        _gangGameplay.TryHandleClassicModalInput(session, input, out result);

    public bool TryExecuteMajorMudPlusAction(RuntimeSession session, RuntimeCommand command, out string result) =>
        _majorMudPlus.TryExecuteAction(session, command, out result, Living(session));

    private RuntimeGameplayCompletionStateStore GameplayStore(string worldId)
    {
        lock (_gameplayStoreGate)
        {
            if (!_gameplayStores.TryGetValue(worldId, out var store))
            {
                store = new RuntimeGameplayCompletionStateStore(_characters.ProjectRoot, worldId);
                _gameplayStores[worldId] = store;
            }
            return store;
        }
    }

    public string RenderHealth(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            return "Health information is only available to a persistent character.";

        var character = _characters.LoadCharacter(actor.Id);
        if (character is null)
            return "Health information is unavailable.";

        var classDefinition = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId).RequireClass(character.ClassId);
        var resourceLabel = classDefinition.Magic?.Type == 5 ? "Kai" : "Mana";
        return $"Health: {character.HitPoints}/{character.MaximumHitPoints}  {resourceLabel}: {character.Mana}/{character.MaximumMana}";
    }

    public string RenderScore(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
        {
            return session.Actor.DisplayName;
        }

        var character = _characters.LoadCharacter(actor.Id);
        if (character is null)
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"Name: {actor.DisplayName}",
                $"Race: {actor.Race}",
                $"Class: {actor.CharacterClass}  Level: {actor.Level}"
            });
        }

        const string green = "\u001b[1;32m";
        const string cyan = "\u001b[1;36m";
        const string reset = "\u001b[0m";
        const int firstColumnWidth = 18;
        const int secondColumnWidth = 21;
        const int rightColumnWidth = 18;
        const int leftWidth = firstColumnWidth + secondColumnWidth;

        static int Read(IReadOnlyDictionary<string, int> values, params string[] keys)
        {
            foreach (var key in keys)
                if (values.TryGetValue(key, out var value)) return value;
            return 0;
        }

        static string ColoredField(string label, object value) =>
            $"{green}{label}{cyan}{value}{reset}";

        static (string Plain, string Colored) FixedField(
            string label,
            object value,
            int width,
            int? valueColumn = null)
        {
            var valueText = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            var prefixPad = valueColumn.HasValue
                ? Math.Max(1, valueColumn.Value - label.Length)
                : 0;
            var plain = label + new string(' ', prefixPad) + valueText;
            var suffixPad = Math.Max(0, width - plain.Length);
            var colored = green + label + new string(' ', prefixPad) + cyan + valueText + reset + new string(' ', suffixPad);
            return (plain + new string(' ', suffixPad), colored);
        }

        static string RightField(string label, object value)
        {
            var valueText = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            var pad = Math.Max(1, rightColumnWidth - label.Length - valueText.Length);
            return green + label + new string(' ', pad) + cyan + valueText + reset;
        }

        static string ComposeLeft(string leftPlain, string leftColored, string rightColored)
        {
            var pad = Math.Max(0, leftWidth - leftPlain.Length);
            return leftColored + new string(' ', pad) + rightColored;
        }

        static string ComposeThree(
            (string Plain, string Colored) first,
            (string Plain, string Colored) second,
            string rightColored) =>
            first.Colored + second.Colored + rightColored;

        var stats = character.BaseStats;
        var skills = character.Skills;
        var semantic = character.SemanticState;
        var derived = semantic?.DerivedStats ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var resistances = semantic?.Resistances ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var armorPrimary = Read(derived, "armor_class", "armour_class", "armor_class_bonus");
        var armorSecondary = Read(derived, "damage_resistance", "damage_resistance_bonus", "defense_value");
        var lives = RuntimeClassicCharacterProgressionFormula.ReadLives(character);
        var creationPoints = ReadCondition(character.Conditions, "character_points", "cp", "creation_points");

        // Native MajorMUD STATUS uses an 18 / 21 / 18 character three-column grid.
        // Within the numeric stat rows the first value starts at column 11 and the
        // second at column 9 of their respective fields; hits/AC have their own
        // fixed value starts. Keeping these columns stable matters to legacy clients.
        var race = FixedField("Race: ", character.RaceName, firstColumnWidth);
        var exp = FixedField("Exp: ", character.Experience, secondColumnWidth);
        var charClass = FixedField("Class: ", character.ClassName, firstColumnWidth);
        var level = FixedField("Level: ", character.Level, secondColumnWidth);
        var hits = FixedField("Hits:", $"{character.HitPoints}/{character.MaximumHitPoints}", firstColumnWidth, 8);
        var armor = FixedField("Armour Class:", $"{armorPrimary}/{armorSecondary}", secondColumnWidth, 15);
        var strength = FixedField("Strength:", Read(stats, "str", "strength"), firstColumnWidth, 11);
        var agility = FixedField("Agility:", Read(stats, "agl", "agi", "agility"), secondColumnWidth, 9);
        var intellect = FixedField("Intellect:", Read(stats, "int", "intellect"), firstColumnWidth, 11);
        var health = FixedField("Health:", Read(stats, "hea", "health"), secondColumnWidth, 9);
        var willpower = FixedField("Willpower:", Read(stats, "wil", "willpower"), firstColumnWidth, 11);
        var charm = FixedField("Charm:", Read(stats, "cha", "charm"), secondColumnWidth, 9);

        var namePlain = "Name: " + character.DisplayName;
        var nameColored = ColoredField("Name: ", character.DisplayName);
        var lines = new List<string>
        {
            ComposeLeft(namePlain, nameColored, RightField("Lives/CP: ", $"{lives}/{creationPoints}")),
            ComposeThree(race, exp, RightField("Perception: ", Read(skills, "perception", "per"))),
            ComposeThree(charClass, level, RightField("Stealth: ", Read(skills, "stealth", "sneak"))),
            ComposeThree(hits, armor, RightField("Thievery: ", Read(skills, "thievery", "thief")))
        };

        var classDefinition = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId)
            .RequireClass(character.ClassId);
        var usesKai = classDefinition.Magic?.Type == 5;
        if (character.MaximumMana > 0)
        {
            var resourceLabel = usesKai ? "Kai:" : "Mana:";
            var maximumManaModified = !usesKai &&
                ((semantic?.DerivedStats.GetValueOrDefault("maximum_mana_bonus") ?? 0) != 0
                 || RuntimeClassicStatusProjection.Numeric(character, "maximum_mana") != 0);
            var resource = maximumManaModified
                ? FixedField(resourceLabel, $"* {character.Mana}/{character.MaximumMana}", firstColumnWidth, 6)
                : FixedField(resourceLabel, $"{character.Mana}/{character.MaximumMana}", firstColumnWidth, usesKai ? 9 : 8);
            var casting = usesKai
                ? (new string(' ', secondColumnWidth), new string(' ', secondColumnWidth))
                : FixedField("Spellcasting:", Read(skills, "spellcasting", "spell_casting"), secondColumnWidth, 14);
            lines.Add(ComposeThree(resource, casting, RightField("Traps: ", Read(skills, "traps", "trap"))));
        }
        else
        {
            lines.Add(new string(' ', leftWidth) + RightField("Traps: ", Read(skills, "traps", "trap")));
        }

        lines.Add(new string(' ', leftWidth) + RightField("Picklocks: ", RuntimeClassicCharacterProgressionFormula.PicklocksValue(character)));
        lines.Add(ComposeThree(strength, agility, RightField("Tracking: ", Read(skills, "tracking", "track"))));
        lines.Add(ComposeThree(intellect, health, RightField("Martial Arts: ", Read(skills, "martial_arts", "martialarts", "ma"))));
        lines.Add(ComposeThree(willpower, charm, RightField("MagicRes: ", Read(resistances, "magic", "magicres", "magic_resistance"))));
        var activeDescriptions = ActiveSpellStatusMessages(session, actor.Id);
        if (activeDescriptions.Count > 0)
        {
            lines.Add(string.Empty);
            lines.AddRange(activeDescriptions);
        }
        return string.Join(Environment.NewLine, lines);
    }


    public string RenderExperience(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "Exp: 0 Level: 1 Exp needed for next level: unavailable";
        var character = _characters.LoadCharacter(actor.Id);
        if (character is null) return "Exp: 0 Level: 1 Exp needed for next level: unavailable";

        var nextLevel = checked(character.Level + 1);
        try
        {
            var schedule = RuntimeCharacterProgressionSchedule.Load(Path.Combine(_characters.ProjectRoot, "Data", character.WorldId));
            var foundation = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId);
            var row = schedule.Require(character, nextLevel, foundation.RequireRace(character.RaceId), foundation.RequireClass(character.ClassId));
            var remaining = Math.Max(0L, row.RequiredExperience - character.Experience);
            var percent = row.RequiredExperience <= 0
                ? 0L
                : (character.Experience * 100L) / row.RequiredExperience;
            return $"Exp: {character.Experience} Level: {character.Level} Exp needed for next level: {remaining} ({row.RequiredExperience}) [{percent}%]";
        }
        catch (Exception ex)
        {
            return $"Exp: {character.Experience} Level: {character.Level} Exp needed for next level: unavailable ({ex.Message})";
        }
    }

    private static int ReadCondition(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
            if (values.TryGetValue(key, out var raw) && int.TryParse(raw, out var value)) return value;
        return 0;
    }

    private static string FormatCharacterSex(string? sex) =>
        string.Equals(sex, "Male", StringComparison.OrdinalIgnoreCase) ? "Male" :
        string.Equals(sex, "Female", StringComparison.OrdinalIgnoreCase) ? "Female" :
        "Unspecified";

    public string RenderInventory(RuntimeSession session) => RenderMajorMudInventorySheet(session);

    // EQUIPMENT is retained as a Reforged convenience command, but its presentation
    // intentionally uses the same native MajorMUD inventory sheet as INVENTORY.
    public string RenderEquipment(RuntimeSession session) => RenderMajorMudInventorySheet(session);

    public string RenderKeys(RuntimeSession session)
    {
        var state = LoadItemState(session);
        if (state is null) return "You have no keys.";
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var keys = state.Items
            .Where(item => string.Equals(item.OwnerId, session.Actor.Id, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(item.LocationKind, RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.LocationKind, RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase))
                && IsClassicKey(catalog.Require(item.CanonicalItemId)))
            .Select(item => FormatClassicQuantity(catalog.Require(item.CanonicalItemId).Name, item.StackQuantity))
            .ToArray();
        return FormatClassicKeyLine(keys);
    }

    private string RenderMajorMudInventorySheet(RuntimeSession session)
    {
        var state = LoadItemState(session);
        if (state is null)
            return "You are carrying nothing.\nYou have no keys.\nWealth: 0 copper farthings\nEncumbrance: 0/0 - None";

        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var owned = state.Items
            .Where(item => string.Equals(item.OwnerId, session.Actor.Id, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(item.LocationKind, RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.LocationKind, RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var readiedLightId = RuntimeEquipmentRuntimeAuthority.ResolveReadiedLightInstanceId(
            state, session.Actor.Id);
        var readiedWeaponId = RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(
            state, session.Actor.Id);
        var currency = RuntimeBundleCInventoryAuthority.ReadCurrency(state);
        var carriedParts = new List<string>();
        var currencyText = RuntimeMajorMudCompatibility.FormatInventoryCurrency(currency);
        if (!string.IsNullOrWhiteSpace(currencyText)) carriedParts.Add(currencyText);

        foreach (var item in owned.Where(item => !IsClassicKey(catalog.Require(item.CanonicalItemId))))
        {
            var definition = catalog.Require(item.CanonicalItemId);
            var quantity = item.StackQuantity > 1 ? $"{item.StackQuantity} {definition.Name}" : definition.Name;
            carriedParts.Add(string.Equals(item.InstanceId, readiedLightId, StringComparison.OrdinalIgnoreCase)
                ? $"{quantity} ({ClassicLightReadiness(catalog, item)})"
                : string.Equals(item.InstanceId, readiedWeaponId, StringComparison.OrdinalIgnoreCase)
                    && item.EquippedSlot is null
                    ? $"{quantity} (readied)"
                    : item.EquippedSlot is null
                        ? quantity
                        : $"{quantity} ({ClassicDisplaySlot(catalog, item)})");
        }

        var lines = new List<string>
        {
            carriedParts.Count == 0 ? "You are carrying nothing." : "You are carrying " + string.Join(", ", carriedParts)
        };

        var keyNames = owned
            .Where(item => IsClassicKey(catalog.Require(item.CanonicalItemId)))
            .Select(item => FormatClassicQuantity(catalog.Require(item.CanonicalItemId).Name, item.StackQuantity))
            .ToArray();
        lines.Add(FormatClassicKeyLine(keyNames));

        var character = _characters.LoadCharacter(session.Actor.Id);
        if (character is null)
        {
            lines.Add($"Wealth: {currency.CopperValue} copper farthings");
            lines.Add("Encumbrance: 0/0 - None");
            return string.Join(Environment.NewLine, lines);
        }

        var projection = RuntimeBundleCInventoryAuthority.Project(character, catalog);
        lines.Add($"Wealth: {projection.Currency.CopperValue} copper farthings");
        var strength = ReadClassicStrength(character, owned, catalog);
        var baseCapacity = RuntimeMajorMudCompatibility.CalculateBaseMaximumEncumberance(strength);
        var modifierPercent = ReadClassicEncumberanceCapacityBonus(character, session.World.Definition.Id);
        var capacity = RuntimeMajorMudCompatibility.ApplyEncumberanceModifier(baseCapacity, modifierPercent);
        lines.Add(RuntimeMajorMudCompatibility.FormatEncumberanceLine(projection.TotalCarriedWeight, capacity));
        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsClassicKey(RuntimeItemDefinition definition) =>
        definition.ItemType.Equals("key", StringComparison.OrdinalIgnoreCase);

    private static string FormatClassicQuantity(string name, int quantity) =>
        Math.Max(1, quantity) > 1 ? $"{Math.Max(1, quantity)} {name}" : name;

    internal static string FormatClassicKeyLine(IReadOnlyList<string> keyNames) =>
        keyNames.Count == 0
            ? "You have no keys."
            : "You have the following keys:  " + string.Join(", ", keyNames) + ".";

    private int ReadClassicEncumberanceCapacityBonus(RuntimeCharacterRecord character, string worldId)
    {
        var characterCatalog = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, worldId);
        var permanent = characterCatalog.RequireRace(character.RaceId).AbilitySlots
            .Where(grant => grant.AbilityId == 96).Sum(grant => grant.Value)
            + characterCatalog.RequireClass(character.ClassId).AbilitySlots
                .Where(grant => grant.AbilityId == 96).Sum(grant => grant.Value);
        var equipment = character.SemanticState?.DerivedStats.GetValueOrDefault("encumbrance_modifier_percent") ?? 0;

        // The recovered maximum-capacity function proves positive permanent and
        // equipment Ability 96 bonuses here. Timed spell/status effects remain
        // separate from the displayed maximum until their native interaction is proven.
        return checked(Math.Max(0, permanent + equipment));
    }

    private static int ReadClassicStrength(
        RuntimeCharacterRecord character,
        IReadOnlyList<RuntimeItemInstance> owned,
        RuntimeItemCatalog itemCatalog)
    {
        var baseStrength = 0;
        foreach (var key in new[] { "strength", "str", "STR" })
        {
            if (!character.BaseStats.TryGetValue(key, out var value)) continue;
            baseStrength = value;
            break;
        }

        var readiedWeaponId = RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(
            character.ItemState, character.CharacterId);
        var readiedLightId = RuntimeEquipmentRuntimeAuthority.ResolveReadiedLightInstanceId(
            character.ItemState, character.CharacterId);
        var equipmentStrength = owned
            .Where(item => item.EquippedSlot is not null
                || item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
                    && (item.InstanceId.Equals(readiedWeaponId, StringComparison.OrdinalIgnoreCase)
                        || item.InstanceId.Equals(readiedLightId, StringComparison.OrdinalIgnoreCase)))
            .Sum(item => itemCatalog.Require(item.CanonicalItemId).EquipmentAbilityModifiers.GetValueOrDefault(46));
        var activeStrength = RuntimeClassicStatusProjection.Numeric(character, "stat_strength");
        return Math.Max(0, checked(baseStrength + equipmentStrength + activeStrength));
    }

    private static string ClassicDisplaySlot(RuntimeItemCatalog catalog, RuntimeItemInstance item)
    {
        var relation = catalog.Equipment(item.CanonicalItemId);
        if (relation?.TwoHanded == true) return "Weapon Hand";
        return (item.EquippedSlot ?? relation?.Slot ?? "equipped").ToUpperInvariant() switch
        {
            "LIGHT" => ClassicLightReadiness(catalog, item),
            "MAIN_HAND" or "PRIMARY_HAND" or "WEAPON" or "WEAPON_HAND" or "HAND" or "WIELD" => "Weapon Hand",
            "OFF_HAND" or "SECONDARY_HAND" => "Off-Hand",
            "ARMS" => "Arms",
            "TORSO" or "BODY" or "CHEST" => "Torso",
            "BACK" => "Back",
            "WRIST" or "WRISTS" or "WRIST_1" or "WRIST_2" => "Wrist",
            "FINGER" or "RING" or "FINGER_1" or "FINGER_2" => "Finger",
            "WAIST" or "BELT" => "Waist",
            "FEET" or "BOOTS" => "Feet",
            "HANDS" or "GLOVES" => "Hands",
            "HEAD" or "HELM" => "Head",
            "LEGS" => "Legs",
            "NECK" => "Neck",
            "EARS" or "EAR" => "Ears",
            "FACE" => "Face",
            "EYES" or "EYE" => "Eyes",
            "WORN" or "WORN_ITEM" => "Worn",
            "SHIELD" => "Off-Hand",
            var slot => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slot.Replace('_', ' ').ToLowerInvariant())
        };
    }

    private static string ClassicLightReadiness(RuntimeItemCatalog catalog, RuntimeItemInstance item)
    {
        // Original inventory shows the remaining fuel with the readied marker,
        // not a fabricated "current/maximum" equipment durability meter.
        var definition = catalog.Require(item.CanonicalItemId);
        var remaining = item.Charges < 0 ? Math.Max(0, definition.MaximumUses) : Math.Max(0, item.Charges);
        return $"readied/{remaining}";
    }

    public string Save(RuntimeSession session)
    {
        if (!session.Actor.IsPersistentCharacter)
        {
            return "This session does not have a persistent character to save.";
        }

        _characters.SaveSession(session);
        return $"{session.Actor.DisplayName} saved at {session.CurrentRoom.Name}.";
    }

    public string RenderWho() => _sessions.RenderWho();

    public string RenderIntegrationStatus(RuntimeSession session)
    {
        var persistent = session.Actor.IsPersistentCharacter ? "persistent" : "transient";
        var itemState = LoadItemState(session) is null ? "unavailable" : "attached";
        return string.Join(Environment.NewLine, new[]
        {
            $"Session: {session.Id}",
            $"Actor: {session.Actor.DisplayName} ({persistent})",
            $"World: {session.World.Definition.Id}",
            $"Room: {session.CurrentRoom.Id}",
            $"Item state: {itemState}",
            "Live domains: room, movement, presence, communication, inventory, equipment, banking, save/reconnect",
            "Live combat domains: generated NPC targeting, encounter admission, NPC attack-profile loading, status, flee, movement guard",
            "Policy-gated domains: player AV/DV derivation, combat damage rounds, spells, abilities, quests, shop/trainer transactions, death/respawn"
        });
    }

    public string ExecuteAuditCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (command.Arguments.Count == 0)
        {
            return ClassicRuntimeAudit.RenderRuntimeAudit(_characters.ProjectRoot, session.World, Living(session));
        }

        if (command.Arguments[0].Equals("room", StringComparison.OrdinalIgnoreCase))
        {
            var query = string.Join(" ", command.Arguments.Skip(1)).Trim();
            if (string.IsNullOrWhiteSpace(query)) query = session.CurrentRoom.Id;
            return ClassicRuntimeAudit.RenderRoomInspection(_characters.ProjectRoot, session.World, Living(session), query, RenderRoom(session));
        }

        if (command.Arguments[0].Equals("spawns", StringComparison.OrdinalIgnoreCase))
        {
            return ClassicRuntimeAudit.RenderSpawnVerification(session.World, Living(session));
        }

        if (command.Arguments[0].Equals("megamud", StringComparison.OrdinalIgnoreCase))
        {
            return ClassicRuntimeAudit.RenderMegaMudCompatibility(session.World);
        }

        if (command.Arguments[0].Equals("parity", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeMajorMudParityAudit.Render(RuntimeMajorMudParityAudit.Run(_characters.ProjectRoot));
        }

        if (command.Arguments[0].Equals("timers", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeMajorMudTimerAudit.Render(RuntimeMajorMudTimerAudit.Run(_characters.ProjectRoot, Living(session)));
        }

        if (command.Arguments[0].Equals("combat", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeMajorMudCombatAudit.Render(RuntimeMajorMudCombatAudit.Run(_characters.ProjectRoot));
        }

        return "Use: audit, audit room <room>, audit spawns, audit megamud, audit parity, audit timers, or audit combat.";
    }

    private RuntimeItemState? LoadItemState(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
        {
            return null;
        }

        return _characters.LoadCharacter(actor.Id)?.ItemState;
    }

    private static string RenderItem(RuntimeItemInstance item)
    {
        var quantity = item.StackQuantity > 1 ? $" x{item.StackQuantity}" : string.Empty;
        var charges = item.Charges > 0 ? $" charges={item.Charges}" : string.Empty;
        return $"- {item.CanonicalItemId}{quantity}{charges} [{item.InstanceId}]";
    }
}

public sealed class RuntimeLiveGameplaySmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeLiveGameplaySmokeReport Run()
    {
        var report = new RuntimeLiveGameplaySmokeReport();
        try
        {
            var look = RuntimeCommand.Parse("look");
            Require(look.Name == "look" && look.Arguments.Count == 0, "parse-look");
            report.Checks.Add("parse-look");

            Require(RuntimeCommand.Parse("l").Name == "look", "parse-look-short");
            Require(RuntimeCommand.Parse("lo").Name == "look", "parse-look-prefix");
            report.Checks.Add("parse-look-abbreviations");

            var examine = RuntimeCommand.Parse("examine ancient sword");
            Require(examine.Name == "examine" && examine.ArgumentText == "ancient sword", "parse-arguments");
            report.Checks.Add("parse-arguments");

            var north = RuntimeCommand.Parse("north");
            Require(north.IsMovement, "movement-alias");
            report.Checks.Add("movement-alias");

            Require(RuntimeCommand.Parse("n").Name == "north", "movement-north-short");
            report.Checks.Add("movement-abbreviations");

            var inventory = RuntimeCommand.Parse("i");
            Require(inventory.Name == "inventory", "command-alias");
            Require(RuntimeCommand.Parse("inv").Name == "inventory", "inventory-inv-alias");
            report.Checks.Add("command-alias");

            Require(RuntimeCommand.Parse("g silver coin").Name == "get", "get-g-alias");
            Require(RuntimeCommand.Parse("g silver coin").ArgumentText == "silver coin", "get-g-arguments");
            report.Checks.Add("get-g-alias");

            Require(RuntimeCommand.Parse("eq").Name == "equipment", "equipment-alias");
            report.Checks.Add("equipment-alias");
            Require(RuntimeCommand.Parse("key").Name == "keys", "keys-prefix");
            Require(RuntimeCommand.Parse("keys").Name == "keys", "keys-command");
            report.Checks.Add("keys-command-routing");
            Require(RuntimeCommand.Parse("eq longsword").Name == "equipment" && RuntimeCommand.Parse("eq longsword").ArgumentText == "longsword", "equipment-equip-context-alias");
            Require(RuntimeCommand.Parse("equip longsword").Name == "equip" && RuntimeCommand.Parse("equip longsword").ArgumentText == "longsword", "equip-command-route");
            report.Checks.Add("equipment-equip-context-alias");
            report.Checks.Add("equip-command-route");

            Require(RuntimeCommand.Parse("st").Name == "score", "st-alias");
            Require(RuntimeCommand.Parse("stat").Name == "score", "stat-alias");
            Require(RuntimeCommand.Parse("stats").Name == "score", "stats-alias");
            Require(RuntimeCommand.Parse("status").Name == "score", "status-alias");
            report.Checks.Add("stat-command-aliases");

            Require(RuntimeCommand.Parse("op n").Name == "open", "open-prefix");
            Require(RuntimeCommand.Parse("ba north").Name == "bash", "bash-prefix");
            report.Checks.Add("interaction-command-prefixes");

            Require(RuntimeCommand.Parse("s").Name.Equals("south", StringComparison.Ordinal), "movement-south-s");
            Require(RuntimeCommand.Parse("se").Name.Equals("southeast", StringComparison.Ordinal), "movement-southeast-se");
            Require(RuntimeCommand.Parse("st").Name == "score", "status-st-alias");
            report.Checks.Add("native-status-st-alias");

            var save = RuntimeCommand.Parse("save");
            Require(save.Name == "save", "save-command");
            report.Checks.Add("save-command");

            Require(RuntimeCommand.Parse("qui").Name == "quit", "quit-prefix");
            Require(RuntimeCommand.Parse("logout").Name == "quit", "logout-alias");
            Require(RuntimeCommand.Parse("exit").Name == "quit", "exit-alias");
            report.Checks.Add("session-exit-command-routing");

            var audit = RuntimeCommand.Parse("audit room Village Entrance");
            Require(audit.Name == "audit" && audit.ArgumentText == "room Village Entrance", "audit-command");
            Require(RuntimeCommand.Parse("audit parity").ArgumentText == "parity", "audit-parity-command");
            Require(RuntimeCommand.Parse("audit timers").ArgumentText == "timers", "audit-timers-command");
            Require(RuntimeCommand.Parse("audit combat").ArgumentText == "combat", "audit-combat-command");
            Require(RuntimeMajorMudParityAudit.Percent(32, 94) == 34.0d, "audit-parity-percent");
            report.Checks.Add("audit-command");
            report.Checks.Add("audit-parity-command");
            report.Checks.Add("audit-timers-command");
            report.Checks.Add("audit-combat-command");
            report.Checks.Add("audit-parity-percent");

            Require(RuntimeCommand.Parse("statline full").Name == "statline", "classic-statline-command");
            Require(RuntimeCommand.Parse("set statline full custom [%h/%H]").Name == "set", "classic-set-statline-command");
            report.Checks.Add("classic-statline-command-routing");

            var megaMudEntranceExits = new[]
            {
                new RuntimeExit { Direction = "N", TargetMapId = 1, TargetRoomNumber = 1, GateKind = "open" },
                new RuntimeExit { Direction = "S", TargetMapId = 1, TargetRoomNumber = 1, GateKind = "open" },
                new RuntimeExit { Direction = "W", TargetMapId = 1, TargetRoomNumber = 1, GateKind = "open" },
                new RuntimeExit { Direction = "SE", TargetMapId = 1, TargetRoomNumber = 1, GateKind = "open" },
                new RuntimeExit { Direction = "E", TargetMapId = 1, TargetRoomNumber = 1, GateKind = "text_command" }
            };
            Require(RuntimeMajorMudCompatibility.ComputeMegaMudRoomHash("Newhaven, Village Entrance") == "2D6", "megamud-room-hash");
            Require(RuntimeMajorMudCompatibility.ComputeMegaMudExitCode(megaMudEntranceExits) == "01045", "megamud-exit-code");
            Require(RuntimeMajorMudCompatibility.RenderStatlineTemplate("[%h/%H %m/%M]%r", 18, 20, 4, 7, 0, 0, 0, string.Empty) == "[18/20 4/7]", "majormud-custom-statline-template");
            Require(RuntimeMajorMudCompatibility.FormatDefaultStatlinePlain(328, 270, usesKai: false, resting: false) == "[HP=328/MA=270]:", "majormud-default-mana-statline");
            Require(RuntimeMajorMudCompatibility.FormatDefaultStatlinePlain(571, 64, usesKai: true, resting: false) == "[HP=571/KAI=64]:", "majormud-default-kai-statline");
            Require(RuntimeMajorMudCompatibility.FormatDefaultStatlinePlain(182, 274, usesKai: false, resting: true) == "[HP=182/MA=274]: (Resting)", "majormud-resting-statline");
            Require(RuntimeMajorMudCompatibility.CalculateBaseMaximumEncumberance(100) == 4800, "majormud-encumbrance-base-100");
            Require(RuntimeMajorMudCompatibility.CalculateBaseMaximumEncumberance(110) == 5640, "majormud-encumbrance-over-100");
            Require(RuntimeMajorMudCompatibility.CalculateBaseMaximumEncumberance(170) == 10680, "majormud-encumbrance-over-100-slope");
            Require(RuntimeMajorMudCompatibility.ApplyEncumberanceModifier(5640, 20) == 6768, "majormud-encumbrance-ability-modifier");
            Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(1567, 6768) == "Light", "majormud-encumbrance-light-23");
            Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(1029, 3840) == "Light", "majormud-encumbrance-light-26");
            Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(1875, 4320) == "Medium", "majormud-encumbrance-medium-43");
            Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(1535, 2880) == "Medium", "majormud-encumbrance-medium-53");
            Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(6473, 10680) == "Medium", "majormud-encumbrance-medium-60");
            Require(RuntimeMajorMudCompatibility.ClassifyEncumberance(7776, 7776) == "Heavy", "majormud-encumbrance-heavy-100");
            Require(RuntimeMajorMudCompatibility.ApplyEncumberanceModifier(
                RuntimeMajorMudCompatibility.CalculateBaseMaximumEncumberance(120), 20) == 7776, "majormud-dwarf-paladin-capacity");
            Require(RuntimeMajorMudCompatibility.FormatEncumberanceLine(1567, 6768) == "Encumbrance: 1567/6768 - Light [23%]", "majormud-encumbrance-line");
            Require(RuntimeMajorMudCompatibility.FormatInventoryCurrency(new RuntimeClassicInventoryCurrency(1, 41, 41, 0, 0)) == "1 Runic Coins, 41 platinum pieces, 41 gold crowns", "majormud-inventory-currency-line");
            Require(RuntimeLiveGameplayFacade.FormatClassicKeyLine(new[] { "helmet key", "2 dragon key" }) == "You have the following keys:  helmet key, 2 dragon key.", "majormud-key-ring-line");
            report.Checks.Add("majormud-native-inventory-encumbrance-format");
            Require(RuntimeMajorMudCompatibility.FormatRoomOccupants(
                new[] { (Name: "Night", IsFiend: false), (Name: "Rave", IsFiend: true) },
                new[] { "ju-ju zombie", "ju-ju zombie" })
                == "Also here: Night, Rave*, ju-ju zombie, ju-ju zombie.", "majormud-fiend-marker");
            report.Checks.Add("megamud-room-signature-compatibility");
            report.Checks.Add("majormud-native-prompt-and-presence-format");

            Require(RuntimeCommand.SupportedCommands.SetEquals(RuntimeCommand.DefaultRegistrations.Select(registration => registration.Canonical)), "supported-command-catalog");
            Require(RuntimeCommandRuntimeRouteCatalog.Definitions.Count == RuntimeCommand.DefaultRegistrations.Count, "runtime-route-catalog-count");
            report.Checks.Add("supported-command-catalog");
            report.Checks.Add("runtime-route-catalog-count");
        }
        catch (Exception ex)
        {
            report.Failures.Add(ex.Message);
        }
        return report;
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
