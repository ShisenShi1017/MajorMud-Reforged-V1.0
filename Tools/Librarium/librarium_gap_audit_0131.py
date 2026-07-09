#!/usr/bin/env python3
"""CodeUpdate0131 Librarium gap audit and generated Classic server smoke prep.

This is intentionally no-runtime: it audits the Librarium-generated Data/Classic
payload that the .NET server smoke path is expected to load.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Dict, Iterable, List, Tuple

REQUIRED_RUNTIME_FAMILIES = [
    "rooms", "exits", "spawns", "npcs", "items", "spells", "formulas", "shops", "quests", "world_manifest", "compile_manifest"
]

OBJECT_FAMILIES = [
    "ai_profiles", "areas", "banks", "classes", "commands", "economy_rules", "equipment", "events",
    "exits", "formulas", "guilds", "items", "loot_rules", "npcs", "quests", "races", "regions",
    "rooms", "scripts", "shops", "skills", "spawns", "spells", "status_effects", "triggers", "world_overlays", "worlds"
]


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def records(doc: Any) -> List[Dict[str, Any]]:
    if isinstance(doc, list):
        return [r for r in doc if isinstance(r, dict)]
    if isinstance(doc, dict) and isinstance(doc.get("records"), list):
        return [r for r in doc["records"] if isinstance(r, dict)]
    if isinstance(doc, dict):
        return [doc]
    return []


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def has_runtime_mapping(record: Dict[str, Any]) -> bool:
    mapping = record.get("runtime_mapping")
    return isinstance(mapping, dict) and bool(mapping.get("exports_to")) and mapping.get("runtime_reads_generated_data") is True


def has_generator_mapping(record: Dict[str, Any]) -> bool:
    mapping = record.get("generator_mapping_0124") or record.get("generator_mapping")
    return isinstance(mapping, dict) and bool(mapping.get("export_path") or mapping.get("output_collection"))


def has_evidence(record: Dict[str, Any]) -> bool:
    ev = record.get("evidence_refs") or record.get("evidence") or record.get("sources")
    if isinstance(ev, list):
        return len(ev) > 0
    if isinstance(ev, dict):
        return len(ev) > 0
    return False


def is_placeholder(record: Dict[str, Any]) -> bool:
    markers = [
        str(record.get("status", "")), str(record.get("confidence", "")),
        str(record.get("runtime_mapping", {}).get("status", "")) if isinstance(record.get("runtime_mapping"), dict) else "",
        str(record.get("name", "")), str(record.get("description", "")), str(record.get("id", "")),
    ]
    joined = " ".join(markers).lower()
    return any(token in joined for token in ["placeholder", "stub", "template_only", "pending"])


def id_set(family_records: Iterable[Dict[str, Any]]) -> set[str]:
    return {str(r.get("id")) for r in family_records if r.get("id")}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".", help="Repository root")
    parser.add_argument("--world", default="Classic", help="Generated world under Data/<world>")
    parser.add_argument("--out", default="Reports/CodeUpdate0131/librarium_gap_audit_0131.json")
    parser.add_argument("--md", default="Reports/CodeUpdate0131/LIBRARIUM_GAP_AUDIT_0131.md")
    args = parser.parse_args()

    repo = Path(args.root).resolve()
    data_root = repo / "Data" / args.world
    out = repo / args.out
    out_md = repo / args.md
    out.parent.mkdir(parents=True, exist_ok=True)

    diagnostics: List[Dict[str, Any]] = []
    families: Dict[str, Dict[str, Any]] = {}
    loaded: Dict[str, List[Dict[str, Any]]] = {}

    if not data_root.exists():
        diagnostics.append({"severity": "fatal", "code": "DATA_ROOT_MISSING", "message": str(data_root)})
    else:
        for path in sorted(data_root.glob("*.json")):
            try:
                doc = load_json(path)
                recs = records(doc)
                loaded[path.stem] = recs
                families[path.stem] = {
                    "file": str(path.relative_to(repo)),
                    "sha256": sha256(path),
                    "record_count": len(recs),
                    "records_with_id": sum(1 for r in recs if r.get("id")),
                    "records_with_evidence": sum(1 for r in recs if has_evidence(r)),
                    "records_with_runtime_mapping": sum(1 for r in recs if has_runtime_mapping(r)),
                    "records_with_generator_mapping": sum(1 for r in recs if has_generator_mapping(r)),
                    "placeholder_or_pending_records": sum(1 for r in recs if is_placeholder(r)),
                }
            except Exception as exc:
                diagnostics.append({"severity": "error", "code": "JSON_READ_FAILED", "file": str(path.relative_to(repo)), "message": str(exc)})

    for family in REQUIRED_RUNTIME_FAMILIES:
        if family not in families:
            diagnostics.append({"severity": "error", "code": "REQUIRED_FAMILY_MISSING", "family": family})
        elif families[family]["record_count"] <= 0:
            diagnostics.append({"severity": "error", "code": "REQUIRED_FAMILY_EMPTY", "family": family})

    # Relationship integrity checks for the current generated Classic data.
    rooms = id_set(loaded.get("rooms", []))
    spawns = id_set(loaded.get("spawns", []))
    npcs = id_set(loaded.get("npcs", []))
    formulas = id_set(loaded.get("formulas", []))
    spells = id_set(loaded.get("spells", []))
    items = id_set(loaded.get("items", []))

    rel_checks = {
        "room_spawn_refs_checked": 0,
        "room_spawn_refs_missing": 0,
        "spawn_room_refs_checked": 0,
        "spawn_room_refs_missing": 0,
        "npc_formula_refs_checked": 0,
        "npc_formula_refs_missing": 0,
        "spell_formula_refs_checked": 0,
        "spell_formula_refs_missing": 0,
    }

    for r in loaded.get("rooms", []):
        for sid in r.get("spawns", []) if isinstance(r.get("spawns"), list) else []:
            rel_checks["room_spawn_refs_checked"] += 1
            if str(sid) not in spawns:
                rel_checks["room_spawn_refs_missing"] += 1
                diagnostics.append({"severity": "warning", "code": "ROOM_SPAWN_REF_MISSING", "room": r.get("id"), "spawn": sid})
    for s in loaded.get("spawns", []):
        rr = s.get("room_ref")
        if rr:
            rel_checks["spawn_room_refs_checked"] += 1
            if str(rr) not in rooms:
                rel_checks["spawn_room_refs_missing"] += 1
                diagnostics.append({"severity": "warning", "code": "SPAWN_ROOM_REF_MISSING", "spawn": s.get("id"), "room": rr})
    for n in loaded.get("npcs", []):
        for fid in n.get("formula_refs", []) if isinstance(n.get("formula_refs"), list) else []:
            rel_checks["npc_formula_refs_checked"] += 1
            if str(fid) not in formulas:
                rel_checks["npc_formula_refs_missing"] += 1
                diagnostics.append({"severity": "warning", "code": "NPC_FORMULA_REF_MISSING", "npc": n.get("id"), "formula": fid})
    for sp in loaded.get("spells", []):
        for fid in sp.get("formula_refs", []) if isinstance(sp.get("formula_refs"), list) else []:
            rel_checks["spell_formula_refs_checked"] += 1
            if str(fid) not in formulas:
                rel_checks["spell_formula_refs_missing"] += 1
                diagnostics.append({"severity": "warning", "code": "SPELL_FORMULA_REF_MISSING", "spell": sp.get("id"), "formula": fid})

    manifest = loaded.get("world_manifest", [{}])[0] if loaded.get("world_manifest") else {}
    compile_manifest = loaded.get("compile_manifest", [{}])[0] if loaded.get("compile_manifest") else {}

    summary = {
        "code_update": "0131",
        "purpose": "Librarium gap audit and server smoke-test prep for generated Classic data",
        "world": args.world,
        "data_root": str(data_root.relative_to(repo)) if data_root.exists() else str(data_root),
        "families_indexed": len(families),
        "records_indexed": sum(f["record_count"] for f in families.values()),
        "required_runtime_families": REQUIRED_RUNTIME_FAMILIES,
        "relationship_checks": rel_checks,
        "missing_relationship_count": sum(v for k, v in rel_checks.items() if k.endswith("_missing")),
        "diagnostic_count": len(diagnostics),
        "error_count": sum(1 for d in diagnostics if d.get("severity") in {"error", "fatal"}),
        "warning_count": sum(1 for d in diagnostics if d.get("severity") == "warning"),
        "status": "PASS_WITH_GAPS" if diagnostics and all(d.get("severity") == "warning" for d in diagnostics) else ("PASS" if not diagnostics else "FAIL"),
        "world_manifest_code_update": manifest.get("code_update"),
        "compile_manifest_code_update": compile_manifest.get("code_update"),
        "families": families,
        "diagnostics": diagnostics[:500],
        "diagnostics_truncated": len(diagnostics) > 500,
        "server_smoke_commands": [
            "dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj",
            "dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data",
            "dotnet run --project Apps/Source/Reforged.Server -- --run-local"
        ]
    }

    out.write_text(json.dumps(summary, indent=2, sort_keys=True), encoding="utf-8")

    lines = [
        "# CodeUpdate0131 Librarium Gap Audit + Server Smoke Test Prep",
        "",
        f"Status: **{summary['status']}**",
        "",
        "## Summary",
        f"- World: `{args.world}`",
        f"- Data root: `{summary['data_root']}`",
        f"- Families indexed: **{summary['families_indexed']}**",
        f"- Records indexed: **{summary['records_indexed']}**",
        f"- Relationship references checked: **{sum(v for k, v in rel_checks.items() if k.endswith('_checked'))}**",
        f"- Missing relationship references: **{summary['missing_relationship_count']}**",
        f"- Warnings: **{summary['warning_count']}**",
        f"- Errors: **{summary['error_count']}**",
        "",
        "## Server smoke commands for Codex",
        "```bash",
        *summary["server_smoke_commands"],
        "```",
        "",
        "## Family readiness",
        "| Family | Records | IDs | Evidence | Runtime mapping | Generator mapping | Pending/placeholder |",
        "|---|---:|---:|---:|---:|---:|---:|",
    ]
    for name, fam in sorted(families.items()):
        lines.append(f"| {name} | {fam['record_count']} | {fam['records_with_id']} | {fam['records_with_evidence']} | {fam['records_with_runtime_mapping']} | {fam['records_with_generator_mapping']} | {fam['placeholder_or_pending_records']} |")
    lines += [
        "",
        "## Relationship checks",
        "```json",
        json.dumps(rel_checks, indent=2, sort_keys=True),
        "```",
        "",
        "## Interpretation",
        "This audit does not prove full gameplay. It proves the generated Classic payload is structurally inspectable before the .NET server smoke test. Any warning here is a Librarium completion target; any error blocks server boot readiness.",
    ]
    out_md.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({k: summary[k] for k in ["status", "families_indexed", "records_indexed", "missing_relationship_count", "warning_count", "error_count"]}, indent=2))
    return 0 if summary["error_count"] == 0 else 1

if __name__ == "__main__":
    raise SystemExit(main())
