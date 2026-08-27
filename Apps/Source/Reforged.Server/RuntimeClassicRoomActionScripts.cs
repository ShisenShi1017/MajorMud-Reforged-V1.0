namespace Reforged.Server;

/// <summary>
/// Evidence-gated executor for room-owned MajorMUD textblock action scripts.
/// Pass 23 intentionally executes only two recovered families:
///  - simple healer purchases whose decoded line is monsters -> price -> cast(219..224) -> adddelay;
///  - immediate room relocations whose decoded line is presentation -> teleport -> presentation.
/// Other decoded room actions remain owned by their existing runtime consumers or fail closed until their
/// condition/timing semantics are recovered.
/// </summary>
public sealed partial class RuntimeLiveGameplayFacade
{
    private bool TryExecuteClassicRoomActionScript(RuntimeSession session, RuntimeCommand command, out string result)
    {
        result = string.Empty;
        var entered = string.Join(' ', new[] { command.Name }.Concat(command.Arguments)).Trim();
        var graph = RuntimeClassicTextblockGraphAuthority.Load(_characters.ProjectRoot);
        if (!graph.TryResolveRoomActionPlan(session.CurrentRoom.MapId, session.CurrentRoom.RoomNumber, entered, out var plan))
            return false;

        if (IsRecoveredSimpleHealerPlan(plan))
        {
            result = ExecuteRecoveredSimpleHealerPlan(session, plan);
            return true;
        }

        if (IsRecoveredImmediateRelocationPlan(plan))
        {
            result = ExecuteRecoveredImmediateRelocationPlan(session, plan);
            return true;
        }

        return false;
    }

    private static bool IsRecoveredSimpleHealerPlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        var opcodes = plan.Operations.Select(op => op.Opcode).ToArray();
        if (opcodes.Length != 4 ||
            !opcodes[0].Equals("monsters", StringComparison.OrdinalIgnoreCase) ||
            !opcodes[1].Equals("price", StringComparison.OrdinalIgnoreCase) ||
            !opcodes[2].Equals("cast", StringComparison.OrdinalIgnoreCase) ||
            !opcodes[3].Equals("adddelay", StringComparison.OrdinalIgnoreCase)) return false;
        var spellId = plan.Operations[2].SpellId ?? plan.Operations[2].NumericArguments.FirstOrDefault();
        return spellId is >= 219 and <= 224;
    }

    private string ExecuteRecoveredSimpleHealerPlan(RuntimeSession session, RuntimeClassicTextblockExecutionPlan plan)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
            throw new InvalidOperationException("A persistent character is required for a healer service.");

        // The native `monsters` guard proves that the room's healer/NPC must be present. Its no-healer
        // failure presentation is not encoded on these lines, so fail closed with no invented prose.
        if (!LivingNpcsInRoom(session).Any(npc => npc.Alive)) return string.Empty;

        var priceOp = plan.Operations.Single(op => op.Opcode.Equals("price", StringComparison.OrdinalIgnoreCase));
        var castOp = plan.Operations.Single(op => op.Opcode.Equals("cast", StringComparison.OrdinalIgnoreCase));
        var priceCopper = priceOp.NumericArguments.FirstOrDefault();
        var insufficientFundsMessageId = priceOp.NumericArguments.Skip(1).FirstOrDefault();
        var spellId = castOp.SpellId ?? castOp.NumericArguments.FirstOrDefault();
        if (priceCopper < 0 || spellId is < 219 or > 224)
            throw new InvalidOperationException("Decoded healer transaction is invalid.");

        var before = _characters.LoadCharacter(actor.Id)
            ?? throw new InvalidOperationException("Character save was not found for the healer service.");
        var beforeState = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0,
            new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        if (RuntimeBundleCInventoryAuthority.ReadCurrency(beforeState).CopperValue < priceCopper)
            return insufficientFundsMessageId > 0 ? _nativePayloads.RequireMessage(insufficientFundsMessageId) : string.Empty;

        EnsureLivePowers(session);
        var liveBefore = _liveSpellEngine!.Snapshot();
        try
        {
            ApplyRecoveredHealerSpell(session, actor.Id, spellId);

            // Debit after the effect so remove-curse/item mutations are preserved, but affordability is
            // validated before any effect. Rollback restores both character and live-spell state.
            var afterEffect = _characters.LoadCharacter(actor.Id) ?? before;
            var afterState = afterEffect.ItemState ?? beforeState;
            var debited = RuntimeBundleCEconomyAuthority.DebitCopperValue(afterState, priceCopper);
            _characters.SaveCharacter(afterEffect with { ItemState = debited, UpdatedAtUtc = DateTimeOffset.UtcNow },
                $"classic-healer:textblock.{plan.RootTextblockId}:spell.{spellId}");
            PersistLivePowers(session.World.Definition.Id);

            // Exact healer success/observer presentation is not recovered for this room-script path.
            // Do not reuse player-cast observer prose with the player incorrectly acting as the healer.
            return string.Empty;
        }
        catch
        {
            _liveSpellEngine.RestoreSnapshot(liveBefore);
            _characters.SaveCharacter(before, $"classic-healer-rollback:textblock.{plan.RootTextblockId}:spell.{spellId}");
            PersistLivePowers(session.World.Definition.Id);
            throw;
        }
    }

    private void ApplyRecoveredHealerSpell(RuntimeSession session, string characterId, int legacySpellId)
    {
        var spell = _liveSpellCatalog!.Require($"spell.{legacySpellId}");
        var target = new RuntimeSpellTarget("character", characterId);
        var now = DateTimeOffset.UtcNow;

        if (legacySpellId is >= 219 and <= 221)
        {
            // These healer spells are non-hostile, level-0 generated spells. Reuse the same native-data
            // spell magnitude resolver used by live spellcasting rather than creating a healer formula.
            var resolution = RuntimeClassicPlayerSpellCombat.Resolve(spell, 0, false, false, false, Random.Shared, 0);
            var character = _characters.LoadCharacter(characterId)
                ?? throw new InvalidOperationException("Healer target character was not found.");
            var healed = Math.Clamp(checked(character.HitPoints + resolution.Magnitude), 0, character.MaximumHitPoints);
            _characters.SaveCharacter(character with { HitPoints = healed, UpdatedAtUtc = now },
                $"classic-healer-spell:{legacySpellId}");
            return;
        }

        if (legacySpellId is 222 or 223)
        {
            _ = ApplyDecodedCureAndDispel(session, spell, target);
            SynchronizeCharacterSpellStatuses(target, now, $"classic-healer-spell:{legacySpellId}");
            return;
        }

        if (legacySpellId == 224)
        {
            _ = ApplyDecodedRemoveCurse(session, target, now);
            return;
        }

        throw new InvalidOperationException($"Unsupported recovered healer spell {legacySpellId}.");
    }

    private static bool IsRecoveredImmediateRelocationPlan(RuntimeClassicTextblockExecutionPlan plan)
    {
        if (plan.Operations.Count == 0 || plan.Operations.Count(op => op.Opcode.Equals("teleport", StringComparison.OrdinalIgnoreCase)) != 1)
            return false;
        return plan.Operations.All(op => op.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase)
            || op.Opcode.Equals("text", StringComparison.OrdinalIgnoreCase)
            || op.Opcode.Equals("teleport", StringComparison.OrdinalIgnoreCase));
    }

    private string ExecuteRecoveredImmediateRelocationPlan(RuntimeSession session, RuntimeClassicTextblockExecutionPlan plan)
    {
        if (session.Actor is not RuntimeCharacterActor)
            throw new InvalidOperationException("A persistent character is required for a decoded room relocation.");

        var output = new List<string>();
        foreach (var operation in plan.Operations)
        {
            if (operation.Opcode.Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                var messageId = operation.NumericArguments.FirstOrDefault();
                if (messageId > 0)
                {
                    var payload = _nativePayloads.RequireMessage(messageId);
                    if (!string.IsNullOrWhiteSpace(payload)) output.Add(payload);
                }
                continue;
            }
            if (operation.Opcode.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                var textblockId = operation.NumericArguments.FirstOrDefault();
                if (textblockId > 0)
                {
                    var payload = _nativePayloads.RequireTextblock(textblockId);
                    if (!string.IsNullOrWhiteSpace(payload)) output.Add(payload);
                }
                continue;
            }
            if (!operation.Opcode.Equals("teleport", StringComparison.OrdinalIgnoreCase)) continue;

            var roomNumber = operation.RoomNumber ?? operation.NumericArguments.FirstOrDefault();
            var mapNumber = operation.MapNumber ?? operation.NumericArguments.Skip(1).FirstOrDefault();
            var destination = session.World.FindRoom(mapNumber, roomNumber)
                ?? throw new InvalidOperationException($"Decoded room relocation destination {mapNumber}/{roomNumber} is unavailable.");
            session.MoveToWorld(session.World, destination, $"classic-room-textblock:{plan.RootTextblockId}");
            _characters.SaveSession(session);
            AdmitClassicRoomEntryNpcRespawns(session);
        }
        return string.Join(Environment.NewLine, output);
    }
}
