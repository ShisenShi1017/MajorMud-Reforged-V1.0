"""Equipment stat calculator for Runtime-0003.

This calculator aggregates explicit item fields from canonical /Data. It does
not infer hidden classic mechanics. Ability records are preserved and counted,
but not converted into stat effects until their exact semantics are verified.
"""
from __future__ import annotations

from dataclasses import dataclass, field, asdict
from typing import Any, Dict, Iterable, List, Optional

from Runtime.adapters import load_schema_bundle
from Runtime.character_snapshot import CharacterSnapshot

@dataclass
class EquipmentIssue:
    severity: str
    code: str
    message: str
    item_id: Optional[str] = None

@dataclass
class EquipmentStatTrace:
    formula_id: str
    mode: str
    classic_accuracy: str
    character_id: str
    equipped_items: List[str]
    totals: Dict[str, Any]
    item_steps: List[Dict[str, Any]] = field(default_factory=list)
    issues: List[EquipmentIssue] = field(default_factory=list)

    def to_jsonable(self) -> Dict[str, Any]:
        return asdict(self)


def _num(mapping: Dict[str, Any], key: str, default: int = 0) -> int:
    try:
        return int(mapping.get(key, default) or 0)
    except Exception:
        return default


def calculate_equipment_stats(snapshot: CharacterSnapshot, data_dir: str = "Data") -> EquipmentStatTrace:
    bundle = load_schema_bundle(data_dir)
    totals: Dict[str, Any] = {
        "min_hit": 0,
        "max_hit": 0,
        "accuracy": 0,
        "speed": 0,
        "armor_class_bonus": 0,
        "damage_resistance_bonus": 0,
        "weight": 0,
        "cost": 0,
        "ability_records_seen": 0,
        "equipped_count": 0,
    }
    issues: List[EquipmentIssue] = []
    item_steps: List[Dict[str, Any]] = []

    for item_id in snapshot.equipped_item_ids():
        item = bundle.items.get(item_id)
        if item is None:
            issues.append(EquipmentIssue("warning", "UNKNOWN_EQUIPPED_ITEM", f"Equipped item not found: {item_id}", item_id))
            continue
        raw = item.raw or {}
        combat = item.combat or {}
        defense = item.defense or {}
        economy = item.economy or {}
        abilities = raw.get("abilities") or []
        ability_count = len(abilities) if isinstance(abilities, list) else 0
        step = {
            "item_id": item.runtime_id,
            "name": item.name,
            "legacy_number": item.legacy_number,
            "combat": combat,
            "defense": defense,
            "economy": economy,
            "ability_records_seen": ability_count,
            "note": "Only explicit combat/defense/economy fields are aggregated. Ability semantics remain unpromoted.",
        }
        totals["min_hit"] += _num(combat, "minHit")
        totals["max_hit"] += _num(combat, "maxHit")
        totals["accuracy"] += _num(combat, "accuracy")
        totals["speed"] += _num(combat, "speed")
        totals["armor_class_bonus"] += _num(defense, "armorClassBonus")
        totals["damage_resistance_bonus"] += _num(defense, "damageResistanceBonus")
        totals["weight"] += _num(economy, "weight")
        totals["cost"] += _num(economy, "cost")
        totals["ability_records_seen"] += ability_count
        totals["equipped_count"] += 1
        if ability_count:
            issues.append(EquipmentIssue("info", "ABILITY_SEMANTICS_UNRESOLVED", f"{item.name} has {ability_count} ability records not converted to stat effects.", item.runtime_id))
        item_steps.append(step)

    return EquipmentStatTrace(
        formula_id="equipment_explicit_field_aggregate_v1",
        mode="runtime_calculator",
        classic_accuracy="not_classic_claimed",
        character_id=snapshot.runtime_id,
        equipped_items=snapshot.equipped_item_ids(),
        totals=totals,
        item_steps=item_steps,
        issues=issues,
    )
