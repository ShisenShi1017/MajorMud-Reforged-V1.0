using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeCharacterDecodeBundleAuditResult(bool Passed, string WorldId, int RaceCount, int ClassCount, IReadOnlyList<string> ConnectedFields, IReadOnlyList<string> Diagnostics)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeCharacterDecodeBundleAudit
{
    public static RuntimeCharacterDecodeBundleAuditResult Run(string projectRoot, string worldId)
    {
        var connected = new List<string>();
        var diagnostics = new List<string>();
        var bundle = RuntimeCharacterBundleCatalog.Load(projectRoot, worldId);
        var fieldLedger = RuntimeCharacterBundleFieldLedger.Build(bundle);
        Check(fieldLedger.Count >= 15, "bundle_a.field_ownership_ledger.minimum_coverage");
        Check(fieldLedger.All(x => !string.IsNullOrWhiteSpace(x.Owner) && !string.IsNullOrWhiteSpace(x.Consumer) && !string.IsNullOrWhiteSpace(x.Evidence)), "bundle_a.field_ownership_ledger.provenance_complete");
        Check(fieldLedger.Count(x => x.Status == RuntimeCharacterFieldOwnershipStatus.CrossBundleHandoff) == 6, "bundle_a.field_ownership_ledger.explicit_cross_bundle_handoffs");
        Check(fieldLedger.All(x => x.Status != RuntimeCharacterFieldOwnershipStatus.ExplicitlyUnresolved || x.FieldGroup == "alignment_class_reward_matrix"), "bundle_a.field_ownership_ledger.no_untracked_unresolved_groups");
        connected.AddRange(fieldLedger.Where(x => x.Status == RuntimeCharacterFieldOwnershipStatus.Authoritative).Select(x => $"bundle_a.owner.{x.FieldGroup}"));
        connected.AddRange(fieldLedger.Where(x => x.Status == RuntimeCharacterFieldOwnershipStatus.CrossBundleHandoff).Select(x => $"bundle_a.handoff.{x.FieldGroup}->{x.Consumer}"));
        var catalog = bundle.Characters;
        var bundleAFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "character_foundation", "classes", "races", "skills", "abilities"
        };
        var repositoryCoverage = ClassicDecodeExhaustionAudit.Run(projectRoot);
        var disconnectedBundleAFields = repositoryCoverage.Fields
            .Where(x => bundleAFamilies.Contains(x.Family) &&
                        x.Status == "generated-unconsumed" &&
                        !IsBundleARepositoryCoverageMetadata(x))
            .ToArray();
        var evidenceNameOnlyWarnings = repositoryCoverage.Fields
            .Count(x => bundleAFamilies.Contains(x.Family) && x.Status == "consumed-without-field-evidence");
        Check(disconnectedBundleAFields.Length == 0, "bundle_a.repository_decode_coverage.zero_disconnected_fields");
        connected.Add($"bundle_a.repository_decode_coverage.evidence_name_only_warnings:{evidenceNameOnlyWarnings}");
        connected.Add("bundle_a.repository_decode_coverage.character_foundation_classes_races_skills_abilities");
        var learning = bundle.Learning;
        Check(learning.Level40QuestCandidates.Count == 3, "jungle_gnome.textblock2798.three_class_scroll_branches");
        Check(learning.Level40QuestCandidates.All(x => x.NpcId == "npc.716" && x.TurnInTextblockId == 2798 && x.TurnInScriptResolved), "jungle_gnome.textblock2798.activation_root_connected");
        Check(learning.Level40QuestCandidates.All(x => x.RequiredItemIds.Count == 4 && x.UnresolvedItemNames.Count == 0), "jungle_gnome.all_required_items_canonical");
        Check(learning.Level40QuestCandidates.All(x => learning.ResolveSpellItem(x.RewardItemId).Any(y => y.SpellId.Equals(x.RewardSpellId, StringComparison.OrdinalIgnoreCase))), "jungle_gnome.reward_scroll_learnsp_relationships");
        connected.Add("runtime.character.jungle_gnome_textblock2798_scroll_turnin_authority");
        var classCapabilities = bundle.ClassCapabilities;
        var questAbilities = bundle.QuestAbilities;
        var questFlagProbe = new RuntimeCharacterRecord(
            CharacterId: "audit.quest-flags", AccountId: "audit", DisplayName: "Audit", WorldId: worldId,
            RaceId: "race.1", RaceName: "Audit Race", ClassId: "class.1", ClassName: "Warrior",
            Level: 50, Experience: 0, HitPoints: 1, MaximumHitPoints: 1, Mana: 0, MaximumMana: 0,
            RoomId: "room.1", RoomNumber: 1, BaseStats: new Dictionary<string, int>(), Skills: new Dictionary<string, int>(),
            Spells: Array.Empty<string>(), Inventory: Array.Empty<string>(), Equipment: new Dictionary<string, string?>(),
            Conditions: new Dictionary<string, string>(), CreatedAtUtc: DateTimeOffset.UtcNow, UpdatedAtUtc: DateTimeOffset.UtcNow);
        foreach (var reward in new[] { (2,1), (4,1), (22,3), (27,2), (34,1), (57,1), (58,1), (69,10), (70,2), (117,10), (118,10) })
            questFlagProbe = questAbilities.ApplyGrant(questFlagProbe, reward.Item1, reward.Item2, "mudinfo.quest-flags-list");
        var questFlagBonuses = questAbilities.ResolveDerivedBonuses(questFlagProbe);
        Check(questFlagBonuses.ArmourClassBonus == 1 && questFlagBonuses.MaximumDamageBonus == 1, "quest_flags.ac_and_max_damage_semantics");
        Check(questFlagBonuses.AccuracyPercentBonus == 3 && questFlagBonuses.DodgeBonus == 1 && questFlagBonuses.CriticalBonus == 1, "quest_flags.accuracy_dodge_critical_semantics");
        Check(questFlagBonuses.StealthBonus == 2 && questFlagBonuses.MaximumManaBonus == 10 && questFlagBonuses.SpellcastingBonus == 2, "quest_flags.stealth_mana_spellcasting_semantics");
        Check(questFlagBonuses.BackstabMinimumBonus == 10 && questFlagBonuses.BackstabMaximumBonus == 10 && questFlagBonuses.SeeHiddenRank == 1, "quest_flags.backstab_and_see_hidden_semantics");
        connected.Add("runtime.character.quest_flag_semantic_projection_from_uploaded_evidence");
        var worldRewards = bundle.WorldRewards;
        var alignmentRewards = bundle.AlignmentRewards;
        var alignmentGates = bundle.AlignmentGates;
        var alignmentPayloads = bundle.AlignmentPayloads;
        var alignmentNpcs = bundle.AlignmentNpcs;
        var alignmentMatrix = bundle.AlignmentMatrix;
        var alignmentFullRewards = bundle.AlignmentFullRewards;
        var alignmentCorroboration = bundle.AlignmentCorroboration;
        Check(alignmentCorroboration.LevelGates.Select(x => x.MinimumLevel).SequenceEqual(new[] { 10, 20, 30, 40, 50 }), "alignment_quest_progression.sequential_level_gates_10_20_30_40_50");
        Check(alignmentCorroboration.SecondQuestClassRewards.Count == 15, "alignment_quest_second_reward.all_classes_class_dependent");
        Check(alignmentCorroboration.SecondQuestClassRewards.All(x => x.QuestOrdinal == 2 && x.MinimumLevel == 20 && x.StatChanges.Count > 0), "alignment_quest_second_reward.level20_stat_packages");
        Check(alignmentCorroboration.RequireSecondQuestReward(1).StatChanges.TryGetValue("max_damage", out var warriorDamage) && warriorDamage == 1, "alignment_quest_second_reward.warrior_max_damage");
        Check(alignmentCorroboration.RequireSecondQuestReward(5).StatChanges.TryGetValue("spellcasting", out var priestSpellcasting) && priestSpellcasting == 1, "alignment_quest_second_reward.priest_spellcasting");
        Check(alignmentCorroboration.RequireSecondQuestReward(14).StatChanges.TryGetValue("stealth", out var rangerStealth) && rangerStealth == 2, "alignment_quest_second_reward.ranger_stealth");
        connected.Add("runtime.character.corroborated_sequential_alignment_level_gates_and_class_stat_matrix");
        Check(alignmentNpcs.Quests.Count == 3, "alignment_npc_command_maps.good_neutral_evil_count");
        var goodNpc = alignmentNpcs.Require("Good");
        Check(goodNpc.NpcId == "npc.244" && goodNpc.NpcName == "Chancellor Annotated" && goodNpc.RootTextblockId == 335 && goodNpc.QuestAbilityId == 126, "alignment_npc_command_maps.chancellor_annotated_textblock335");
        Check(goodNpc.Commands.Any(x => x.Keyword == "missions" && x.TargetTextblockId == 518), "alignment_npc_command_maps.chancellor_missions_entry");
        Check(goodNpc.Commands.Any(x => x.Keyword == "knowledge" && x.TargetTextblockId == 9459), "alignment_npc_command_maps.chancellor_level50_entry");
        Check(goodNpc.Commands.Any(x => x.Keyword == "cloak" && x.TargetTextblockId == 4309), "alignment_npc_command_maps.chancellor_cloak_entry");
        Check(goodNpc.Commands.Any(x => x.Keyword == "reconcile" && x.TargetTextblockId == 4315), "alignment_npc_command_maps.chancellor_reconcile_entry");
        var neutralNpc = alignmentNpcs.Require("Neutral");
        Check(neutralNpc.NpcId == "npc.251" && neutralNpc.NpcName == "Grey Lord" && neutralNpc.RootTextblockId == 366 && neutralNpc.QuestAbilityId == 127, "alignment_npc_command_maps.grey_lord_textblock366");
        Check(neutralNpc.Commands.Any(x => x.Keyword == "missions" && x.TargetTextblockId == 505), "alignment_npc_command_maps.grey_lord_missions_entry");
        Check(neutralNpc.Commands.Any(x => x.Keyword == "reward" && x.TargetTextblockId == 1284), "alignment_npc_command_maps.grey_lord_reward_entry");
        Check(neutralNpc.Commands.Any(x => x.Keyword == "knowledge" && x.TargetTextblockId == 9473), "alignment_npc_command_maps.grey_lord_level50_entry");
        Check(neutralNpc.Commands.Any(x => x.Keyword == "cloak" && x.TargetTextblockId == 4311), "alignment_npc_command_maps.grey_lord_cloak_entry");
        var evilNpc = alignmentNpcs.Require("Evil");
        Check(evilNpc.NpcId == "npc.263" && evilNpc.NpcName == "Balthazar" && evilNpc.RootTextblockId == 428 && evilNpc.QuestAbilityId == 128, "alignment_npc_command_maps.balthazar_textblock428");
        Check(evilNpc.Commands.Any(x => x.Keyword == "work" && x.TargetTextblockId == 430), "alignment_npc_command_maps.balthazar_work_entry");
        Check(evilNpc.Commands.Any(x => x.Keyword == "reward" && x.TargetTextblockId == 436), "alignment_npc_command_maps.balthazar_reward_entry");
        Check(evilNpc.Commands.Any(x => x.Keyword == "knowledge" && x.TargetTextblockId == 9488), "alignment_npc_command_maps.balthazar_level50_entry");
        Check(evilNpc.Commands.Any(x => x.Keyword == "cloak" && x.TargetTextblockId == 4313), "alignment_npc_command_maps.balthazar_cloak_entry");
        Check(goodNpc.Commands.SelectMany(x => x.MutationOpcodes).Contains("giveability", StringComparer.OrdinalIgnoreCase), "alignment_npc_command_maps.good_character_mutation_path");
        Check(neutralNpc.Commands.SelectMany(x => x.MutationOpcodes).Contains("giveability", StringComparer.OrdinalIgnoreCase), "alignment_npc_command_maps.neutral_character_mutation_path");
        Check(evilNpc.Commands.SelectMany(x => x.MutationOpcodes).Contains("giveability", StringComparer.OrdinalIgnoreCase), "alignment_npc_command_maps.evil_character_mutation_path");
        connected.Add("runtime.character.decoded_good_neutral_evil_npc_command_activation_maps");
        Check(alignmentFullRewards.Graphs.Count > 0, "alignment_full_reward_graph.graphs_projected");
        Check(alignmentFullRewards.Graphs.Any(x => x.AlignmentPath == "Good" && x.RootTextblockId == 335), "alignment_full_reward_graph.good_verified_from_textblock335");
        Check(alignmentFullRewards.Graphs.Any(x => x.AlignmentPath == "Neutral" && x.RootTextblockId == 366), "alignment_full_reward_graph.neutral_verified_from_textblock366");
        Check(alignmentFullRewards.Graphs.Any(x => x.AlignmentPath == "Evil" && x.RootTextblockId == 428), "alignment_full_reward_graph.evil_verified_from_textblock428");
        Check(alignmentFullRewards.Effects.Any(x => x.AlignmentPath == "Good" && x.Opcode.Equals("giveitem", StringComparison.OrdinalIgnoreCase)), "alignment_full_reward_graph.good_item_rewards");
        Check(alignmentFullRewards.Effects.Any(x => x.AlignmentPath == "Neutral" && (x.Opcode.Equals("giveitem", StringComparison.OrdinalIgnoreCase) || x.Opcode.Equals("giveability", StringComparison.OrdinalIgnoreCase))), "alignment_full_reward_graph.neutral_rewards");
        Check(alignmentFullRewards.Effects.Any(x => x.AlignmentPath == "Evil" && (x.Opcode.Equals("giveitem", StringComparison.OrdinalIgnoreCase) || x.Opcode.Equals("giveability", StringComparison.OrdinalIgnoreCase))), "alignment_full_reward_graph.evil_rewards");
        Check(alignmentFullRewards.Graphs.Where(x => x.CommandKeyword == "cloak").All(x => x.ReachableTextblockIds.Count > 1), "alignment_full_reward_graph.cloak_owned_child_traversal");
        Check(alignmentFullRewards.Graphs.Where(x => x.CommandKeyword == "knowledge").All(x => x.ReachableTextblockIds.Count > 1), "alignment_full_reward_graph.knowledge_random_child_traversal");
        Check(alignmentFullRewards.Effects.All(x => !string.IsNullOrWhiteSpace(x.DecodedSource) && x.EvidenceStatus == "verified"), "alignment_full_reward_graph.effect_provenance_complete");
        connected.Add("runtime.character.full_good_neutral_evil_alignment_reward_operation_graphs");
        Check(alignmentMatrix.Cells.Count == 225, "alignment_reward_matrix.three_paths_five_levels_fifteen_classes");
        Check(alignmentMatrix.Cells.GroupBy(x => (x.AlignmentPath, x.QuestLevel)).All(group => group.Count() == 15), "alignment_reward_matrix.every_path_level_has_all_classes");
        Check(alignmentMatrix.Cells.Where(x => x.AlignmentPath == "Good" && x.QuestLevel == 40).All(x => x.RewardPayloadResolved && x.GrantedItemIds.Count == 1), "alignment_reward_matrix.good_level40_all_class_cloaks_resolved");
        Check(alignmentMatrix.Cells.Where(x => (x.AlignmentPath == "Neutral" || x.AlignmentPath == "Evil") && x.QuestLevel is 20 or 30 or 40 or 50).All(x => !x.RewardPayloadResolved || x.EvidenceStatus == "verified"), "alignment_reward_matrix.neutral_evil_unresolved_cells_explicit_fail_closed");
        Check(alignmentMatrix.UnresolvedCells.All(x => !string.IsNullOrWhiteSpace(x.DecodedSource)), "alignment_reward_matrix.unresolved_provenance_retained");
        connected.Add("runtime.character.legacy_alignment_matrix_plus_full_decoded_reward_graph");
        var witchunterQuest = bundle.WitchunterQuest;
        var itemRequirements = bundle.ItemRequirements;
        Check(catalog.CreationContract.AllRaceClassCombinationsAllowed, "character_creation_rules.all_race_class_combinations_allowed");
        Check(catalog.CreationContract.StartingRoomNumber > 0, "character_creation_rules.new_character_start");
        Check(!string.IsNullOrWhiteSpace(catalog.CreationContract.StartingAbilitiesRule), "character_creation_rules.starting_abilities_rule");
        Check(!string.IsNullOrWhiteSpace(catalog.CreationContract.StartingStatsRule), "character_creation_rules.starting_stats_rule");
        Check(!string.IsNullOrWhiteSpace(catalog.ExperienceRule.CombinedPercentFormula), "xp_rule.combined_percent_formula");
        Check(!string.IsNullOrWhiteSpace(catalog.DerivedFormulaContract.MaximumHitPointsFormula), "derived_stat_formulas.maximum_hit_points.formula");
        Check(!string.IsNullOrWhiteSpace(catalog.DerivedFormulaContract.MaximumManaCasterFormula), "derived_stat_formulas.maximum_mana.magic_type_1_to_4");
        Check(catalog.DerivedFormulaContract.SpellcastingTypes.Count == 5, "derived_stat_formulas.spellcasting.types");
        Check(catalog.AlignmentRuleContract.AttackReputationPenalties.Count >= 8, "character_creation_rules.alignment_evil_point_rules.attack_reputation_penalties");
        Check(catalog.AlignmentRuleContract.FiendAttackLockoutThreshold == 300, "character_creation_rules.alignment_evil_point_rules.fiend_attack_lockout.threshold");

        Check(learning.Skills.Count == 13, "skills.records");
        Check(learning.Spells.Count > 0, "spells.records");
        Check(learning.Skills.All(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Classification)), "skills[].id/classification");
        Check(learning.Spells.All(x => !string.IsNullOrWhiteSpace(x.Id)), "spells[].id");
        Check(learning.Skills.All(x => !x.ExecutableLearningRelationship), "skills[].learning_relationship_fail_closed");
        connected.Add("runtime.character.persisted_skill_spell_identity_validation");
        Check(learning.SpellAcquisitionSources.Any(x => x.Channel == "spell_shop_scroll"), "spell_acquisition.spell_shop_scroll_sources");
        Check(learning.SpellAcquisitionSources.Any(x => x.Channel == "level_40_class_quest" && x.MinimumLevel == 0), "spell_acquisition.level40_scroll_receipt_not_level_gated");
        Check(learning.SpellAcquisitionSources.Any(x => x.Channel == "level_50_alignment_quest" && x.MinimumLevel == 50), "spell_acquisition.level50_alignment_quest");
        Check(learning.SpellAcquisitionSources.Where(x => x.Channel == "spell_shop_scroll").All(x => x.OfferedItemIds.Count > 0), "spell_shop.inventory_scroll_projection");
        Check(learning.SpellItemRelationships.Count > 0, "spell_shop.scroll_to_spell_decoded_learnsp_relationships");
        Check(learning.SpellItemRelationships.All(x => learning.IsKnownSpell(x.SpellId)), "spell_shop.scroll_to_spell_canonical_identity_validation");
        connected.Add("runtime.character.authoritative_spell_grant_persistence_consumer");
        connected.Add("runtime.character.verified_learnsp_item_to_spell_projection");
        connected.Add("runtime.character.live_use_learnsp_item_consumer");
        Check(learning.SpellItemRelationships.GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1), "spell_learning.multi_spell_item_relationship_preservation");
        Check(learning.QuestSpellReferences.Count > 0, "spell_acquisition.quest_spell_reference_projection");
        Check(learning.QuestSpellReferences.All(x => learning.IsKnownSpell(x.SpellId)), "spell_acquisition.quest_spell_reference_canonical_validation");
        Check(learning.QuestSpellReferences.Where(x => x.Opcode.Equals("cast", StringComparison.OrdinalIgnoreCase)).All(x => !x.GrantsPermanentOwnership), "spell_acquisition.quest_cast_not_misclassified_as_learning");
        connected.Add("runtime.character.quest_spell_reference_fail_closed_classification");
        Check(learning.Level40QuestCandidates.Count == 3, "spell_acquisition.level40_gnome_merchant_class_branches");
        Check(learning.Level40QuestCandidates.All(x => x.NpcId == "npc.716" && x.LocationName.Equals("Shallow Jungle", StringComparison.OrdinalIgnoreCase)), "spell_acquisition.level40_gnome_merchant_location");
        Check(learning.Level40QuestCandidates.All(x => learning.IsKnownSpell(x.RewardSpellId)), "spell_acquisition.level40_reward_spell_canonical_validation");
        Check(learning.Level40QuestCandidates.All(x => !string.IsNullOrWhiteSpace(x.RewardItemId) && learning.ResolveSpellItem(x.RewardItemId).Any(r => r.SpellId.Equals(x.RewardSpellId, StringComparison.OrdinalIgnoreCase))), "spell_acquisition.level40_reward_scroll_learnsp_relationship");
        Check(learning.Level40QuestCandidates.All(x => !x.RewardItemUseLevelResolved && x.RewardItemMinimumUseLevel == 0), "spell_acquisition.level40_scroll_use_level_fail_closed_until_decoded_field_promoted");
        Check(learning.Level40QuestCandidates.Select(x => x.ClassName).OrderBy(x => x).SequenceEqual(new[] { "Druid", "Mage", "Priest" }), "spell_acquisition.level40_supported_class_set");
        Check(learning.Level40QuestCandidates.All(x => x.RequiredItemNames.Count == 4), "spell_acquisition.level40_component_sets");
        Check(learning.Level40QuestCandidates.All(x => x.UnresolvedItemNames.Count == 0), "spell_acquisition.level40_component_identity_resolution");
        Check(learning.Level40QuestCandidates.Where(x => x.ClassName.Equals("Mage", StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.RequiredItemIds).Contains("item.479", StringComparer.OrdinalIgnoreCase), "spell_acquisition.level40_mage_cyclops_eye_alias");
        Check(learning.Level40QuestCandidates.All(x => x.TurnInScriptResolved), "spell_acquisition.level40_turnin_textblock2798_connected");
        connected.Add("runtime.character.level40_quest_evidence_projection");
        Check(learning.Level40QuestCandidates.All(x => x.RequiredItemIds.Count == 4), "spell_acquisition.level40_atomic_component_requirements");
        connected.Add("runtime.character.level40_atomic_scroll_turnin_consumer_evidence_gated");
        Check(learning.Level50AlignmentQuestStages.Count == 3, "spell_acquisition.level50_alignment_stage_count");
        Check(learning.Level50AlignmentQuestStages.Select(x => x.AlignmentPath).OrderBy(x => x).SequenceEqual(new[] { "Evil", "Good", "Neutral" }), "spell_acquisition.level50_alignment_paths");
        Check(learning.Level50AlignmentQuestStages.All(x => x.MinimumLevel == 50 && x.QuestAbilityId is 126 or 127 or 128 && x.RequiredStageValue > 0), "spell_acquisition.level50_decoded_stage_gates");
        Check(learning.Level50AlignmentQuestStages.All(x => x.SourceTextblockId > 0 && x.RewardTextblockId > 0 && !string.IsNullOrWhiteSpace(x.NpcId)), "spell_acquisition.level50_npc_textblock_relationships");
        Check(learning.Level50AlignmentQuestStages.All(x => !x.PermanentSpellGrantResolved), "spell_acquisition.level50_reward_fail_closed_until_decoded");
        connected.Add("runtime.character.level50_alignment_stage_projection");
        Check(alignmentGates.Transitions.Count >= 5, "alignment_quest.decoded_level_stage_transition_projection");
        Check(alignmentGates.TryGetTransition("Good", 10, out var good10) && good10.QuestAbilityId == 126 && good10.GrantedStageValue == 4, "alignment_quest.good_level10_stage_gate");
        Check(alignmentGates.TryGetTransition("Good", 20, out var good20) && good20.GrantedStageValue == 7, "alignment_quest.good_level20_stage_gate");
        Check(alignmentGates.TryGetTransition("Good", 30, out var good30) && good30.GrantedStageValue == 10, "alignment_quest.good_level30_stage_gate");
        Check(alignmentGates.TryGetTransition("Neutral", 10, out var neutral10) && neutral10.QuestAbilityId == 127 && neutral10.GrantedStageValue == 7, "alignment_quest.neutral_level10_stage_gate");
        Check(alignmentGates.TryGetTransition("Evil", 10, out var evil10) && evil10.QuestAbilityId == 128 && evil10.GrantedStageValue == 3, "alignment_quest.evil_level10_stage_gate");
        Check(neutral10.GrantedItemIds.Contains("item.645", StringComparer.OrdinalIgnoreCase) && neutral10.ExperienceReward == 150000, "alignment_quest.neutral_level10_reward_projection");
        Check(evil10.RequiredItemIds.Contains("item.621", StringComparer.OrdinalIgnoreCase) && evil10.RequiredItemIds.Contains("item.622", StringComparer.OrdinalIgnoreCase) && evil10.GrantedItemIds.Contains("item.651", StringComparer.OrdinalIgnoreCase) && evil10.ExperienceReward == 150000 && evil10.EvilPointChange == 30, "alignment_quest.evil_level10_reward_projection");
        Check(alignmentGates.Branches.Count >= 15, "alignment_quest.decoded_level_branch_projection");
        Check(alignmentGates.ForPathAtLevel("Good", 20).Any(x => x.RequiredStageValue == 6 && x.TargetTextblockId == 496), "alignment_quest.good_level20_branch");
        Check(alignmentGates.ForPathAtLevel("Good", 30).Any(x => x.RequiredStageValue == 15 && x.TargetTextblockId == 1095), "alignment_quest.good_level30_branch");
        Check(alignmentGates.ForPathAtLevel("Good", 40).Any(x => x.RequiredStageValue == 17 && x.TargetTextblockId == 2844), "alignment_quest.good_level40_branch");
        Check(alignmentGates.ForPathAtLevel("Good", 50).Any(x => x.RequiredStageValue == 19 && x.TargetTextblockId == 9472), "alignment_quest.good_level50_branch");
        Check(alignmentGates.ForPathAtLevel("Neutral", 20).Any(x => x.RequiredStageValue == 7 && x.TargetTextblockId == 508), "alignment_quest.neutral_level20_branch");
        Check(alignmentGates.ForPathAtLevel("Neutral", 30).Any(x => x.RequiredStageValue == 8 && x.TargetTextblockId == 1259), "alignment_quest.neutral_level30_branch");
        Check(alignmentGates.ForPathAtLevel("Neutral", 40).Any(x => x.RequiredStageValue == 11 && x.TargetTextblockId == 2845), "alignment_quest.neutral_level40_branch");
        Check(alignmentGates.ForPathAtLevel("Neutral", 50).Any(x => x.RequiredStageValue == 13 && x.TargetTextblockId == 9486), "alignment_quest.neutral_level50_branch");
        Check(alignmentGates.ForPathAtLevel("Evil", 20).Any(x => x.RequiredStageValue == 3 && x.TargetTextblockId == 439), "alignment_quest.evil_level20_branch");
        Check(alignmentGates.ForPathAtLevel("Evil", 30).Any(x => x.RequiredStageValue == 4 && x.TargetTextblockId == 1287), "alignment_quest.evil_level30_branch");
        Check(alignmentGates.ForPathAtLevel("Evil", 40).Any(x => x.RequiredStageValue == 12 && x.TargetTextblockId == 2848), "alignment_quest.evil_level40_branch");
        Check(alignmentGates.ForPathAtLevel("Evil", 50).Any(x => x.RequiredStageValue == 14 && x.TargetTextblockId == 9487), "alignment_quest.evil_level50_branch");
        connected.Add("runtime.character.decoded_alignment_level_stage_gate_authority");
        connected.Add("runtime.character.good_neutral_evil_level20_30_40_50_branch_authority");
        Check(alignmentPayloads.Payloads.Count >= 40, "alignment_quest.complete_stage_payload_projection");
        Check(alignmentPayloads.ForPath("Good").Count > 0 && alignmentPayloads.ForPath("Neutral").Count > 0 && alignmentPayloads.ForPath("Evil").Count > 0, "alignment_quest.all_paths_payload_projection");
        Check(alignmentPayloads.Payloads.Any(x => x.GrantedItemIds.Count > 0), "alignment_quest.item_reward_payloads");
        Check(alignmentPayloads.Payloads.Any(x => x.RequiredItemIds.Count > 0 || x.RemovedItemIds.Count > 0), "alignment_quest.item_turnin_payloads");
        Check(alignmentPayloads.Payloads.Any(x => x.ExperienceReward > 0), "alignment_quest.experience_reward_payloads");
        Check(alignmentPayloads.Payloads.Any(x => x.EvilPointChange != 0), "alignment_quest.evil_point_payloads");
        Check(alignmentPayloads.FromStage("Good", 30).Any(x => x.GrantedStageValue == 31 && x.ExperienceReward == 158010000), "alignment_quest.good_final_stage_payload");
        Check(alignmentPayloads.FromStage("Neutral", 30).Any(x => x.GrantedStageValue == 31 && x.ExperienceReward == 158010000), "alignment_quest.neutral_final_stage_payload");
        Check(alignmentPayloads.FromStage("Evil", 30).Any(x => x.GrantedStageValue == 31 && x.ExperienceReward == 158010000), "alignment_quest.evil_final_stage_payload");
        connected.Add("runtime.character.complete_alignment_stage_payload_authority");
        Check(alignmentRewards.ItemRewards.Count == 15, "alignment_quest.class_specific_cloak_reward_count");
        Check(alignmentRewards.ItemRewards.All(x => x.AlignmentPath == "Good" && x.QuestAbilityId == 126 && x.RequiredStageValue == 19), "alignment_quest.good_stage19_cloak_gate");
        Check(alignmentRewards.ItemRewards.Select(x => x.ClassLegacyNumber).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, 15)), "alignment_quest.all_class_cloak_branches");
        Check(alignmentRewards.ItemRewards.All(x => x.ItemId.StartsWith("item.", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.ItemName)), "alignment_quest.cloak_item_identity_resolution");
        Check(alignmentRewards.ItemRewards.All(x => x.SourceTextblockId == 4310 && x.MessageTextblockId == 2067), "alignment_quest.cloak_textblock_message_provenance");
        var witchunterCloak = alignmentRewards.RequireItemReward("Good", 2);
        Check(witchunterCloak.ClassName.Equals("Witchunter", StringComparison.OrdinalIgnoreCase) && witchunterCloak.ItemId == "item.1310" && witchunterCloak.ItemName.Equals("inquisitor's cloak", StringComparison.OrdinalIgnoreCase), "alignment_quest.witchunter_inquisitors_cloak_mapping");
        var probe = new RuntimeCharacterRecord(
            CharacterId: "audit.quest-ability", AccountId: "audit", DisplayName: "Audit", WorldId: worldId,
            RaceId: "race.1", RaceName: "Audit Race", ClassId: "class.1", ClassName: "Audit Class",
            Level: 50, Experience: 0, HitPoints: 1, MaximumHitPoints: 1, Mana: 0, MaximumMana: 0,
            RoomId: "room.1", RoomNumber: 1, BaseStats: new Dictionary<string, int>(), Skills: new Dictionary<string, int>(),
            Spells: Array.Empty<string>(), Inventory: Array.Empty<string>(), Equipment: new Dictionary<string, string?>(),
            Conditions: new Dictionary<string, string>(), CreatedAtUtc: DateTimeOffset.UtcNow, UpdatedAtUtc: DateTimeOffset.UtcNow);
        var alignmentRewardProbe = questAbilities.ApplyGrant(probe, 126, 19, "audit:good-fourth-alignment-complete");
        Check(alignmentRewards.EvaluateItemEligibility(alignmentRewardProbe, questAbilities, "Good", 1).Eligible, "runtime.character.alignment_item_entitlement_consumer");
        connected.Add("runtime.character.decoded_class_specific_alignment_cloak_entitlements");
        Check(witchunterQuest.Steps.Count > 0, "witchunter.magebanequest.decoded_steps");
        var witchunterStart = witchunterQuest.Steps.Single(x => x.StageValue == 1 && x.MinimumLevel == 15 && x.GrantedItemIds.Contains("item.646", StringComparer.OrdinalIgnoreCase));
        Check(witchunterStart.NpcId == "npc.257" && witchunterStart.SourceTextblockId == 399, "witchunter.magebanequest.level15_start_npc_textblock");
        var witchunterProbe = probe with { ClassId = "class.2", ClassName = "Witchunter", Level = 15 };
        Check(witchunterQuest.EvaluateStart(witchunterProbe).Eligible, "runtime.character.witchunter_level15_first_half_gate");
        Check(!witchunterQuest.EvaluateStart(witchunterProbe with { Level = 14 }).Eligible, "runtime.character.witchunter_first_half_rejects_below15");
        Check(witchunterQuest.Steps.Any(x => x.RequiredItemIds.Contains("item.1183", StringComparer.OrdinalIgnoreCase) && x.RemovedItemIds.Contains("item.1183", StringComparer.OrdinalIgnoreCase)), "witchunter.magebanequest.decoded_item_turnin");
        Check(witchunterQuest.HasDecodedLevel50WeaponUseRelationship, "witchunter.draka_blade_decoded_level50_item_gate");
        var draka = witchunterQuest.DrakaBladeEntitlement!;
        Check(draka.ItemId == "item.1334" && draka.ItemName.Equals("Draka's Blade", StringComparison.OrdinalIgnoreCase), "witchunter.draka_blade_canonical_item_identity");
        Check(draka.RequiredQuestAbilityId == 50 && draka.RequiredQuestStage == 4 && draka.ClassRestrictions.Contains(2), "witchunter.draka_blade_quest_stage_class_entitlement");
        Check(draka.MinimumUseLevel == 50 && draka.RewardScriptId.Contains("MageBaneQuest", StringComparison.OrdinalIgnoreCase), "witchunter.draka_blade_item_minimum_level_and_reward_provenance");
        var drakaEntitled = questAbilities.ApplyGrant(witchunterProbe with { Level = 49 }, 50, 4, "audit:witchunter-draka-entitlement");
        Check(!witchunterQuest.EvaluateDrakaBladeUse(drakaEntitled, questAbilities).Eligible, "runtime.character.draka_blade_rejects_below_item_minimum_level");
        Check(witchunterQuest.EvaluateDrakaBladeUse(drakaEntitled with { Level = 50 }, questAbilities).Eligible, "runtime.character.draka_blade_accepts_eligible_level50_witchunter");
        Check(!witchunterQuest.EvaluateDrakaBladeUse(drakaEntitled with { ClassId = "class.1", ClassName = "Warrior", Level = 50 }, questAbilities).Eligible, "runtime.character.draka_blade_rejects_non_witchunter");
        connected.Add("runtime.character.decoded_witchunter_magebanequest_stage_authority");
        connected.Add("runtime.character.draka_blade_entitlement_and_item_level_gate");
        Check(questAbilities.Operations.Count > 0, "quests.decoded_ability_operations");
        Check(questAbilities.Grants.Count > 0, "quests.decoded_ability_grants");
        Check(questAbilities.Operations.All(x => x.AbilityId > 0 && !string.IsNullOrWhiteSpace(x.AbilityName)), "quests.ability_identity_resolution");
        Check(RuntimeCharacterQuestAbilityCatalog.QuestProgressionAbilities.Count == 10
            && RuntimeCharacterQuestAbilityCatalog.QuestProgressionAbilities.Keys.SequenceEqual(Enumerable.Range(125, 10)),
            "quests.progression_ability_family_125_134_complete");
        var generatedAbilityCatalog = RuntimeAbilityCatalog.Load(projectRoot, worldId);
        foreach (var progressionAbility in RuntimeCharacterQuestAbilityCatalog.QuestProgressionAbilities)
        {
            Check(questAbilities.TryGetAbilityName(progressionAbility.Key, out var progressionName)
                && progressionName.Equals(progressionAbility.Value, StringComparison.Ordinal),
                $"quests.progression_ability_{progressionAbility.Key}_identity");
            var progressionProbe = questAbilities.ApplyGrant(probe, progressionAbility.Key, progressionAbility.Key - 123,
                $"audit:quest-progression:{progressionAbility.Key}");
            Check(questAbilities.HasAtLeast(progressionProbe, progressionAbility.Key, progressionAbility.Key - 123)
                && RuntimeCharacterAbilityValues.Restore(progressionProbe.Conditions).Count == 1,
                $"runtime.character.quest_progression_{progressionAbility.Key}_persistence");
            Check(!questAbilities.ResolveExecutablePowerIds(progressionProbe, generatedAbilityCatalog).Any(),
                $"runtime.character.quest_progression_{progressionAbility.Key}_not_player_power");
        }
        connected.Add("runtime.character.quest_progression_abilities_125_134");
        var firstGrant = questAbilities.Grants.First();
        var grantedProbe = questAbilities.ApplyGrant(probe, firstGrant.AbilityId, firstGrant.AbilityValue, $"audit:{firstGrant.QuestId}:{firstGrant.TextblockId}");
        Check(questAbilities.HasAtLeast(grantedProbe, firstGrant.AbilityId, firstGrant.AbilityValue), "runtime.character.quest_ability_persistence_consumer");
        connected.Add("runtime.character.quest_ability_reward_authority");
        var smashProbe = questAbilities.ApplyGrant(probe, RuntimeCharacterQuestAbilityCatalog.SmashAbilityId, 1, "audit:smash-capability");
        var meditateProbe = questAbilities.ApplyGrant(probe, RuntimeCharacterQuestAbilityCatalog.MeditateAbilityId, 1, "audit:meditate-capability");
        Check(questAbilities.ResolveExecutablePowerIds(smashProbe, generatedAbilityCatalog).Contains("ability.32", StringComparer.OrdinalIgnoreCase), "runtime.character.quest_flag_unlocks_smash");
        Check(questAbilities.ResolveExecutablePowerIds(meditateProbe, generatedAbilityCatalog).Contains("ability.187", StringComparer.OrdinalIgnoreCase), "runtime.character.quest_flag_unlocks_meditate");
        Check(!questAbilities.ResolveExecutablePowerIds(grantedProbe, generatedAbilityCatalog).Any(), "runtime.character.quest_progress_flags_not_executable_powers");
        connected.Add("runtime.character.quest_capability_live_power_restoration");
        Check(worldRewards.Rewards.Count == 9, "character_world_rewards.known_classic_sources");
        Check(worldRewards.Rewards.Count(x => x.TriggerKind == "room_interaction") == 3, "character_world_rewards.room_interactions");
        Check(worldRewards.Rewards.Count(x => x.TriggerKind == "quest_completion") == 4, "character_world_rewards.alignment_quest_rewards");
        Check(worldRewards.Rewards.Single(x => x.Id == "classic.reward.gaunt_one").RaceRequirement == "Gaunt", "character_world_rewards.gaunt_race_gate");
        Check(worldRewards.Rewards.Single(x => x.Id == "classic.reward.gaunt_one").RewardName == "See Hidden", "character_world_rewards.gaunt_see_hidden_identity");
        Check(worldRewards.Rewards.Single(x => x.Id == "classic.reward.high_druid_gem").MinimumLevel == 15, "character_world_rewards.high_druid_verified_level_gate");
        Check(worldRewards.Rewards.Single(x => x.Id == "classic.reward.ice_sorceress_throne").MinimumLevel == 15, "character_world_rewards.ice_sorceress_verified_level_gate");
        Check(worldRewards.Rewards.Single(x => x.Id == "classic.reward.adult_red_dragon").MinimumLevel == 35, "character_world_rewards.adult_red_verified_level_gate");
        Check(worldRewards.Rewards.Single(x => x.Id == "classic.reward.alignment_quest_1").MinimumLevel == 10, "character_world_rewards.alignment_1_verified_level_gate");
        Check(worldRewards.Rewards.Single(x => x.Id == "classic.reward.alignment_quest_2").MinimumLevel == 20, "character_world_rewards.alignment_2_verified_level_gate");
        Check(worldRewards.Rewards.Single(x => x.Id == "classic.reward.alignment_quest_3").MinimumLevel == 30, "character_world_rewards.alignment_3_verified_level_gate");
        Check(worldRewards.Rewards.Single(x => x.Id == "classic.reward.alignment_quest_4").MinimumLevel == 40, "character_world_rewards.alignment_4_verified_level_gate");
        Check(worldRewards.Rewards.Where(x => x.Id != "classic.reward.master_assassin").All(x => x.LevelGateResolved), "character_world_rewards.fixed_level_gates_resolved");
        Check(worldRewards.AbilityGates.Count == 23, "character_class_ability_gates.decoded_count");
        Check(worldRewards.AbilityGates.Count(x => x.AbilityId == 32) == 6, "character_class_ability_gates.smash");
        Check(worldRewards.AbilityGates.Count(x => x.AbilityId == 187) == 10, "character_class_ability_gates.meditate");
        Check(worldRewards.AbilityGates.Count(x => x.AbilityId == 186) == 7, "character_class_ability_gates.perfect_stealth");
        Check(worldRewards.MeetsClassAbilityGate(32, 1, 22) && !worldRewards.MeetsClassAbilityGate(32, 1, 21), "character_class_ability_gates.smash_warrior_level22");
        Check(worldRewards.MeetsClassAbilityGate(187, 12, 20) && !worldRewards.MeetsClassAbilityGate(187, 12, 19), "character_class_ability_gates.meditate_mage_level20");
        Check(worldRewards.MeetsClassAbilityGate(186, 15, 27) && !worldRewards.MeetsClassAbilityGate(186, 15, 26), "character_class_ability_gates.perfect_stealth_mystic_level27");
        var acquisitionAuthority = RuntimeClassicAbilityAcquisitionAuthority.Load(projectRoot, worldId);
        var perfectContext = new RuntimeQuestDialogueContext(probe with { ClassId = "class.8", ClassName = "Thief", Level = 20 }, RuntimeItemCatalog.Load(projectRoot, worldId));
        var perfectAcquisition = acquisitionAuthority.Execute(745, "secret", perfectContext);
        Check(perfectAcquisition.Granted && RuntimeCharacterAbilityValues.Restore(perfectAcquisition.Character.Conditions).ContainsKey(186),
            "runtime.character.ask_master_assassin_secret_grants_perfect_stealth");
        var meditateOwner = "audit.meditate-acquisition";
        var meditateItemState = new RuntimeItemState(
            [new RuntimeItemInstance("audit-item-1351", "item.1351", meditateOwner, RuntimeItemLocations.Character, meditateOwner, null, 0, 1, null)],
            0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var meditateCharacter = probe with { CharacterId = meditateOwner, ClassId = "class.12", ClassName = "Mage", Level = 20, ItemState = meditateItemState };
        var meditateContext = new RuntimeQuestDialogueContext(meditateCharacter, RuntimeItemCatalog.Load(projectRoot, worldId));
        var meditateAcquisition = acquisitionAuthority.Execute(132, "return", meditateContext);
        var meditateValues = RuntimeCharacterAbilityValues.Restore(meditateAcquisition.Character.Conditions);
        Check(meditateAcquisition.Granted && meditateValues.TryGetValue(187, out var meditateValue) && meditateValue == 1
            && meditateAcquisition.Character.Experience == meditateCharacter.Experience + 5000
            && (meditateAcquisition.Character.ItemState?.Items.Any(x => x.CanonicalItemId.Equals("item.1351", StringComparison.OrdinalIgnoreCase)) ?? false) is false,
            "runtime.character.ask_dhelvanen_return_consumes_item_rewards_xp_and_grants_meditate");
        connected.Add("runtime.character.decoded_ask_ability_acquisition_authority");
        var mageMeditate = questAbilities.ApplyGrant(probe with { ClassId = "class.12", ClassName = "Mage", Level = 20 }, RuntimeCharacterQuestAbilityCatalog.MeditateAbilityId, 1, "audit:meditate-mage");
        Check(questAbilities.CanUseCapability(mageMeditate, RuntimeCharacterQuestAbilityCatalog.MeditateAbilityId, worldRewards)
            && !questAbilities.CanUseCapability(mageMeditate with { Level = 19 }, RuntimeCharacterQuestAbilityCatalog.MeditateAbilityId, worldRewards)
            && !questAbilities.CanUseCapability(mageMeditate with { ClassId = "class.1", ClassName = "Warrior" }, RuntimeCharacterQuestAbilityCatalog.MeditateAbilityId, worldRewards),
            "runtime.character.meditate_requires_grant_class_and_level");
        var thiefPerfectStealth = questAbilities.ApplyGrant(probe with { ClassId = "class.8", ClassName = "Thief", Level = 20 }, RuntimeCharacterQuestAbilityCatalog.PerfectStealthAbilityId, 1, "audit:perfect-stealth-thief");
        Check(questAbilities.CanUseCapability(thiefPerfectStealth, RuntimeCharacterQuestAbilityCatalog.PerfectStealthAbilityId, worldRewards)
            && !questAbilities.CanUseCapability(thiefPerfectStealth with { Level = 19 }, RuntimeCharacterQuestAbilityCatalog.PerfectStealthAbilityId, worldRewards),
            "runtime.character.perfect_stealth_requires_grant_class_and_level");
        Check(worldRewards.MeetsClassAbilityGate(32, "Warrior", 22) && !worldRewards.MeetsClassAbilityGate(32, "Warrior", 21), "character_class_ability_gates.name_based_runtime_gate");
        var rewardProbe = questAbilities.ApplyGrant(probe, RuntimeCharacterQuestAbilityCatalog.ArmourClassAbilityId, 1, "audit:ac-bonus");
        rewardProbe = questAbilities.ApplyGrant(rewardProbe, RuntimeCharacterQuestAbilityCatalog.CriticalAbilityId, 1, "audit:critical-bonus");
        rewardProbe = questAbilities.ApplyGrant(rewardProbe, RuntimeCharacterQuestAbilityCatalog.SpellcastingAbilityId, 2, "audit:spellcasting-bonus");
        rewardProbe = questAbilities.ApplyGrant(rewardProbe, RuntimeCharacterQuestAbilityCatalog.SeeHiddenAbilityId, 1, "audit:see-hidden");
        var derivedReward = questAbilities.ResolveDerivedBonuses(rewardProbe);
        Check(derivedReward.ArmourClassBonus == 1 && derivedReward.CriticalBonus == 1 && derivedReward.SpellcastingBonus == 2 && derivedReward.SeeHiddenRank == 1, "runtime.character.quest_ability_derived_bonus_projection");
        Check(questAbilities.HasSeeHidden(rewardProbe), "runtime.character.see_hidden_passive_projection");
        var searchCapability = questAbilities.ResolveSearchCapability(rewardProbe);
        Check(searchCapability.SeeHiddenRank == 1 && searchCapability.Modifiers.TryGetValue("quest.ability.57.see_hidden_rank", out var seeHiddenRank) && seeHiddenRank == 1, "runtime.character.see_hidden_search_handoff_projection");
        connected.Add("runtime.character.see_hidden_search_modifier_handoff");
        connected.Add("runtime.character.verified_world_reward_level_gate_authority");
        connected.Add("runtime.character.decoded_class_specific_capability_gate_authority");
        connected.Add("runtime.character.world_reward_anchor_authority");
        connected.Add("runtime.character.quest_ability_derived_bonus_authority");
        connected.Add("runtime.character.class_gated_live_power_restoration");

        Check(!string.IsNullOrWhiteSpace(catalog.CreationContract.StartingAlignmentClassification), "character_creation_rules.starting_alignment_classification");
        Check(!string.IsNullOrWhiteSpace(catalog.AlignmentRuleContract.AttackPenaltyRule), "alignment_evil_point_rules.attack_penalty_rule");
        Check(!catalog.AlignmentRuleContract.CreationBlocking, "alignment_evil_point_rules.creation_blocking");
        Check(catalog.AlignmentRuleContract.DefaultEpCycleMinutes == catalog.AlignmentRuleContract.CycleMinutes, "alignment_evil_point_rules.default_epcycle_minutes");
        Check(catalog.AlignmentRuleContract.DefaultEpAmount == catalog.AlignmentRuleContract.CycleAmount, "alignment_evil_point_rules.default_epamount");
        Check(catalog.AlignmentRuleContract.MaximumDailyReduction == 18, "alignment_evil_point_rules.maxepday_default_18");
        Check(catalog.AlignmentRuleContract.ReputationMinimumEvilPoints.Count == 8
            && catalog.AlignmentRuleContract.ReputationMinimumEvilPoints["Good"] == -200
            && catalog.AlignmentRuleContract.ReputationMinimumEvilPoints["Neutral"] == -50
            && catalog.AlignmentRuleContract.ReputationMinimumEvilPoints["Seedy"] == 30
            && catalog.AlignmentRuleContract.ReputationMinimumEvilPoints["Outlaw"] == 40
            && catalog.AlignmentRuleContract.ReputationMinimumEvilPoints["Criminal"] == 80
            && catalog.AlignmentRuleContract.ReputationMinimumEvilPoints["Villain"] == 120
            && catalog.AlignmentRuleContract.ReputationMinimumEvilPoints["Fiend"] == 210, "alignment_evil_point_rules.reputation_thresholds");
        Check(!string.IsNullOrWhiteSpace(catalog.AlignmentRuleContract.NegativeEpException), "alignment_evil_point_rules.negative_ep_exception");
        Check(catalog.DerivedFormulaContract.CreationAbilityAndEquipmentCache == 0, "derived_stat_formulas.maximum_mana.creation_ability_and_equipment_cache");
        Check(catalog.DerivedFormulaContract.SpellcastingTypes.Count == 5, "derived_stat_formulas.spellcasting.types");
        connected.Add("runtime.character.complete_foundation_contract_projection");

        var trainerPath = Path.Combine(projectRoot, "Data", worldId, "shops.json");
        if (!File.Exists(trainerPath))
        {
            diagnostics.Add($"Generated trainer source is missing: {trainerPath}");
        }
        else
        {
            using var trainerDocument = JsonDocument.Parse(File.ReadAllText(trainerPath));
            var trainerRecords = trainerDocument.RootElement.GetProperty("records")
                .EnumerateArray()
                .Where(record => record.TryGetProperty("service_kind", out var kind) && kind.ValueKind == JsonValueKind.String && string.Equals(kind.GetString(), "trainer", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var restricted = trainerRecords.Count(record => ReadTrainerInt(record, "class_limit") > 0);
            var universal = trainerRecords.Count(record => ReadTrainerInt(record, "class_limit") <= 0);
            Check(trainerRecords.Length > 0, "shops.records[service_kind=trainer]");
            Check(restricted > 0, "trainers.class_restricted_admission");
            Check(universal > 0, "trainers.universal_class_admission");
            connected.Add("runtime.training.single_class_trainer_admission");
        }

        Check(catalog.Races.All(x => x.LegacyNumber > 0), "races[].number");
        Check(catalog.Races.All(x => x.MinimumStats.Count == 6 && x.MaximumStats.Count == 6), "races[].minimum_stats/maximum_stats");
        Check(catalog.Races.All(x => !string.IsNullOrWhiteSpace(x.SourceFile) && x.SourceRecordOffset > 0), "races[].source.file/record_offset");
        Check(catalog.Classes.All(x => x.LegacyNumber > 0), "classes[].number");
        Check(catalog.Classes.All(x => x.ExperiencePercent == x.ExperienceStored + 100), "classes[].experience_stored/experience_percent");
        Check(catalog.Classes.All(x => x.HitPoints is not null), "classes[].min_hits/max_hits_bonus");
        Check(catalog.Classes.All(x => x.WeaponType >= 0 && x.ArmourType >= 0 && x.CombatLevel >= 0), "classes[].weapon_type/armour_type/combat_level");
        Check(catalog.Classes.All(x => !string.IsNullOrWhiteSpace(x.MageryName)), "classes[].magery_name");
        Check(catalog.Classes.All(x => !string.IsNullOrWhiteSpace(x.SourceFile) && x.SourceRecordOffset > 0), "classes[].source.file/record_offset");
        Check(classCapabilities.Classes.Count == catalog.Classes.Count, "classes[].capability_profile_projection");
        Check(classCapabilities.Classes.All(x => x.WeaponTypeCode > 0 && x.ArmourTypeCode > 0 && x.CombatLevelCode > 0), "classes[].decoded_weapon_armour_combat_codes");
        Check(classCapabilities.Classes.Where(x => x.MagicFamily > 0).All(x => x.MagicRank > 0), "classes[].magic_type_magic_rank");
        var paladinMagic = classCapabilities.RequireClass("Paladin");
        Check(paladinMagic.MagicFamily == 2 && paladinMagic.MagicRank == 1, "classes[Paladin].priest_rank_1");
        var priestMagic = classCapabilities.RequireClass("Priest");
        Check(priestMagic.MagicFamily == 2 && priestMagic.MagicRank == 3, "classes[Priest].priest_rank_3");
        var priestLowSpell = classCapabilities.Spells.FirstOrDefault(x => x.RequiredMagicFamily == 2 && x.RequiredMagicRank <= 1);
        var priestHighSpell = classCapabilities.Spells.FirstOrDefault(x => x.RequiredMagicFamily == 2 && x.RequiredMagicRank == 3);
        Check(priestLowSpell is not null && classCapabilities.CanPurchaseSpell("Paladin", priestLowSpell.SpellId).Allowed, "runtime.character.magic_type.low_rank_spell_eligibility");
        Check(priestHighSpell is not null && !classCapabilities.CanPurchaseSpell("Paladin", priestHighSpell.SpellId).Allowed && classCapabilities.CanPurchaseSpell("Priest", priestHighSpell.SpellId).Allowed, "runtime.character.magic_type.rank_cap_enforcement");
        Check(classCapabilities.CanUseWeaponType("Warrior", 1).EvidenceStatus == "relationship_unresolved", "runtime.character.weapon_type_fail_closed_until_code_map");
        Check(classCapabilities.CanUseArmourType("Warrior", 1).EvidenceStatus == "relationship_unresolved", "runtime.character.armour_type_fail_closed_until_code_map");
        connected.Add("runtime.character.decoded_magic_family_rank_spell_eligibility");
        connected.Add("runtime.character.class_weapon_armour_combat_code_projection");
        Check(itemRequirements.Items.Count > 0, "items[].character_requirement_projection");
        Check(itemRequirements.Items.All(x => x.MinimumLevel >= 0), "items[].decoded_minimum_level");
        var drakaRequirement = itemRequirements.Require("item.1334");
        Check(drakaRequirement.MinimumLevel == 50 && drakaRequirement.AllowedClasses.Contains(2) && drakaRequirement.WeaponType == 2, "items[1334].decoded_level_class_weapon_requirements");
        var drakaCharacter = probe with { ClassId = "class.2", ClassName = "Witchunter", Level = 50, BaseStats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["str"] = 100 } };
        Check(itemRequirements.EvaluateCharacterInputs(drakaCharacter, "item.1334").Allowed, "runtime.character.item_requirement_inputs_draka_level50");
        Check(!itemRequirements.EvaluateCharacterInputs(drakaCharacter with { Level = 49 }, "item.1334").Allowed, "runtime.character.item_requirement_inputs_minlevel_rejection");
        Check(!itemRequirements.EvaluateCharacterInputs(drakaCharacter with { ClassId = "class.1", ClassName = "Warrior" }, "item.1334").Allowed, "runtime.character.item_requirement_inputs_class_rejection");
        connected.Add("runtime.character.decoded_item_level_class_race_stat_requirement_consumer");

        foreach (var race in catalog.Races)
        foreach (var characterClass in catalog.Classes)
        {
            var expected = characterClass.ExperiencePercent + race.ExperienceAddPercent;
            var actual = RuntimeClassicCharacterProgressionFormula.ExperienceChartPercent(race, characterClass);
            if (actual != expected) diagnostics.Add($"XP chart runtime mismatch for {race.Name}/{characterClass.Name}: expected {expected}, got {actual}.");
        }
        if (!diagnostics.Any(x => x.StartsWith("XP chart runtime mismatch", StringComparison.Ordinal))) connected.Add("runtime.progression.decoded_race_class_experience_chart");

        var alignment = RuntimeCharacterAlignmentResolver.CreateInitial(catalog.AlignmentRuleContract);
        Check(alignment.EvilPoints == catalog.AlignmentRuleContract.InitialEvilPoints && alignment.Reputation == catalog.AlignmentRuleContract.InitialReputation, "runtime.alignment.decoded_initial_state");
        Check(catalog.AlignmentRuleContract.AttackReputationPenalties.All(pair => RuntimeCharacterAlignmentResolver.AttackPenaltyForTargetReputation(pair.Key, catalog.AlignmentRuleContract) == pair.Value), "runtime.alignment.decoded_attack_penalties");
        Check(RuntimeCharacterAlignmentResolver.CanInitiateAttack(alignment with { EvilPoints = catalog.AlignmentRuleContract.FiendAttackLockoutThreshold }, catalog.AlignmentRuleContract) && !RuntimeCharacterAlignmentResolver.CanInitiateAttack(alignment with { EvilPoints = catalog.AlignmentRuleContract.FiendAttackLockoutThreshold + 1 }, catalog.AlignmentRuleContract), "runtime.alignment.decoded_fiend_lockout");
        var cleanup = RuntimeCharacterAlignmentResolver.CreateCleanupConfiguration(catalog.AlignmentRuleContract);
        Check(cleanup.EvilActionReduction == catalog.AlignmentRuleContract.EvilActionReduction && cleanup.NoEvilActionBaseReduction == catalog.AlignmentRuleContract.NoEvilActionBaseReduction && cleanup.CycleMinutes == catalog.AlignmentRuleContract.CycleMinutes && cleanup.CycleAmount == catalog.AlignmentRuleContract.CycleAmount && cleanup.MaximumDailyReduction == 18, "runtime.alignment.decoded_cleanup_configuration");
        Check(RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(29, "Neutral", catalog.AlignmentRuleContract) == "Neutral"
            && RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(30, "Neutral", catalog.AlignmentRuleContract) == "Seedy"
            && RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(40, "Seedy", catalog.AlignmentRuleContract) == "Outlaw"
            && RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(80, "Outlaw", catalog.AlignmentRuleContract) == "Criminal"
            && RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(120, "Criminal", catalog.AlignmentRuleContract) == "Villain"
            && RuntimeCharacterAlignmentResolver.ResolveReputationForEvilPoints(210, "Villain", catalog.AlignmentRuleContract) == "Fiend", "runtime.alignment.decoded_reputation_transition_thresholds");

        foreach (var race in catalog.Races)
        foreach (var characterClass in catalog.Classes)
        {
            try
            {
                var stats = race.MinimumStats.Count > 0 ? race.MinimumStats : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["hea"] = 50 };
                if (!stats.ContainsKey("hea")) stats = new Dictionary<string, int>(stats, StringComparer.OrdinalIgnoreCase) { ["hea"] = 50 };
                var derived = RuntimeCharacterDerivedStatisticCalculator.CalculateCreation(race, characterClass, 1, stats);
                if (derived.MaximumHitPoints < 1 || derived.MaximumMana < 0) diagnostics.Add($"Derived-stat runtime projection invalid for {race.Name}/{characterClass.Name}.");
                var grants = RuntimeCharacterAbilityResolver.Resolve(race, characterClass);
                if (grants.Grants.Count != race.AbilitySlots.Count + characterClass.AbilitySlots.Count) diagnostics.Add($"Creation ability merge mismatch for {race.Name}/{characterClass.Name}.");
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Character creation runtime projection failed for {race.Name}/{characterClass.Name}: {ex.Message}");
            }
        }
        if (!diagnostics.Any(x => x.StartsWith("Derived-stat runtime projection", StringComparison.Ordinal) || x.StartsWith("Character creation runtime projection", StringComparison.Ordinal))) connected.Add("runtime.creation.decoded_derived_statistics");
        if (!diagnostics.Any(x => x.StartsWith("Creation ability merge mismatch", StringComparison.Ordinal) || x.StartsWith("Character creation runtime projection", StringComparison.Ordinal))) connected.Add("runtime.creation.decoded_race_class_ability_merge");

        return new(diagnostics.Count == 0, worldId, catalog.Races.Count, catalog.Classes.Count, connected, diagnostics);

        void Check(bool condition, string path)
        {
            if (condition) connected.Add(path); else diagnostics.Add($"Disconnected or invalid generated field: {path}");
        }
    }

    private static int ReadTrainerInt(JsonElement record, string property)
    {
        if (!record.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }

    private static bool IsBundleARepositoryCoverageMetadata(ClassicDecodeFieldCoverage field)
        => field.FieldName.Equals("promotion_note", StringComparison.OrdinalIgnoreCase) ||
           field.FieldName.Equals("world_id", StringComparison.OrdinalIgnoreCase) ||
           field.FieldName.Equals("evidenceState", StringComparison.OrdinalIgnoreCase) ||
           field.JsonPath.Contains(".spellcasting.types.", StringComparison.OrdinalIgnoreCase);
}
