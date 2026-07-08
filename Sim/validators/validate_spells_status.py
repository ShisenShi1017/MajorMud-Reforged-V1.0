from __future__ import annotations
from dataclasses import asdict, is_dataclass
from pathlib import Path

from Runtime.adapters import load_schema_bundle
from Runtime.spells.spell_runtime import SpellCastRequest, SpellRuntime, StatusEffectRuntime


def _records(value):
    if isinstance(value, dict):
        seq = list(value.values())
    else:
        seq = list(value or [])
    out = []
    for row in seq:
        if is_dataclass(row):
            out.append(asdict(row))
        elif isinstance(row, dict):
            out.append(row)
    return out


def validate_spells_status(repo_root: Path):
    passed = failed = unknown = 0
    notes = []
    bundle = load_schema_bundle(repo_root / "Data")
    spells = _records(getattr(bundle, "spells", []))
    monsters = _records(getattr(bundle, "monsters", []))

    if spells:
        passed += 1
        notes.append(f"Loaded spell records: {len(spells)}")
    else:
        failed += 1
        notes.append("No spell records loaded from /Data.")

    runtime = SpellRuntime(seed=1100)
    caster = {"id": "validation_caster", "level": 10, "casting_skill": 50, "intelligence": 15, "mana": 100, "cp": 20}
    target = (monsters[0] if monsters else {"id": "validation_target", "level": 3, "resist": 5, "hp": 25})
    if isinstance(target, dict):
        target = {**target, "id": str(target.get("runtime_id") or target.get("id") or target.get("name") or "validation_target"), "level": int(float(target.get("level", 3) or 3)), "resist": int(float(target.get("resist", 5) or 5))}

    damage_spell = {"id": "validation_damage_spell", "name": "Validation Fire Bolt", "effect_type": "damage", "magnitude": 7, "mana_cost": 3}
    damage = runtime.resolve_cast(SpellCastRequest(caster=caster, target=target, spell=damage_spell, seed=1))
    if damage.traces and any(e.get("event_type") == "spell_cast_resolved" for e in damage.events):
        passed += 1
        notes.append(f"Spell cast pipeline PASS; success={damage.success}; hp_delta={damage.hp_delta}; events={len(damage.events)}")
    else:
        failed += 1
        notes.append("Spell cast pipeline did not emit expected trace/events.")

    heal_spell = {"id": "validation_heal_spell", "name": "Validation Heal", "effect_type": "heal", "magnitude": 9, "mana_cost": 4}
    heal = runtime.resolve_cast(SpellCastRequest(caster=caster, target=caster, spell=heal_spell, seed=1))
    if heal.success and heal.hp_delta > 0:
        passed += 1
        notes.append(f"Healing spell pipeline PASS; hp_delta={heal.hp_delta}")
    else:
        failed += 1
        notes.append("Healing spell pipeline failed to produce positive hp_delta.")

    status_spell = {"id": "validation_poison_spell", "name": "Validation Poison", "effect_type": "poison", "magnitude": 2, "duration": 3, "mana_cost": 5}
    status = runtime.resolve_cast(SpellCastRequest(caster=caster, target=target, spell=status_spell, seed=1))
    status_runtime = StatusEffectRuntime()
    for effect in status.status_effects:
        status_runtime.add(effect)
    tick_events = status_runtime.tick()
    expired = 0
    for _ in range(3):
        status_runtime.tick()
    expired = status_runtime.purge_expired()
    if status.success and status.status_effects and tick_events and expired >= 1:
        passed += 1
        notes.append("Status effect apply/tick/expire pipeline PASS.")
    else:
        failed += 1
        notes.append("Status effect lifecycle pipeline failed.")

    unknown += 6
    notes.append("Classic spell success, exact mana/CP costs, durations, resist tables, buff stacking, and status tick timing remain UNKNOWN until evidence-backed.")
    return {"status": "PASS" if failed == 0 else "FAIL", "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
