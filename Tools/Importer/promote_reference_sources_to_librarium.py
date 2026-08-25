#!/usr/bin/env python3
"""Promote structured MMExplorer and OmniVerse reference data into Librarium.

The promotion is conservative:
- matched canonical records receive nested provenance blocks;
- unmatched MMExplorer SQL rows are written as supplement files, not canonical
  replacements;
- OmniVerse public data fills gaps as reference-backed fields and does not
  overwrite binary-backed/source-priority fields;
- abilities are added as a new Librarium object family because no active
  ability family exists yet.
"""
from __future__ import annotations

import argparse
import json
import time
from pathlib import Path
from typing import Any


PROMOTION_SCOPE = "mmexplorer_omniverse_librarium_reconciliation"
OMNI_ROOT = Path("ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc")
MMX_SQL_ROOT = Path("Librarium/SourceImports/MMExplorerSQLData")


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(data, indent=2) + "\n"
    last_error: OSError | None = None
    for _ in range(5):
        try:
            path.write_text(payload, encoding="utf-8")
            return
        except OSError as exc:
            last_error = exc
            time.sleep(0.5)
    if last_error is not None:
        raise last_error


def extract_records(root: Any) -> list[dict[str, Any]]:
    if isinstance(root, list):
        return root
    if not isinstance(root, dict):
        return []
    for key in ("records", "items", "objects", "catalog", "entries", "races", "classes"):
        value = root.get(key)
        if isinstance(value, list):
            return value
    return []


def by_legacy(records: list[dict[str, Any]], key: str = "legacy_id") -> dict[str, dict[str, Any]]:
    out: dict[str, dict[str, Any]] = {}
    for record in records:
        value = record.get(key)
        if value is None:
            value = record.get("legacy_number")
        if value is None:
            value = record.get("number")
        if value is not None:
            out[str(value)] = record
    return out


def selected_source(repo: Path, family: str, filename: str) -> Path:
    return repo / "Librarium" / "Objects" / family / filename


def public_reference(source_file: str, row: dict[str, Any], classification: str) -> dict[str, Any]:
    return {
        "source": source_file.replace("\\", "/"),
        "source_kind": "OmniVerse public codex snapshot",
        "classification": classification,
        "promotion_scope": PROMOTION_SCOPE,
        "confidence": "reference_fill_public_snapshot",
        "override_policy": "does_not_override_binary_or_higher_priority_librarium_fields",
        "fields": row,
    }


def mmexplorer_reference(source_file: str, row: dict[str, Any]) -> dict[str, Any]:
    return {
        "source": source_file.replace("\\", "/"),
        "source_kind": "MMExplorer SQL/MDB extracted row",
        "classification": "structured_reference_fill",
        "promotion_scope": PROMOTION_SCOPE,
        "confidence": row.get("confidence", "sql_mdb_extracted_identity_and_relationships"),
        "override_policy": "does_not_override_binary_or_higher_priority_librarium_fields",
        "source_numbers": row.get("sourceNumbers", {}),
        "printable_segments": row.get("printableSegments", []),
        "relationships": row.get("relationships", []),
        "source_trace": row.get("source", {}),
    }


def promote_omni_family(
    repo: Path,
    family: str,
    librarium_file: str,
    omni_file: str,
    classification: str,
) -> dict[str, int]:
    path = selected_source(repo, family, librarium_file)
    root = load_json(path)
    records = extract_records(root)
    records_by_id = by_legacy(records)

    omni_path = repo / OMNI_ROOT / omni_file
    omni_rows = load_json(omni_path)
    matched = 0
    unmatched = 0

    for row in omni_rows:
        key = str(row.get("legacy_number"))
        record = records_by_id.get(key)
        if record is None:
            unmatched += 1
            continue
        record["public_reference"] = public_reference(str(OMNI_ROOT / omni_file), row, classification)
        refs = record.setdefault("evidence_refs", [])
        if isinstance(refs, list) and not any(ref.get("source") == "OmniVerseCodexApi" and ref.get("row_key") == key for ref in refs if isinstance(ref, dict)):
            refs.append({
                "source": "OmniVerseCodexApi",
                "path": str(OMNI_ROOT / omni_file).replace("\\", "/"),
                "row_key": key,
                "classification": classification,
                "promotion_scope": PROMOTION_SCOPE,
            })
        matched += 1

    write_json(path, root)
    return {"matched": matched, "unmatched": unmatched, "librarium_records": len(records), "source_records": len(omni_rows)}


def promote_mmx_family(repo: Path, family: str, librarium_file: str, sql_file: str) -> dict[str, int]:
    path = selected_source(repo, family, librarium_file)
    root = load_json(path)
    records = extract_records(root)
    records_by_id = by_legacy(records)

    sql_path = repo / "Librarium" / "Objects" / family / sql_file
    sql_root = load_json(sql_path)
    sql_rows = extract_records(sql_root)
    matched = 0
    unmatched_records: list[dict[str, Any]] = []

    for row in sql_rows:
        number = row.get("sourceNumbers", {}).get("number")
        key = str(number) if number is not None else ""
        record = records_by_id.get(key)
        if record is None:
            promoted = dict(row)
            promoted["id"] = f"mmexplorer_unmatched_{family}.{key or row.get('id', len(unmatched_records) + 1)}"
            promoted["object_type"] = family[:-1] if family.endswith("s") else family
            promoted["canonical_status"] = "reference_supplement_unmatched_to_active_librarium_record"
            promoted["promotion_scope"] = PROMOTION_SCOPE
            unmatched_records.append(promoted)
            continue
        record["mmexplorer_sql_reference"] = mmexplorer_reference(
            f"Librarium/Objects/{family}/{sql_file}",
            row,
        )
        matched += 1

    if unmatched_records:
        supplement = {
            "schema": "mmexplorer_unmatched_reference_supplement_v1",
            "promotion_scope": PROMOTION_SCOPE,
            "family": family,
            "source": f"Librarium/Objects/{family}/{sql_file}",
            "record_count": len(unmatched_records),
            "records": unmatched_records,
        }
        write_json(repo / "Librarium" / "Objects" / family / f"mmexplorer_unmatched_{family}_supplement.json", supplement)

    write_json(path, root)
    return {"matched": matched, "unmatched": len(unmatched_records), "librarium_records": len(records), "source_records": len(sql_rows)}


def promote_character_foundation(repo: Path) -> dict[str, int]:
    path = repo / "Librarium" / "Objects" / "character_foundation" / "native_runtime_promoted_0137.json"
    root = load_json(path)
    race_rows = load_json(repo / OMNI_ROOT / "codex-race-wcc.json")
    class_rows = load_json(repo / OMNI_ROOT / "codex-class-wcc.json")
    race_by_id = {str(row["legacy_number"]): row for row in race_rows}
    class_by_id = {str(row["legacy_number"]): row for row in class_rows}

    race_matches = 0
    for race in root.get("races", []):
        row = race_by_id.get(str(race.get("number")))
        if row:
            race["public_reference"] = public_reference(str(OMNI_ROOT / "codex-race-wcc.json"), row, "race_public_reference")
            race_matches += 1

    class_matches = 0
    for klass in root.get("classes", []):
        row = class_by_id.get(str(klass.get("number")))
        if row:
            klass["public_reference"] = public_reference(str(OMNI_ROOT / "codex-class-wcc.json"), row, "class_public_reference")
            class_matches += 1

    write_json(path, root)
    return {"races_matched": race_matches, "classes_matched": class_matches}


def promote_abilities(repo: Path) -> dict[str, int]:
    rows = load_json(repo / OMNI_ROOT / "codex-ability-wcc.json")
    records = []
    for row in rows:
        legacy = str(row.get("legacy_number"))
        records.append({
            "id": f"ability.{legacy}",
            "legacy_id": legacy,
            "name": row.get("name", ""),
            "object_type": "ability",
            "world_scope": ["classic"],
            "public_reference": public_reference(str(OMNI_ROOT / "codex-ability-wcc.json"), row, "ability_public_reference"),
            "evidence_refs": [{
                "source": "OmniVerseCodexApi",
                "path": str(OMNI_ROOT / "codex-ability-wcc.json").replace("\\", "/"),
                "row_key": legacy,
                "classification": "ability_public_reference",
                "promotion_scope": PROMOTION_SCOPE,
            }],
            "runtime_mapping": {
                "exports_to": "/Data/<world>/abilities.json",
                "status": "reference_ready_for_generator",
                "source": "Librarium/Objects/abilities",
                "generated_only": True,
                "runtime_reads_generated_data": True,
            },
            "confidence": "reference_fill_public_snapshot",
        })

    root = {
        "schema": "librarium_ability_records_v1",
        "promotion_scope": PROMOTION_SCOPE,
        "source": str(OMNI_ROOT / "codex-ability-wcc.json").replace("\\", "/"),
        "record_count": len(records),
        "records": records,
    }
    write_json(repo / "Librarium" / "Objects" / "abilities" / "ability_records.json", root)
    return {"records": len(records)}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".", help="Repository root")
    args = parser.parse_args()
    repo = Path(args.repo).resolve()

    report: dict[str, Any] = {
        "promotion_scope": PROMOTION_SCOPE,
        "mode": "mmexplorer_omniverse_librarium_reconciliation",
        "policy": "gap-fill/reference promotion; no binary-backed field overwrites",
        "omni_promotions": {},
        "mmexplorer_promotions": {},
    }

    for family, lib_file, omni_file, classification in [
        ("items", "item_records_0122.json", "codex-item-wcc.json", "item_public_reference"),
        ("spells", "spell_records_0122.json", "codex-spell-wcc.json", "spell_public_reference"),
        ("npcs", "npc_records_0122.json", "codex-monster-wcc.json", "npc_public_reference"),
        ("shops", "shop_records_0122.json", "codex-shop-wcc.json", "shop_public_reference"),
    ]:
        report["omni_promotions"][family] = promote_omni_family(repo, family, lib_file, omni_file, classification)

    for family, lib_file, sql_file in [
        ("items", "item_records_0122.json", "sql_mdb_records_0128.json"),
        ("spells", "spell_records_0122.json", "sql_mdb_records_0128.json"),
        ("npcs", "npc_records_0122.json", "sql_mdb_records_0128.json"),
        ("shops", "shop_records_0122.json", "sql_mdb_records_0128.json"),
    ]:
        report["mmexplorer_promotions"][family] = promote_mmx_family(repo, family, lib_file, sql_file)

    report["character_foundation"] = promote_character_foundation(repo)
    report["abilities"] = promote_abilities(repo)

    report_path = repo / "Reports" / "Generated" / "mmexplorer_omniverse_librarium_reconciliation.json"
    write_json(report_path, report)

    md_lines = [
        "# MMExplorer and OmniVerse Librarium Reconciliation",
        "",
        "This report records structured reference promotion into Librarium.",
        "",
        "- MMExplorer SQL/MDB rows are promoted as matched reference blocks or unmatched supplement records.",
        "- OmniVerse codex rows are promoted as public reference blocks and do not overwrite binary-backed fields.",
        "- Ability records are added as a new Librarium family from OmniVerse public codex data.",
        "",
        "## OmniVerse Promotions",
        "",
        "| Family | Matched | Unmatched | Source Records |",
        "| --- | ---: | ---: | ---: |",
    ]
    for family, stats in report["omni_promotions"].items():
        md_lines.append(f"| {family} | {stats['matched']} | {stats['unmatched']} | {stats['source_records']} |")
    md_lines.extend([
        "",
        "## MMExplorer SQL/MDB Promotions",
        "",
        "| Family | Matched | Unmatched supplements | Source Records |",
        "| --- | ---: | ---: | ---: |",
    ])
    for family, stats in report["mmexplorer_promotions"].items():
        md_lines.append(f"| {family} | {stats['matched']} | {stats['unmatched']} | {stats['source_records']} |")
    md_lines.extend([
        "",
        f"- Character foundation race matches: {report['character_foundation']['races_matched']}.",
        f"- Character foundation class matches: {report['character_foundation']['classes_matched']}.",
        f"- Ability records created: {report['abilities']['records']}.",
        "",
        "Raw details: `Reports/Generated/mmexplorer_omniverse_librarium_reconciliation.json`",
        "",
    ])
    report_path.with_suffix(".md").write_text("\n".join(md_lines), encoding="utf-8")

    print("MMExplorer/OmniVerse Librarium reconciliation complete")
    print(f"Report: {report_path.relative_to(repo)}")
    print(f"Markdown: {report_path.with_suffix('.md').relative_to(repo)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
