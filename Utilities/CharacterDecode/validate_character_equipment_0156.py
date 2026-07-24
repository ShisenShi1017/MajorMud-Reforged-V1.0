#!/usr/bin/env python3
import json, sys
from pathlib import Path

def require(text: str, token: str) -> None:
    if token not in text: raise SystemExit(f"missing runtime token: {token}")

def main(overlay: Path, project: Path) -> int:
    contract=json.loads((overlay/"Data/NativeDecodeReports/CodeUpdate0156CharacterEquipment/character_equipment_curse_contract_0156.json").read_text())
    assert contract["source_records"]["cursed_82_count"] == 15
    assert contract["source_records"]["persistent_cursed_83_count"] == 3
    runtime=(overlay/"Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs").read_text()
    for token in ["CursedWhenEquipped","RetainedOnDeathCurse","RemoveCurse(string characterId)","allowEquippedTransfer","Equipped items must be removed before transfer"]: require(runtime, token)
    records=json.loads((project/"Data/Classic/items.json").read_text())["records"]
    counts={82:0,83:0}
    for record in records:
        abilities=(((record.get("public_reference_0139") or {}).get("fields") or {}).get("abilities_named") or [])
        ids={int(a["ability_id"]) for a in abilities if str(a.get("ability_id","")).isdigit()}
        for aid in counts: counts[aid]+=aid in ids
    assert counts == {82:15,83:3}, counts
    print("CodeUpdate0156 character equipment validation passed")
    return 0

if __name__ == "__main__":
    overlay=Path(sys.argv[1] if len(sys.argv)>1 else ".")
    project=Path(sys.argv[2] if len(sys.argv)>2 else ".")
    raise SystemExit(main(overlay,project))
