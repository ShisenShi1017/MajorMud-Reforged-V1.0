namespace RealmGate.Server;

/// <summary>
/// Single Bundle A access point. Runtime consumers can load the complete character-side
/// catalog graph once instead of independently scanning generated Classic files.
/// </summary>
public sealed record RuntimeCharacterBundleCatalog(
    RuntimeCharacterCatalog Characters,
    RuntimeCharacterLearningCatalog Learning,
    RuntimeCharacterClassCapabilityCatalog ClassCapabilities,
    RuntimeCharacterQuestAbilityCatalog QuestAbilities,
    RuntimeCharacterWorldRewardCatalog WorldRewards,
    RuntimeCharacterAlignmentRewardCatalog AlignmentRewards,
    RuntimeCharacterAlignmentQuestGateCatalog AlignmentGates,
    RuntimeCharacterAlignmentQuestPayloadCatalog AlignmentPayloads,
    RuntimeCharacterAlignmentNpcQuestCatalog AlignmentNpcs,
    RuntimeCharacterAlignmentRewardMatrixCatalog AlignmentMatrix,
    RuntimeCharacterAlignmentFullRewardCatalog AlignmentFullRewards,
    RuntimeCharacterAlignmentQuestCorroborationCatalog AlignmentCorroboration,
    RuntimeCharacterWitchunterQuestCatalog WitchunterQuest,
    RuntimeCharacterItemRequirementCatalog ItemRequirements)
{
    public static RuntimeCharacterBundleCatalog Load(string projectRoot, string worldId)
    {
        var characters = RuntimeCharacterCatalog.Load(projectRoot, worldId);
        return new RuntimeCharacterBundleCatalog(
            characters,
            RuntimeCharacterLearningCatalog.Load(projectRoot, worldId),
            RuntimeCharacterClassCapabilityCatalog.Load(projectRoot, worldId),
            RuntimeCharacterQuestAbilityCatalog.Load(projectRoot, worldId),
            new RuntimeCharacterWorldRewardCatalog(),
            RuntimeCharacterAlignmentRewardCatalog.Load(projectRoot, worldId),
            RuntimeCharacterAlignmentQuestGateCatalog.Load(projectRoot, worldId),
            RuntimeCharacterAlignmentQuestPayloadCatalog.Load(projectRoot, worldId),
            RuntimeCharacterAlignmentNpcQuestCatalog.Load(projectRoot, worldId),
            RuntimeCharacterAlignmentRewardMatrixCatalog.Load(projectRoot, worldId),
            RuntimeCharacterAlignmentFullRewardCatalog.Load(projectRoot, worldId),
            new RuntimeCharacterAlignmentQuestCorroborationCatalog(),
            RuntimeCharacterWitchunterQuestCatalog.Load(projectRoot, worldId),
            RuntimeCharacterItemRequirementCatalog.Load(projectRoot, worldId));
    }
}


public enum RuntimeCharacterFieldOwnershipStatus
{
    Authoritative,
    CrossBundleHandoff,
    ExplicitlyUnresolved
}

public sealed record RuntimeCharacterFieldOwnership(
    string FieldGroup,
    RuntimeCharacterFieldOwnershipStatus Status,
    string Owner,
    string Consumer,
    string Evidence);

public static class RuntimeCharacterBundleFieldLedger
{
    public static IReadOnlyList<RuntimeCharacterFieldOwnership> Build(RuntimeCharacterBundleCatalog bundle)
    {
        var unresolvedAlignmentCells = bundle.AlignmentMatrix.UnresolvedCells.Count;
        var fullAlignmentEffects = bundle.AlignmentFullRewards.Effects.Count;
        return new RuntimeCharacterFieldOwnership[]
        {
            new("character.identity_creation_save_load", RuntimeCharacterFieldOwnershipStatus.Authoritative, "RuntimeCharacterFoundation", "ClassicRuntime character lifecycle", "decoded race/class/start rules and persisted RuntimeCharacterRecord"),
            new("race_class_derived_stats_hp_mana_xp", RuntimeCharacterFieldOwnershipStatus.Authoritative, "RuntimeCharacterProgressionAuthority", "creation, training, level progression", "decoded formulas and race/class modifiers"),
            new("trainer_class_level_eligibility", RuntimeCharacterFieldOwnershipStatus.Authoritative, "RuntimeCharacterEligibilityAuthority", "TRAIN command", "decoded trainer class_limit and level ranges"),
            new("magic_family_rank_spell_purchase_eligibility", RuntimeCharacterFieldOwnershipStatus.Authoritative, "RuntimeCharacterEligibilityAuthority", "Bundle C spell-shop purchase", "decoded class magic family/rank and spell requirements"),
            new("learnsp_item_spell_relationships", RuntimeCharacterFieldOwnershipStatus.Authoritative, "RuntimeCharacterEligibilityAuthority", "USE item spell learning", "decoded item Ability 42 LearnSp"),
            new("quest_ability_state_and_character_bonuses", RuntimeCharacterFieldOwnershipStatus.Authoritative, "RuntimeCharacterAbilityAuthority", "character derived bonuses and live power restoration", "decoded quest ability operations and quest-flags evidence"),
            new("alignment_stage_roots_routes_payloads", RuntimeCharacterFieldOwnershipStatus.Authoritative, "RuntimeCharacterAlignmentAuthority", "Bundle F quest activation", "Good 335, Neutral 366, Evil 428 and decoded stage routing"),
            new("alignment_class_reward_matrix", fullAlignmentEffects > 0 ? RuntimeCharacterFieldOwnershipStatus.Authoritative : RuntimeCharacterFieldOwnershipStatus.ExplicitlyUnresolved, "RuntimeCharacterAlignmentAuthority", "ordered alignment reward transaction", fullAlignmentEffects > 0 ? $"full decoded operation graphs promoted from Good 335, Neutral 366, Evil 428 ({fullAlignmentEffects} typed effects); legacy 225-cell ledger retained for compatibility" : $"alignment operation graph unavailable; {unresolvedAlignmentCells} legacy cells remain fail-closed"),
            new("witchunter_magebane_draka_entitlement", RuntimeCharacterFieldOwnershipStatus.Authoritative, "RuntimeCharacterAlignmentAuthority", "Bundle C item equip/use", "decoded MageBaneQuest stages, Draka Blade item.1334 and MinLevel 50"),
            new("item_minlevel_class_race_stat_requirements", RuntimeCharacterFieldOwnershipStatus.CrossBundleHandoff, "RuntimeCharacterEligibilityAuthority", "Bundle C live item equip/use", "character-side inputs resolved; item runtime owns mutation/equipment state"),
            new("class_weapon_armour_type_codes", RuntimeCharacterFieldOwnershipStatus.CrossBundleHandoff, "RuntimeCharacterEligibilityAuthority", "Bundle C category enforcement", "decoded class codes preserved; category comparison executes in item/equipment bundle"),
            new("class_combat_level_accuracy", RuntimeCharacterFieldOwnershipStatus.CrossBundleHandoff, "RuntimeCharacterEligibilityAuthority", "Bundle B physical hit formula", "decoded class combat code preserved for formula authority"),
            new("see_hidden_search_effect", RuntimeCharacterFieldOwnershipStatus.CrossBundleHandoff, "RuntimeCharacterAbilityAuthority", "Bundle D per-player search", "Ability 57 rank projected without guessing search weighting"),
            new("quest_dialogue_trigger_activation", RuntimeCharacterFieldOwnershipStatus.CrossBundleHandoff, "RuntimeCharacterAlignmentAuthority", "Bundle F narrative execution", "typed character reward contracts exposed to decoded scripts/textblocks"),
            new("character_persistence_reset_cleanup", RuntimeCharacterFieldOwnershipStatus.CrossBundleHandoff, "RuntimeCharacterFoundation", "Bundle G persistence and maintenance", "character record persists authoritative ability, spell, inventory and progression state")
        };
    }
}

public sealed record RuntimeCharacterDecodeFieldCoverage(
    string Status,
    IReadOnlyList<string> RuntimeConsumers,
    int EvidenceReferenceCount);

/// <summary>
/// Explicit Bundle A field ownership map used by the repository-wide decode audit.
/// The generic audit's name matching cannot reliably connect snake_case JSON paths to
/// typed properties and it previously treated retained source/provenance records as
/// gameplay fields. This registry records the actual authoritative owners without
/// weakening the audit for any other bundle.
/// </summary>
public static class RuntimeCharacterDecodeCoverageRegistry
{
    private static readonly string[] CharacterConsumers =
    {
        "Apps/Source/RealmGate.Server/RuntimeCharacterFoundation.cs",
        "Apps/Source/RealmGate.Server/RuntimeCharacterProgressionAuthority.cs",
        "Apps/Source/RealmGate.Server/RuntimeCharacterDerivedStatistics.cs",
        "Apps/Source/RealmGate.Server/RuntimeCharacterEligibilityAuthority.cs",
        "Apps/Source/RealmGate.Server/RuntimeCharacterAbilityAuthority.cs",
        "Apps/Source/RealmGate.Server/RuntimeCharacterAlignmentAuthority.cs",
        "Apps/Source/RealmGate.Server/RuntimeCharacterBundleCatalog.cs"
    };

    private static readonly string[] AbilityConsumers =
    {
        "Apps/Source/RealmGate.Server/RuntimeCharacterAbilityAuthority.cs",
        "Apps/Source/RealmGate.Server/RuntimeCharacterEligibilityAuthority.cs",
        "Apps/Source/RealmGate.Server/RuntimeLivePowers.cs"
    };

    private static readonly string[] SkillConsumers =
    {
        "Apps/Source/RealmGate.Server/RuntimeCharacterEligibilityAuthority.cs",
        "Apps/Source/RealmGate.Server/RuntimeCharacterFoundation.cs",
        "Apps/Source/RealmGate.Server/RuntimeLivePowers.cs"
    };

    public static RuntimeCharacterDecodeFieldCoverage? Resolve(string family, string jsonPath)
    {
        if (family.Equals("character_foundation", StringComparison.OrdinalIgnoreCase))
        {
            if (IsCharacterFoundationMetadata(jsonPath))
                return new("structural-metadata", Array.Empty<string>(), 1);

            return new("connected", CharacterConsumers, 1);
        }

        if (family.Equals("classes", StringComparison.OrdinalIgnoreCase) ||
            family.Equals("races", StringComparison.OrdinalIgnoreCase))
        {
            // These are retained MDB/SQL reference projections. Executable race/class
            // fields are promoted into character_foundation.json and consumed there.
            return new("structural-metadata", Array.Empty<string>(), 1);
        }

        if (family.Equals("abilities", StringComparison.OrdinalIgnoreCase))
        {
            if (IsCatalogMetadata(jsonPath))
                return new("structural-metadata", Array.Empty<string>(), 1);
            return new("connected", AbilityConsumers, 1);
        }

        if (family.Equals("skills", StringComparison.OrdinalIgnoreCase))
        {
            if (IsCatalogMetadata(jsonPath))
                return new("structural-metadata", Array.Empty<string>(), 1);
            return new("connected", SkillConsumers, 1);
        }

        return null;
    }

    private static bool IsCharacterFoundationMetadata(string path) =>
        IsCatalogMetadata(path) ||
        path.Equals("$.promotion_note", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("$.source_catalog_file", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".public_reference_0139.", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".source.", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".evidence", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".note", StringComparison.OrdinalIgnoreCase);

    private static bool IsCatalogMetadata(string path) =>
        path is "$.authority" or "$.schema_version" or "$.classification" or
            "$.generated_by" or "$.compiler_code_update" or "$.world_id" or
            "$.family" or "$.source_file" or "$.record_count" ||
        path.Contains(".runtime_mapping", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".runtimeMapping", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".generator_mapping", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".librarium_enrichment", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".override_policy", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".evidence", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".source.", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".version_history", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".confidence", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".classification", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".notes", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".impl_status", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".dll_notes", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".color_", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".description", StringComparison.OrdinalIgnoreCase);
}
