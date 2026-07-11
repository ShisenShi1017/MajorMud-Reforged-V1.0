#!/usr/bin/env python3
"""Compile the Classic character foundation from Librarium evidence."""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def clean_name(value: str) -> str:
    return re.sub(r"[~\x00-\x1f]+$", "", value).strip()


def normalize(kind: str, value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "_", clean_name(value).lower()).strip("_")
    return f"{kind}.{slug}"


def project_record(kind: str, source_path: Path, root: Path, record: dict) -> dict:
    source_numbers = record.get("sourceNumbers") or {}
    source = record.get("source") or {}
    name = clean_name(record.get("name", ""))
    return {
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


def compile_foundation(root: Path, world: str) -> dict:
    race_src = root / "Librarium" / "Objects" / "races" / "sql_mdb_records_0128.json"
    class_src = root / "Librarium" / "Objects" / "classes" / "sql_mdb_records_0128.json"
    races = [project_record("race", race_src, root, record) for record in load_json(race_src).get("records", [])]
    classes = [project_record("class", class_src, root, record) for record in load_json(class_src).get("records", [])]

    compatibility = []
    for race in races:
        for character_class in classes:
            compatibility.append(
                {
                    "race_id": race["id"],
                    "class_id": character_class["id"],
                    "classification": "unresolved_classic_compatibility",
                    "runtime_allowed": None,
                    "reason": "Playable race and class identities are SQL/MDB-backed; exact Classic race/class compatibility remains blocked until byte-level evidence is promoted.",
                }
            )

    unresolved_fields = [
        "race_class_compatibility",
        "starting_base_stats",
        "starting_hit_points",
        "starting_mana",
        "starting_skills",
        "starting_spells",
        "starting_inventory",
        "starting_equipment",
        "alignment_rules",
    ]

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
        "start_location": {
            "classification": "generated_world_start_room",
            "runtime_resolution": "RuntimeWorld.ResolveGeneratedCharacterStartRoom",
            "fallback_policy": "block instead of guessing when no generated start room exists",
        },
        "blocked_fields": unresolved_fields,
        "rule_evidence": {
            "class_rules": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_CLASS_RULES.json",
            "race_rules": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_RACE_RULES.json",
            "hp_mp_rules": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_HP_MP_GAIN.json",
            "stats": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_STATS.json",
            "training": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_TRAINING.json",
            "xp_level": "Librarium/MajorMUDKnowledgeBase/MechanicsFull/CHAR_XP_LEVEL.json",
        },
        "initialization_policy": {
            "no_guessed_values": True,
            "rule": "Runtime may create a durable shell only from generated catalog identities; unresolved Classic gameplay fields remain blocked and must not be defaulted as verified behavior.",
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
    print(
        "PASS character foundation compile: "
        f"races={foundation['race_count']} classes={foundation['class_count']} "
        f"compatibility={foundation['compatibility_count']} authority={foundation['authority']}"
    )


if __name__ == "__main__":
    main()
