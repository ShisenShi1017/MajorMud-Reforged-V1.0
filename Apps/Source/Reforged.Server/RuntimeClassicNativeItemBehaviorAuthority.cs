namespace Reforged.Server;

/// <summary>
/// Evidence gate for the recovered Classic MajorMUD native-only item/equipment boundary.
///
/// The retained WCCMMUD/WBBMMUD ABI proves dedicated native ownership for item record IO,
/// inventory/room placement, item lookup/rendering, equipment ability application, armor/weapon
/// handling, charge deduction, item-target use/casting, shop stock, limited-item refresh/restock,
/// and user-item cross-reference polling.  This closes the broad ownership boundary only.
/// Exact record layouts/flag bits, stack maxima, charge/durability formulas, loot/reset RNG and
/// branch ordering, persistence offsets, exact messages, and edge-case exception ordering remain
/// fail-closed and must not be inferred from the Reforged compatibility item engine.
/// </summary>
public static class RuntimeClassicNativeItemBehaviorAuthority
{
    public const int CanonicalDecodedItemCount = 1950;
    public const int NativeItemEquipmentExportCount = 51;
    public const int NativeItemFacingCommandExportCount = 13;
    public const int WccMudExportCount = 592;
    public const int WbbMudExportCount = 592;
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public const string WbbMudSha256 = "88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091";

    public static readonly IReadOnlyList<string> NativeItemEquipmentExports = new[]
    {
        "_add_item_to_inventory",
        "_add_item_to_room",
        "_begin_refreshing_limited_items",
        "_begin_user_item_xref",
        "_buy_item",
        "_cast_item_target",
        "_check_initiate_restocking",
        "_clear_item",
        "_clear_items_to_null",
        "_cmd_equip",
        "_deduct_item_charge",
        "_display_all_restocking_required",
        "_display_inventory_items",
        "_display_item_desc",
        "_display_item_in_shop",
        "_display_items_in_room",
        "_display_list_of_all_items",
        "_display_list_of_limited_items",
        "_display_new_list_of_limited_items",
        "_display_shop_items",
        "_dispose_of_item_in_room",
        "_dispose_of_item_in_trail",
        "_equip_apply_item_abilities",
        "_find_item_in_inventory",
        "_find_item_in_room",
        "_get_armour_rating",
        "_get_item_ability_value",
        "_get_item_data",
        "_get_item_from_name",
        "_get_last_item_number",
        "_get_shop_item_from_name",
        "_is_weapon_too_fast_for_user",
        "_item_has_ability",
        "_item_is_in_stock",
        "_load_item_into_buffer",
        "_mem_get_item",
        "_ready_weapon",
        "_refresh_items_polling_routine",
        "_remove_armour",
        "_remove_item_from_inventory",
        "_remove_item_from_room",
        "_restock_items",
        "_save_item_from_buffer",
        "_sell_item",
        "_shop_sells_item",
        "_unequip_apply_item_abilities",
        "_update_allowed_worn_items",
        "_use_item_target",
        "_user_is_wearing",
        "_wear_armour",
        "_xref_users_items_polling_routine",
    };

    public static readonly IReadOnlyList<string> NativeItemFacingCommandExports = new[]
    {
        "_cmd_appraise",
        "_cmd_buy",
        "_cmd_drink",
        "_cmd_drop",
        "_cmd_eat",
        "_cmd_equip",
        "_cmd_get",
        "_cmd_give",
        "_cmd_list",
        "_cmd_sell",
        "_cmd_stock",
        "_cmd_unstock",
        "_cmd_use",
    };

    public const bool NativeItemRecordIoOwnershipResolved = true;
    public const bool NativeInventoryRoomMutationOwnershipResolved = true;
    public const bool NativeEquipmentAbilityOwnershipResolved = true;
    public const bool NativeChargeUseOwnershipResolved = true;
    public const bool NativeLimitedRefreshRestockOwnershipResolved = true;
    public const bool NativeShopItemOwnershipResolved = true;
    public const bool PreNativeSemanticCoverageCanReceiveClassicCredit = true;

    public const bool ReforgedGenericItemEngineDefinesExactClassicSemantics = false;
    public const bool ReforgedStackPolicyDefinesNativeStackMaximums = false;
    public const bool ReforgedDurabilityPolicyDefinesNativeWearFormula = false;
    public const bool ReforgedLootSeedsDefineNativeRngContract = false;
    public const bool ReforgedItemPersistenceDefinesNativeRecordLayout = false;

    public const bool ExactItemRecordLayoutResolved = false;
    public const bool ExactItemTypeAndFlagBitsResolved = false;
    public const bool ExactStackMaximumsResolved = false;
    public const bool ExactChargeConsumptionRulesResolved = false;
    public const bool ExactDurabilityWearRepairBreakageResolved = false;
    public const bool ExactLootQuantityChanceAndRngOrderingResolved = false;
    public const bool ExactLimitedUniqueReleaseOrderingResolved = false;
    public const bool ExactResetRestockSchedulingResolved = false;
    public const bool ExactNativeItemPersistenceLayoutResolved = false;
    public const bool ExactItemCommandMessagesResolved = false;
    public const bool ExactItemPermissionExceptionOrderingResolved = false;
    public const bool ExactContainerDepthWeightEdgeOrderingResolved = false;
    public const bool ExactCorpseResurrectionEquipmentOrderingResolved = false;

    public static bool OwnsNativeExport(string exportName)
        => NativeItemEquipmentExports.Contains(exportName, StringComparer.Ordinal);

    public static void RequireClassicItemRecordLayout() { if (!ExactItemRecordLayoutResolved) throw new InvalidOperationException("native-item-record-layout-unresolved"); }
    public static void RequireClassicItemTypeAndFlagBits() { if (!ExactItemTypeAndFlagBitsResolved) throw new InvalidOperationException("native-item-type-flag-bits-unresolved"); }
    public static void RequireClassicStackMaximums() { if (!ExactStackMaximumsResolved) throw new InvalidOperationException("native-item-stack-maximums-unresolved"); }
    public static void RequireClassicChargeConsumptionRules() { if (!ExactChargeConsumptionRulesResolved) throw new InvalidOperationException("native-item-charge-rules-unresolved"); }
    public static void RequireClassicDurabilityWearRepairBreakage() { if (!ExactDurabilityWearRepairBreakageResolved) throw new InvalidOperationException("native-item-durability-formulas-unresolved"); }
    public static void RequireClassicLootRngOrdering() { if (!ExactLootQuantityChanceAndRngOrderingResolved) throw new InvalidOperationException("native-item-loot-rng-ordering-unresolved"); }
    public static void RequireClassicLimitedReleaseOrdering() { if (!ExactLimitedUniqueReleaseOrderingResolved) throw new InvalidOperationException("native-item-limited-release-ordering-unresolved"); }
    public static void RequireClassicResetRestockScheduling() { if (!ExactResetRestockSchedulingResolved) throw new InvalidOperationException("native-item-reset-restock-scheduling-unresolved"); }
    public static void RequireClassicItemPersistenceLayout() { if (!ExactNativeItemPersistenceLayoutResolved) throw new InvalidOperationException("native-item-persistence-layout-unresolved"); }
    public static void RequireClassicItemCommandMessages() { if (!ExactItemCommandMessagesResolved) throw new InvalidOperationException("native-item-command-messages-unresolved"); }
    public static void RequireClassicItemPermissionOrdering() { if (!ExactItemPermissionExceptionOrderingResolved) throw new InvalidOperationException("native-item-permission-ordering-unresolved"); }
    public static void RequireClassicContainerEdgeOrdering() { if (!ExactContainerDepthWeightEdgeOrderingResolved) throw new InvalidOperationException("native-item-container-edge-ordering-unresolved"); }
    public static void RequireClassicCorpseResurrectionEquipmentOrdering() { if (!ExactCorpseResurrectionEquipmentOrderingResolved) throw new InvalidOperationException("native-item-corpse-resurrection-ordering-unresolved"); }
}
