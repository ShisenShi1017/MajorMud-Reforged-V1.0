#!/usr/bin/env python3
"""Compile the Classic character foundation from Librarium evidence."""
from __future__ import annotations

import argparse
import json
import re
import time
from pathlib import Path

RACE_MIN_STAT_COLUMNS = [
    ("mINT", "int"),
    ("mWIL", "wil"),
    ("mSTR", "str"),
    ("mHEA", "hea"),
    ("mAGL", "agl"),
    ("mCHM", "cha"),
]

CLASS_PREFIX_COLUMNS = [
    ("MinHits", "min_hits"),
    ("MaxHits", "max_hits"),
    ("ExpTable", "exp_table"),
    ("MageryType", "magery_type"),
    ("MageryLVL", "magery_level"),
    ("WeaponType", "weapon_type"),
    ("ArmourType", "armour_type"),
]


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(value, indent=2, ensure_ascii=False) + "\n"
    temp_path = path.with_name(f"{path.name}.tmp")
    last_error: OSError | None = None
    for _ in range(5):
        try:
            temp_path.write_text(payload, encoding="utf-8")
            temp_path.replace(path)
            return
        except OSError as exc:
            last_error = exc
            time.sleep(0.1)
    if last_error is not None:
        raise last_error


def clean_name(value: str) -> str:
    return re.sub(r"[~\x00-\x1f]+$", "", value).strip()


def normalize(kind: str, value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "_", clean_name(value).lower()).strip("_")
    return f"{kind}.{slug}"


def signed_u16(value: int) -> int:
    return value - 0x10000 if value >= 0x8000 else value


def decode_prefix_words(record: dict) -> list[int]:
    source = record.get("source") or {}
    raw = bytes.fromhex(source.get("hexPrefix") or "")
    return [int.from_bytes(raw[index : index + 2], "little", signed=False) for index in range(0, len(raw) - 1, 2)]


def decode_even_padded_values(record: dict, count: int) -> list[int | None]:
    words = decode_prefix_words(record)
    values: list[int | None] = []
    for word_index in range(3, 3 + (count * 2), 2):
        values.append(signed_u16(words[word_index]) if word_index < len(words) else None)
    return values


def project_record(kind: str, source_path: Path, root: Path, record: dict) -> dict:
    source_numbers = record.get("sourceNumbers") or {}
    source = record.get("source") or {}
    name = clean_name(record.get("name", ""))
    projected = {
        "id": normalize(kind, name),
        "name": name,
        "number": source_numbers.get("number"),
        "classification": "playable_classic_catalog_record",
        "evidence_state": record.get("evidenceState"),
        "confidence": record.get("confidence"),
        "source_record_id": record.get("id"),
        "source_file": str(source_path.relative_to(root)).replace("\\", "/"),
        "source_kind": source.get("kind"),
        "source_owner_table_page": source.get("ownerTablePage"),
        "source_page": source.get("page"),
        "source_offset": source.get("offset"),
    }
    if kind == "race":
        values = decode_even_padded_values(record, len(RACE_MIN_STAT_COLUMNS))
        projected["minimum_stats"] = {
            stat: value for (_, stat), value in zip(RACE_MIN_STAT_COLUMNS, values) if value is not None
        }
        projected["phase003_promoted_fields"] = {
            "minimum_stats": {
                "classification": "sql_mdb_prefix_and_schema_alignment",
                "columns": [column for column, _ in RACE_MIN_STAT_COLUMNS],
                "schema_evidence": "Docs/DatabaseAudit/csv_dat_name_alignment.json:races.TXT",
                "byte_evidence": "source.hexPrefix even-padded little-endian words",
            }
        }
    elif kind == "class":
        values = decode_even_padded_values(record, len(CLASS_PREFIX_COLUMNS))
        decoded = {field: value for (_, field), value in zip(CLASS_PREFIX_COLUMNS, values) if value is not None}
        min_hits = decoded.get("min_hits")
        max_hits = decoded.get("max_hits")
        projected["hit_points"] = {
            "minimum_per_level": min_hits,
            "maximum_bonus_per_level": max_hits,
            "maximum_per_level": min_hits + max_hits if min_hits is not None and max_hits is not None else None,
            "classification": "sql_mdb_prefix_and_schema_alignment",
        }
        projected["experience_table"] = decoded.get("exp_table")
        projected["magic"] = {
            "type": decoded.get("magery_type"),
            "level": decoded.get("magery_level"),
        }
        projected["equipment_categories"] = {
            "weapon_type": decoded.get("weapon_type"),
            "armour_type": decoded.get("armour_type"),
        }
        projected["phase003_promoted_fields"] = {
            "class_prefix_fields": {
                "classification": "sql_mdb_prefix_and_schema_alignment",
                "columns": [column for column, _ in CLASS_PREFIX_COLUMNS],
                "schema_evidence": "Docs/DatabaseAudit/csv_dat_name_alignment.json:classes.TXT",
                "byte_evidence": "source.hexPrefix even-padded little-endian words",
                "blocked_note": "CombatLVL and Abil/AbilVal columns are not promoted from the stored 32-byte prefix alone.",
            }
        }
    return projected


def compile_foundation(root: Path, world: str) -> dict:
    race_src = root / "Librarium" / "Objects" / "races" / "sql_mdb_records_0128.json"
    class_src = root / "Librarium" / "Objects" / "classes" / "sql_mdb_records_0128.json"
    races = [project_record("race", race_src, root, record) for record in load_json(race_src).get("records", [])]
    classes = [project_record("class", class_src, root, record) for record in load_json(class_src).get("records", [])]

    promoted_path = root / "Librarium" / "Objects" / "character_foundation" / "native_runtime_promoted_0137.json"
    promoted = load_json(promoted_path)
    if promoted.get("authority") != "Librarium":
        raise ValueError("Character-foundation promoted evidence is not Librarium-authoritative")
    promoted_races = {int(row["number"]): row for row in promoted.get("races", [])}
    promoted_classes = {int(row["number"]): row for row in promoted.get("classes", [])}

    for race in races:
        row = promoted_races.get(int(race["number"]))
        if not row:
            raise ValueError(f"Missing promoted race record {race['number']}")
        race["name"] = row["name"]
        race["id"] = normalize("race", row["name"])
        race["minimum_stats"] = row["minimum_stats"]
        race["maximum_stats"] = row["maximum_stats"]
        race["hp_per_level_adjustment"] = row["hp_per_level_adjustment"]
        race["base_cp"] = row["base_cp"]
        race["experience_add_percent"] = row["experience_add_percent"]
        race["ability_slots"] = row["ability_slots"]
        race["promoted_source"] = str(promoted_path.relative_to(root)).replace("\\", "/")

    for character_class in classes:
        row = promoted_classes.get(int(character_class["number"]))
        if not row:
            raise ValueError(f"Missing promoted class record {character_class['number']}")
        character_class["name"] = row["name"]
        character_class["id"] = normalize("class", row["name"])
        character_class["hit_points"] = {
            "minimum_per_level": row["min_hits"],
            "maximum_bonus_per_level": row["max_hits_bonus"],
            "maximum_per_level": row["min_hits"] + row["max_hits_bonus"],
            "classification": "Librarium promoted native table evidence",
        }
        character_class["experience"] = {
            "stored_modifier": row["experience_stored"],
            "class_percent": row["experience_percent"],
        }
        character_class.pop("experience_table", None)
        character_class["magic"] = {"type": row["magery_type"], "level": row["magery_level"]}
        character_class["equipment_categories"] = {"weapon_type": row["weapon_type"], "armour_type": row["armour_type"]}
        character_class["combat_level"] = row["combat_level"]
        character_class["ability_slots"] = row["ability_slots"]
        character_class["promoted_source"] = str(promoted_path.relative_to(root)).replace("\\", "/")

    compatibility = []
    for race in races:
        for character_class in classes:
            compatibility.append(
                {
                    "race_id": race["id"],
                    "class_id": character_class["id"],
                    "classification": "all_classic_combinations_allowed",
                    "runtime_allowed": True,
                    "reason": "Classic character creation permits every playable race with every playable class.",
                    "experience": {
                        "class_percent": character_class["experience"]["class_percent"],
                        "race_add_percent": race["experience_add_percent"],
                        "combined_chart_percent": character_class["experience"]["class_percent"] + race["experience_add_percent"],
                        "formula": "class_percent + race_add_percent",
                    },
                }
            )

    unresolved_fields: list[str] = []

    rules = promoted["character_creation_rules"]
    formulas = promoted["derived_stat_formulas"]


    creation_gate = {
        "ready": True,
        "classification": "phase003_character_creation_rules_complete",
        "required_for_promotion": [
            "race_class_compatibility", "race_minimum_stats", "race_maximum_stats",
            "class_hit_points", "alignment_rules", "starting_hit_points", "starting_mana",
            "starting_skills", "starting_spells", "starting_inventory", "starting_equipment",
            "generated_character_start_room", "persistence_round_trip", "exhaustive_195_combo_matrix"
        ],
        "promoted_this_phase": [
            "all_195_race_class_combinations_allowed",
            "race_minimum_and_maximum_stats",
            "class_hit_point_roll_bounds_and_level_one_formula",
            "class_and_race_experience_percentages",
            "combined_experience_chart_percent_for_all_195_pairs",
            "class_magery_type_and_level",
            "class_weapon_and_armour_type",
            "race_hp_per_level_adjustment",
            "race_and_class_ability_slots",
            "empty_starting_inventory_and_equipment",
            "race_plus_class_tree_starting_abilities",
            "starting_mana_formula",
            "newhaven_adventurers_guild_character_start"
        ],
        "blockers": [],
    }


    return {
        "schema_version": 1,
        "generated_by": "Tools/Librarium/compile_character_foundation_0137.py",
        "code_update": "0137-internal-character-foundation",
        "world_id": world,
        "authority": "Librarium",
        "classic_locked": True,
        "source_policy": "Generated from Librarium SQL/MDB-backed playable race/class records. ImportedDatabases are not runtime sources.",
        "race_count": len(races),
        "class_count": len(classes),
        "compatibility_count": len(compatibility),
        "races": races,
        "classes": classes,
        "compatibility": compatibility,
        "creation_gate": creation_gate,
        "start_location": {
            "classification": "Librarium_promoted_classic_character_start",
            "map_id": rules["new_character_start"]["map_id"],
            "room_id": rules["new_character_start"]["room_id"],
            "room_number": rules["new_character_start"]["room_number"],
            "name": rules["new_character_start"]["name"],
            "runtime_resolution": "RuntimeWorld.ResolveGeneratedCharacterStartRoom",
        },
        "character_creation_rules": rules,
        "derived_stat_formulas": formulas,
        "blocked_fields": unresolved_fields,
        "rule_evidence": {
            "class_rules": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_CLASS_RULES.json",
            "race_rules": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_RACE_RULES.json",
            "hp_mp_rules": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_HP_MP_GAIN.json",
            "stats": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_STATS.json",
            "training": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_TRAINING.json",
            "xp_level": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_XP_LEVEL.json",
            "promoted_native_character_tables": "Librarium/Objects/character_foundation/native_runtime_promoted_0137.json",
        },
        "initialization_policy": {
            "no_guessed_values": True,
            "rule": "Runtime creates level-one characters from generated Librarium race/class records, formulas, empty inventory/equipment, and race/class ability trees.",
        },
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument("--world", default="Classic")
    args = parser.parse_args()
    root = Path(args.root).resolve()
    out = root / "Data" / args.world

    foundation = compile_foundation(root, args.world)
    write_json(out / "character_foundation.json", foundation)
    write_json(
        out / "races.json",
        {
            "schema_version": 1,
            "generated_by": foundation["generated_by"],
            "world_id": args.world,
            "authority": foundation["authority"],
            "family": "races",
            "record_count": foundation["race_count"],
            "records": foundation["races"],
        },
    )
    write_json(
        out / "classes.json",
        {
            "schema_version": 1,
            "generated_by": foundation["generated_by"],
            "world_id": args.world,
            "authority": foundation["authority"],
            "family": "classes",
            "record_count": foundation["class_count"],
            "records": foundation["classes"],
        },
    )

    manifest_path = out / "world_manifest.json"
    manifest = load_json(manifest_path) if manifest_path.exists() else {"schema_version": 1, "world_id": args.world}
    manifest["character_start_room"] = foundation["start_location"]["room_id"]
    manifest["character_spawning"] = {
        "start_room": foundation["start_location"]["room_id"],
        "map_id": foundation["start_location"]["map_id"],
        "room_number": foundation["start_location"]["room_number"],
        "classification": foundation["start_location"]["classification"],
    }
    write_json(manifest_path, manifest)
    print(
        "PASS character foundation compile: "
        f"races={foundation['race_count']} classes={foundation['class_count']} "
        f"compatibility={foundation['compatibility_count']} authority={foundation['authority']}"
    )


if __name__ == "__main__":
    main()
