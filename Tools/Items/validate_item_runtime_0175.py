from pathlib import Path
import json, sys
root=Path(sys.argv[1] if len(sys.argv)>1 else ".").resolve()
source=(root/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text(encoding="utf-8")
smoke=(root/"Apps/Source/Reforged.Server/RuntimeItemSmokeReport.cs").read_text(encoding="utf-8")
for token in [
    "string? LootOwnerId = null",
    "DateTimeOffset? ExclusiveLootUntilUtc = null",
    "public bool CanLootCorpse",
    "EnforceCorpseLootAccess(corpse, characterId",
    "Corpse loot is temporarily reserved for another owner.",
    "INVALID_CORPSE_LOOT_RESERVATION",
    "INVALID_CORPSE_LOOT_WINDOW"
]:
    if token not in source: raise SystemExit("missing runtime reference: "+token)
for token in [
    "corpse-exclusive-loot-permission-and-expiry",
    "non-owner was allowed during exclusive loot window",
    "corpse reservation did not expire"
]:
    if token not in smoke: raise SystemExit("missing smoke coverage: "+token)
for rel in [
    "Evidence/Items/CodeUpdate0175/item_corpse_loot_permission_semantics.json",
    "Librarium/Policies/runtime_item_corpse_loot_permission_policy.json"
]:
    json.loads((root/rel).read_text(encoding="utf-8"))
items=json.loads((root/"Data/Classic/items.json").read_text(encoding="utf-8"))["records"]
print(f"CodeUpdate0175 validation passed; canonical_items={len(items)}")
