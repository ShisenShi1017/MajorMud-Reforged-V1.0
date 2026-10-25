using System.Globalization;

namespace RealmGate.Server;

/// <summary>
/// Production execution boundary for decoded WCC textblock plans. The graph and opcode
/// vocabulary are generated authorities; this class only adapts them to the active host state.
/// </summary>
public sealed partial class RuntimeLiveGameplayFacade
{
    private static readonly IReadOnlySet<string> DecodedNarrativeLiveOpcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "message", "text", "route", "random", "break", "delay", "adddelay",
        "class", "race", "minlevel", "maxlevel", "levelcheck", "goodaligned", "evilaligned", "check",
        "checkability", "failability", "testability", "testskill", "checkspell", "checkitem", "failitem",
        "roomitem", "failroomitem", "nomonsters", "monsters", "needmonster", "price",
        "giveability", "addability", "removeability", "addexp", "addevil", "goodability",
        "cast", "takeitem", "giveitem", "clearitem", "givecoins", "teleport", "summon", "remoteaction"
    };

    public static bool IsDecodedNarrativeOpcodeLiveBound(string opcode) =>
        DecodedNarrativeLiveOpcodes.Contains((opcode ?? string.Empty).Trim());

    private sealed record DecodedWorldIntent(string Opcode, IReadOnlyList<int> Arguments);

    private sealed record DecodedPlanAttempt(bool Admitted, string Output, RuntimeCharacterRecord? Character);

    private string ExecuteDecodedNarrativePlans(
        RuntimeSession session,
        IReadOnlyList<RuntimeClassicTextblockExecutionPlan> plans,
        string source)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            throw new InvalidOperationException("A persistent character is required for decoded Classic narrative execution.");
        if (plans.Count == 0) throw new InvalidOperationException("No decoded narrative plan was supplied.");

        string firstFailure = string.Empty;
        foreach (var sourcePlan in plans)
        {
            var expanded = RuntimeClassicRandomTableAuthority.Load(
                    _characters.ProjectRoot,
                    session.World.Definition.Id,
                    new RuntimeRoomActionRandomSource())
                .Expand(sourcePlan.Operations, out _);
            var plan = sourcePlan with { Operations = expanded };
            var attempt = TryExecuteDecodedNarrativePlan(session, actor, plan, source);
            if (attempt.Admitted) return attempt.Output;
            if (firstFailure.Length == 0) firstFailure = attempt.Output;
        }

        return firstFailure;
    }

    private DecodedPlanAttempt TryExecuteDecodedNarrativePlan(
        RuntimeSession session,
        RuntimeCharacterActor actor,
        RuntimeClassicTextblockExecutionPlan plan,
        string source)
    {
        var before = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found for decoded narrative execution.");
        var items = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var worldIntents = RuntimeQuestWorldIntentValidator.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var working = new RuntimeQuestDialogueContext(before, items, worldIntents);
        var outputs = new List<string>();
        var pendingWorld = new List<DecodedWorldIntent>();
        var pendingDelay = 0;
        var pendingPrice = 0L;

        foreach (var operation in plan.Operations.OrderBy(operation => operation.Sequence).ThenBy(operation => operation.Line))
        {
            if (IsDecodedNarrativeCondition(operation))
            {
                if (EvaluateDecodedNarrativeCondition(session, before, working, operation, out var failure))
                {
                    if (operation.Opcode.Equals("price", StringComparison.OrdinalIgnoreCase))
                        pendingPrice = checked(pendingPrice + Math.Max(0, Argument(operation, 0)));
                    continue;
                }
                return new(false, RenderDecodedNarrativeFailure(session, operation, failure), null);
            }

            switch (operation.Opcode)
            {
                case "message":
                    AddDecodedPayload(outputs, NativePayloads(session.World.Definition.Id).RequireMessage(RequireNarrativeTarget(operation)));
                    break;
                case "text":
                    AddDecodedPayload(outputs, NativePayloads(session.World.Definition.Id).RequireTextblock(RequireNarrativeTarget(operation)));
                    break;
                case "route":
                    // Native route 0 is the decoded terminal/no-output sentinel.
                    if (operation.NumericArguments.FirstOrDefault() > 0)
                        AddDecodedPayload(outputs, NativePayloads(session.World.Definition.Id).RequireTextblock(operation.NumericArguments[0]));
                    break;
                case "break":
                    goto Commit;
                case "delay":
                case "adddelay":
                    pendingDelay = checked(pendingDelay + Math.Max(0, operation.NumericArguments.FirstOrDefault()));
                    break;
                case "giveability":
                    working.GiveAbility(Argument(operation, 0), Argument(operation, 1));
                    break;
                case "addability":
                    working.AddAbility(Argument(operation, 0), Argument(operation, 1));
                    break;
                case "removeability":
                    working.RemoveAbility(Argument(operation, 0));
                    break;
                case "addexp":
                    working.AddExperience(Argument(operation, 0));
                    break;
                case "addevil":
                    working.AddAlignment(-Argument(operation, 0));
                    break;
                case "giveitem":
                    working.GiveItem(Argument(operation, 0));
                    break;
                case "takeitem":
                    working.TakeItem(Argument(operation, 0));
                    break;
                case "clearitem":
                    working.ClearItem(Argument(operation, 0));
                    break;
                case "givecoins":
                    working.CreditCoins(Argument(operation, 0), operation.Arguments.Skip(1).FirstOrDefault() ?? "C");
                    break;
                case "cast":
                case "teleport":
                case "summon":
                case "remoteaction":
                    ValidateDecodedWorldIntent(session, worldIntents, operation);
                    pendingWorld.Add(new(operation.Opcode, operation.NumericArguments));
                    break;
                case "random":
                    throw new InvalidOperationException("A decoded RANDOM operation reached execution without expansion.");
                case "price":
                    // PRICE is normally classified as a condition. Keep this defensive path so a
                    // generated projection cannot admit the purchase without charging it.
                    pendingPrice = checked(pendingPrice + Math.Max(0, Argument(operation, 0)));
                    break;
                case "goodability":
                    // GOODABILITY is a decoded alignment predicate despite its historical effect label.
                    if (!EvaluateDecodedNarrativeCondition(session, before, working, operation, out var goodFailure))
                        return new(false, RenderDecodedNarrativeFailure(session, operation, goodFailure), null);
                    break;
                default:
                    throw new InvalidOperationException($"Decoded narrative opcode '{operation.Opcode}' has no production live binding.");
            }
        }

    Commit:
        if (pendingPrice > 0) working.DebitCopper(pendingPrice);
        var updated = working.ToCharacterRecord();
        var originalRoom = session.CurrentRoom;
        var summoned = new List<string>();
        RuntimeSpellState? spellSnapshot = null;
        try
        {
            _characters.SaveCharacter(updated, source);
            if (pendingWorld.Any(intent => intent.Opcode.Equals("cast", StringComparison.OrdinalIgnoreCase)))
            {
                EnsureLivePowers(session);
                spellSnapshot = _liveSpellEngine!.Snapshot();
            }
            foreach (var intent in pendingWorld)
                ApplyDecodedWorldIntent(session, intent, source, summoned);
            if (pendingDelay > 0) session.AddClassicCommandDelay(pendingDelay);
            _characters.SaveSession(session);
            return new(true, string.Join(Environment.NewLine, outputs), updated);
        }
        catch
        {
            if (spellSnapshot is not null)
            {
                _liveSpellEngine!.RestoreSnapshot(spellSnapshot);
                PersistLivePowers(session.World.Definition.Id);
            }
            foreach (var instanceId in summoned) Living(session).RemoveSummonedNpc(instanceId);
            if (!session.CurrentRoom.Id.Equals(originalRoom.Id, StringComparison.OrdinalIgnoreCase))
                session.MoveToWorld(session.World, originalRoom, source + ":rollback");
            _characters.SaveCharacter(before, source + ":rollback");
            throw;
        }
    }

    private bool EvaluateDecodedNarrativeCondition(
        RuntimeSession session,
        RuntimeCharacterRecord character,
        RuntimeQuestDialogueContext working,
        RuntimeClassicTextblockOperation operation,
        out string failure)
    {
        failure = string.Empty;
        if (operation.Opcode.Equals("testskill", StringComparison.OrdinalIgnoreCase))
        {
            var stat = operation.Arguments.FirstOrDefault() ?? string.Empty;
            var modifier = operation.NumericArguments.FirstOrDefault();
            var score = ReadDecodedSkillValue(character, stat);
            var passed = Random.Shared.Next(1, 101) <= Math.Clamp(score + modifier, 1, 100);
            if (!passed) failure = $"Decoded {stat} test failed.";
            return passed;
        }
        if (operation.Opcode.Equals("check", StringComparison.OrdinalIgnoreCase)) return true;

        var current = working.ToCharacterRecord();
        var state = current.ItemState ?? new RuntimeItemState([], 0, new Dictionary<string, string>(), []);
        var inventory = state.Items
            .Where(item => item.OwnerId?.Equals(current.CharacterId, StringComparison.OrdinalIgnoreCase) == true)
            .Select(item => ParseLegacyItemId(item.CanonicalItemId))
            .Where(id => id > 0)
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());
        var roomItems = Living(session).GroundItems(session.CurrentRoom.Id)
            .Select(item => ParseLegacyItemId(item.CanonicalItemId))
            .Where(id => id > 0)
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());
        var presentNpcs = LivingNpcsInRoom(session).Where(npc => npc.Alive)
            .Select(npc => ParseLegacyId(npc.NpcId)).ToHashSet();
        var abilities = current.Conditions
            .Where(pair => pair.Key.StartsWith("quest.ability.", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(pair.Key["quest.ability.".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                && int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .ToDictionary(
                pair => int.Parse(pair.Key["quest.ability.".Length..], CultureInfo.InvariantCulture),
                pair => int.Parse(pair.Value, CultureInfo.InvariantCulture));
        var spells = current.Spells.Select(ParseLegacyItemId).Where(id => id > 0).ToHashSet();
        var alignment = current.Conditions.TryGetValue("alignment", out var alignmentText)
            && int.TryParse(alignmentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAlignment)
                ? parsedAlignment : 0;
        var conditionState = new RuntimeClassicQuestConditionState(
            current.Level,
            ParseLegacyItemId(current.ClassId),
            ParseLegacyItemId(current.RaceId),
            alignment,
            abilities,
            spells,
            inventory,
            roomItems,
            presentNpcs,
            RuntimeBundleCInventoryAuthority.ReadCurrency(state).CopperValue);
        var decision = RuntimeClassicQuestConditionAndTransactionAuthority.Evaluate(operation, conditionState);
        failure = decision.Failure ?? string.Empty;
        return decision.Passed;
    }

    private static bool IsDecodedNarrativeCondition(RuntimeClassicTextblockOperation operation) => operation.Opcode is
        "class" or "race" or "minlevel" or "maxlevel" or "levelcheck" or "goodaligned" or "evilaligned" or
        "check" or "checkability" or "failability" or "testability" or "testskill" or "checkspell" or
        "checkitem" or "failitem" or "roomitem" or "failroomitem" or "nomonsters" or "monsters" or
        "needmonster" or "price" or "goodability";

    private string RenderDecodedNarrativeFailure(RuntimeSession session, RuntimeClassicTextblockOperation operation, string fallback)
    {
        var failureId = operation.TargetTextblockId
            ?? (operation.Opcode.Equals("testskill", StringComparison.OrdinalIgnoreCase)
                ? operation.NumericArguments.Skip(1).FirstOrDefault()
                : operation.NumericArguments.Skip(1).LastOrDefault());
        if (failureId > 0)
        {
            try { return NativePayloads(session.World.Definition.Id).RequireMessage(failureId); }
            catch (KeyNotFoundException) { return NativePayloads(session.World.Definition.Id).RequireTextblock(failureId); }
        }
        return fallback;
    }

    private static int ReadDecodedSkillValue(RuntimeCharacterRecord character, string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        foreach (var source in new[] { character.Skills, character.BaseStats })
            if (source.TryGetValue(normalized, out var value) || source.TryGetValue(name, out value)) return value;
        return 0;
    }

    private static int ParseLegacyItemId(string value)
    {
        var normalized = value ?? string.Empty;
        var dot = normalized.LastIndexOf('.');
        return int.TryParse(dot >= 0 ? normalized[(dot + 1)..] : normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0;
    }

    private static int Argument(RuntimeClassicTextblockOperation operation, int index) =>
        operation.NumericArguments.Count > index
            ? operation.NumericArguments[index]
            : throw new InvalidOperationException($"Decoded {operation.Opcode} operation is missing numeric argument {index}.");

    private static int RequireNarrativeTarget(RuntimeClassicTextblockOperation operation)
    {
        var target = operation.TargetTextblockId ?? operation.NumericArguments.FirstOrDefault();
        return target > 0 ? target : throw new InvalidOperationException($"Decoded {operation.Opcode} operation has no payload target.");
    }

    private static void AddDecodedPayload(ICollection<string> output, string payload)
    {
        if (!string.IsNullOrWhiteSpace(payload)) output.Add(payload);
    }

    private static void ValidateDecodedWorldIntent(
        RuntimeSession session,
        RuntimeQuestWorldIntentValidator validator,
        RuntimeClassicTextblockOperation operation)
    {
        switch (operation.Opcode)
        {
            case "cast": validator.Validate("cast", $"spell.{Argument(operation, 0)}"); break;
            case "summon": validator.Validate("summon", $"npc.{Argument(operation, 0)}"); break;
            case "teleport":
                var room = session.World.FindRoom(Argument(operation, 1), Argument(operation, 0));
                if (room is null) throw new InvalidOperationException($"Decoded teleport destination {Argument(operation, 1)}/{Argument(operation, 0)} is unavailable.");
                break;
            case "remoteaction": _ = RuntimeNativeRemoteActionIntent.Parse(string.Join('.', operation.NumericArguments)); break;
        }
    }

    private void ApplyDecodedWorldIntent(
        RuntimeSession session,
        DecodedWorldIntent intent,
        string source,
        ICollection<string> summoned)
    {
        switch (intent.Opcode)
        {
            case "cast":
                ApplyDecodedNarrativeSpell(session, intent.Arguments[0]);
                break;
            case "teleport":
                var destination = session.World.FindRoom(intent.Arguments[1], intent.Arguments[0])!;
                session.MoveToWorld(session.World, destination, source + ":teleport");
                AdmitClassicRoomEntryNpcRespawns(session);
                break;
            case "summon":
                var npc = SummonLivingNpc(session, $"npc.{intent.Arguments[0]}", source);
                summoned.Add(npc.InstanceId);
                break;
            case "remoteaction":
                Living(session).RecordQuestRemoteAction(string.Join('.', intent.Arguments), session.CurrentRoom.Id, source);
                break;
        }
    }

    private void ApplyDecodedNarrativeSpell(RuntimeSession session, int legacySpellId)
    {
        if (legacySpellId is >= 219 and <= 224 && session.Actor is RuntimeCharacterActor actor)
        {
            ApplyRecoveredHealerSpell(session, actor.Id, legacySpellId);
            return;
        }

        EnsureLivePowers(session);
        var spell = _liveSpellCatalog!.Require($"spell.{legacySpellId}");
        if (session.Actor is not RuntimeCharacterActor characterActor)
            throw new InvalidOperationException("Decoded narrative CAST requires a persistent character target.");
        var character = _characters.LoadCharacter(characterActor.Id)
            ?? throw new InvalidOperationException("Decoded narrative spell target character was not found.");

        var before = _liveSpellEngine!.Snapshot();
        if (!_liveSpellEngine.Knows(characterActor.Id, spell.Id))
            _liveSpellEngine.Learn(characterActor.Id, spell.Id, new Dictionary<string, string>
            {
                ["authority"] = "decoded-native-narrative-cast",
                ["temporary"] = "true"
            });
        _liveSpellEngine.SetResources(characterActor.Id, new RuntimeSpellResources(
            Math.Max(character.Mana, spell.ManaCost),
            Math.Max(ReadEnergy(character), spell.EnergyCost)));
        var execution = new RuntimeSpellExecutionEngine(
            _liveSpellCatalog,
            _liveSpellEngine,
            new GeneratedLiveSpellOperationPolicy(this, session),
            new LiveSpellEventSink(this, session));
        var result = execution.Cast(new RuntimeSpellCastRequest(
            characterActor.Id,
            spell.Id,
            new RuntimeSpellTarget("character", characterActor.Id),
            DateTimeOffset.UtcNow));
        if (!result.Succeeded)
        {
            _liveSpellEngine.RestoreSnapshot(before);
            throw new InvalidOperationException(result.FailureMessage ?? result.FailureCode ?? $"Decoded narrative spell {legacySpellId} failed.");
        }

        var applied = _liveSpellEngine.Snapshot();
        _liveSpellEngine.RestoreSnapshot(new RuntimeSpellState(before.KnownSpells, applied.ActiveEffects, before.Resources));
        SynchronizeCharacterSpellStatuses(new RuntimeSpellTarget("character", characterActor.Id), DateTimeOffset.UtcNow,
            $"decoded-native-cast:spell.{legacySpellId}");
        PersistLivePowers(session.World.Definition.Id);
    }
}
