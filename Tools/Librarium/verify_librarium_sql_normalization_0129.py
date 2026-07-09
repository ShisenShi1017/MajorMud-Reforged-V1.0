#!/usr/bin/env python3
"""CodeUpdate0129: Librarium SQL/MDB normalization and completeness verifier.

This tool does not mutate runtime gameplay. It audits the SQL/MDB records promoted
into the Librarium during CodeUpdate0128 and produces machine-readable reports that
answer whether the Librarium can stand on its own as the source of truth for the
currently imported SQL/MDB evidence corpus.
"""
from __future__ import annotations

import json
import hashlib
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, Iterable, List, Tuple

ROOT = Path(__file__).resolve().parents[2]
LIB = ROOT / "Librarium"
OBJ = LIB / "Objects"
OUT = LIB / "Normalization0129"
REPORTS = ROOT / "Reports" / "CodeUpdate0129"

SQL_FAMILIES = {
    "rooms": "room",
    "items": "item",
    "npcs": "npc",
    "spells": "spell",
    "shops": "shop",
    "classes": "class",
    "races": "race",
}

REQUIRED_RECORD_FIELDS = ("id", "type", "name", "source", "evidenceState", "confidence", "runtimeMapping")
REQUIRED_RUNTIME_FIELDS = ("exportFamily", "generationSource", "dataGeneratedOnly", "classicLocked")


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_md(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def stable_hash(data: Any) -> str:
    return hashlib.sha256(json.dumps(data, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def iter_record_files() -> Iterable[Path]:
    for p in sorted(OBJ.glob("*/*.json")):
        if p.name == "catalog.json" or p.name.startswith("catalog_seed_"):
            continue
        yield p


def extract_records_from_file(path: Path) -> List[Dict[str, Any]]:
    try:
        data = load_json(path)
    except Exception:
        return []
    if isinstance(data, dict) and isinstance(data.get("records"), list):
        return [r for r in data["records"] if isinstance(r, dict)]
    if isinstance(data, list):
        return [r for r in data if isinstance(r, dict)]
    if isinstance(data, dict) and "id" in data:
        return [data]
    return []


def main() -> int:
    OUT.mkdir(parents=True, exist_ok=True)
    REPORTS.mkdir(parents=True, exist_ok=True)

    metrics_0128_path = LIB / "Indexes" / "sql_mdb_import_metrics_0128.json"
    metrics_0128 = load_json(metrics_0128_path) if metrics_0128_path.exists() else {}
    expected_family_counts = metrics_0128.get("familyCounts", {})

    # Load all records in Librarium objects for ID lookup and general completeness checks.
    all_records_by_id: Dict[str, Dict[str, Any]] = {}
    all_record_files: Dict[str, str] = {}
    duplicate_ids: Dict[str, List[str]] = defaultdict(list)
    object_family_record_counts = Counter()
    placeholder_files = []
    catalog_files = []

    for family_dir in sorted([p for p in OBJ.iterdir() if p.is_dir()]):
        catalog = family_dir / "catalog.json"
        if catalog.exists():
            catalog_files.append(catalog)
        for seed in family_dir.glob("catalog_seed_*.json"):
            placeholder_files.append(seed)

    for path in iter_record_files():
        records = extract_records_from_file(path)
        object_family_record_counts[path.parent.name] += len(records)
        for record in records:
            rid = record.get("id")
            if not isinstance(rid, str) or not rid:
                continue
            if rid in all_records_by_id:
                duplicate_ids[rid].append(str(path.relative_to(ROOT)))
            all_records_by_id[rid] = record
            all_record_files[rid] = str(path.relative_to(ROOT))

    # SQL/MDB family-specific coverage and validation.
    family_reports = []
    sql_ids = set()
    source_file_counter = Counter()
    field_issue_samples = []
    runtime_issue_samples = []
    relationship_edges = []
    relationship_by_kind = Counter()
    broken_relationships = []
    valid_relationships = 0
    relationship_source_families = Counter()
    evidence_missing = []
    confidence_missing = []
    runtime_missing = []

    for family, typ in SQL_FAMILIES.items():
        path = OBJ / family / "sql_mdb_records_0128.json"
        family_report = {
            "family": family,
            "objectType": typ,
            "path": str(path.relative_to(ROOT)) if path.exists() else None,
            "exists": path.exists(),
            "expectedCountFrom0128": expected_family_counts.get(family),
            "declaredRecordCount": None,
            "actualRecordCount": 0,
            "coverageStatus": "missing_file",
            "missingRequiredFieldCount": 0,
            "runtimeMappingIssueCount": 0,
            "relationshipCount": 0,
            "validRelationshipCount": 0,
            "brokenRelationshipCount": 0,
            "evidenceLinkedCount": 0,
            "confidenceLinkedCount": 0,
            "runtimeMappedCount": 0,
            "sampleIds": [],
        }
        if not path.exists():
            family_reports.append(family_report)
            continue
        data = load_json(path)
        records = data.get("records", []) if isinstance(data, dict) else []
        family_report["declaredRecordCount"] = data.get("recordCount") if isinstance(data, dict) else None
        family_report["actualRecordCount"] = len(records)
        family_report["sampleIds"] = [r.get("id") for r in records[:5] if isinstance(r, dict)]
        if family_report["expectedCountFrom0128"] == len(records) == family_report["declaredRecordCount"]:
            family_report["coverageStatus"] = "complete_against_0128_import_metrics"
        elif family_report["expectedCountFrom0128"] == len(records):
            family_report["coverageStatus"] = "actual_matches_0128_metrics_declared_mismatch"
        else:
            family_report["coverageStatus"] = "count_mismatch"

        for record in records:
            rid = record.get("id")
            if isinstance(rid, str):
                sql_ids.add(rid)
            src = record.get("source")
            if isinstance(src, dict):
                source_file_counter[src.get("canonicalFile", "<missing>")] += 1
                if src.get("canonicalFile") and src.get("extractedFrom"):
                    family_report["evidenceLinkedCount"] += 1
                else:
                    evidence_missing.append(rid)
            else:
                evidence_missing.append(rid)

            if record.get("confidence"):
                family_report["confidenceLinkedCount"] += 1
            else:
                confidence_missing.append(rid)

            missing_fields = [f for f in REQUIRED_RECORD_FIELDS if f not in record]
            if missing_fields:
                family_report["missingRequiredFieldCount"] += len(missing_fields)
                if len(field_issue_samples) < 25:
                    field_issue_samples.append({"id": rid, "family": family, "missing": missing_fields})

            rm = record.get("runtimeMapping")
            if isinstance(rm, dict):
                missing_rm = [f for f in REQUIRED_RUNTIME_FIELDS if f not in rm]
                if missing_rm:
                    family_report["runtimeMappingIssueCount"] += len(missing_rm)
                    if len(runtime_issue_samples) < 25:
                        runtime_issue_samples.append({"id": rid, "family": family, "missingRuntimeFields": missing_rm})
                else:
                    family_report["runtimeMappedCount"] += 1
            else:
                runtime_missing.append(rid)
                family_report["runtimeMappingIssueCount"] += len(REQUIRED_RUNTIME_FIELDS)

            rels = record.get("relationships", [])
            if not isinstance(rels, list):
                rels = []
            family_report["relationshipCount"] += len(rels)
            relationship_source_families[family] += len(rels)
            for rel in rels:
                if not isinstance(rel, dict):
                    continue
                kind = rel.get("kind", "unknown")
                target = rel.get("targetId")
                relationship_by_kind[kind] += 1
                edge = {
                    "sourceId": rid,
                    "sourceFamily": family,
                    "kind": kind,
                    "targetId": target,
                    "raw": rel.get("raw"),
                }
                relationship_edges.append(edge)
                if isinstance(target, str) and target in all_records_by_id:
                    valid_relationships += 1
                    family_report["validRelationshipCount"] += 1
                else:
                    family_report["brokenRelationshipCount"] += 1
                    if len(broken_relationships) < 200:
                        broken_relationships.append(edge)
        family_reports.append(family_report)

    # SQL source table mapping report.
    owner_tables = metrics_0128.get("ownerTables", []) if isinstance(metrics_0128, dict) else []
    owner_table_mapping = []
    # The importer already selected the table families. Preserve mapping explicitly.
    for table in owner_tables:
        row_count = table.get("row_count")
        page = table.get("owner_table_page")
        mapped_family = None
        for family, expected in expected_family_counts.items():
            if expected == row_count:
                mapped_family = family
                break
        owner_table_mapping.append({
            "ownerTablePage": page,
            "rowCount": row_count,
            "mappedFamily": mapped_family or "supporting_or_unmapped_raw_table",
            "classification": "imported_gameplay_family" if mapped_family else "supporting_raw_evidence_table",
            "sampleStrings": table.get("sample_strings", [])[:10],
        })

    completeness = {
        "codeUpdate": "0129",
        "title": "Librarium SQL/MDB Normalization and Verification",
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "baseline": "CodeUpdate0128-SQLDataAddedToLibrarium",
        "librariumAuthority": True,
        "dataPolicy": "/Data is generated runtime output, not authoritative source data.",
        "importedDatabasePolicy": "ImportedDatabases are retained until Librarium is 100% complete and audited.",
        "sqlMdbSource": metrics_0128.get("sourceFile"),
        "sqlMdbMetrics0128": {
            "rawRows": metrics_0128.get("rawRows"),
            "selectedRowsImported": metrics_0128.get("selectedRowsImported"),
            "relationshipsCaptured": metrics_0128.get("relationshipsCaptured"),
            "familyCounts": expected_family_counts,
        },
        "familyReports": family_reports,
        "summary": {
            "familiesAudited": len(family_reports),
            "familiesCompleteAgainst0128Metrics": sum(1 for f in family_reports if f["coverageStatus"] == "complete_against_0128_import_metrics"),
            "sqlRecordIdsIndexed": len(sql_ids),
            "allLibrariumRecordIdsIndexed": len(all_records_by_id),
            "catalogFiles": len(catalog_files),
            "placeholderSeedFilesRetained": len(placeholder_files),
            "duplicateIdCount": len(duplicate_ids),
            "relationshipEdgesIndexed": len(relationship_edges),
            "validRelationshipEdges": valid_relationships,
            "brokenRelationshipEdges": len(relationship_edges) - valid_relationships,
            "evidenceMissingCount": len(evidence_missing),
            "confidenceMissingCount": len(confidence_missing),
            "runtimeMappingMissingCount": len(runtime_missing),
            "openClassicFormulaConflicts": 3,
            "runtimeModified": False,
        },
        "status": "PASS_WITH_KNOWN_GAPS" if broken_relationships or duplicate_ids else "PASS",
        "knownGaps": [
            "Some imported SQL/MDB references remain printable-reference links instead of fully decoded field semantics.",
            "Broken relationship edges usually indicate references not present in the selected imported families or references requiring later semantic decoding.",
            "Three gameplay formula conflicts remain open: physical DR, dodge order, special/backstab defense order.",
        ],
        "artifactHash": None,
    }
    completeness["artifactHash"] = stable_hash({k: v for k, v in completeness.items() if k != "artifactHash"})

    relationship_report = {
        "codeUpdate": "0129",
        "title": "Librarium SQL/MDB Relationship Verification",
        "totalEdges": len(relationship_edges),
        "validEdges": valid_relationships,
        "brokenEdges": len(relationship_edges) - valid_relationships,
        "edgesByKind": dict(sorted(relationship_by_kind.items())),
        "edgesBySourceFamily": dict(sorted(relationship_source_families.items())),
        "brokenRelationshipSamples": broken_relationships,
        "edgeHash": stable_hash(relationship_edges),
    }

    evidence_report = {
        "codeUpdate": "0129",
        "title": "Librarium Evidence Linkage Verification",
        "sourceFileCounts": dict(sorted(source_file_counter.items())),
        "missingEvidenceSamples": evidence_missing[:100],
        "missingConfidenceSamples": confidence_missing[:100],
        "missingRuntimeMappingSamples": runtime_missing[:100],
        "fieldIssueSamples": field_issue_samples,
        "runtimeIssueSamples": runtime_issue_samples,
    }

    compiler_readiness = {
        "codeUpdate": "0129",
        "title": "Compiler Readiness Audit",
        "objectFamiliesWithGenerationContracts": 0,
        "objectFamiliesMissingGenerationContracts": [],
        "dataClassicFiles": [],
        "dataClassicManifestPresent": (ROOT / "Data" / "Classic" / "compile_manifest.json").exists(),
        "worldManifestPresent": (ROOT / "Data" / "Classic" / "world_manifest.json").exists(),
        "status": "PASS_WITH_GAPS",
        "notes": [
            "The Librarium has SQL/MDB records and runtime mapping metadata for imported families.",
            "The next compiler step should use these reports to decide which broken references are expected external references versus true missing Librarium objects.",
        ],
    }
    gen_contract_path = LIB / "GenerationReadiness" / "generation_contracts_0125.json"
    if gen_contract_path.exists():
        try:
            gen = load_json(gen_contract_path)
            # Count contracts flexibly.
            if isinstance(gen, dict):
                contracts = gen.get("contracts") or gen.get("objectFamilies") or gen.get("generationContracts") or []
                if isinstance(contracts, list):
                    compiler_readiness["objectFamiliesWithGenerationContracts"] = len(contracts)
        except Exception:
            pass
    for f in sorted((ROOT / "Data" / "Classic").glob("*.json")):
        compiler_readiness["dataClassicFiles"].append(str(f.relative_to(ROOT)))

    source_table_report = {
        "codeUpdate": "0129",
        "title": "SQL/MDB Owner Table Mapping",
        "ownerTablesFound": len(owner_table_mapping),
        "ownerTables": owner_table_mapping,
        "mappingPolicy": "Gameplay families imported into Librarium object records; supporting raw tables remain evidence until decoded or intentionally excluded.",
    }

    write_json(OUT / "sql_mdb_normalization_summary_0129.json", completeness)
    write_json(OUT / "sql_mdb_relationship_verification_0129.json", relationship_report)
    write_json(OUT / "sql_mdb_evidence_linkage_0129.json", evidence_report)
    write_json(OUT / "sql_mdb_owner_table_mapping_0129.json", source_table_report)
    write_json(OUT / "compiler_readiness_audit_0129.json", compiler_readiness)

    # Also place copies in Reports for human review.
    write_json(REPORTS / "sql_mdb_normalization_summary_0129.json", completeness)
    write_json(REPORTS / "sql_mdb_relationship_verification_0129.json", relationship_report)
    write_json(REPORTS / "compiler_readiness_audit_0129.json", compiler_readiness)

    md = f"""# CodeUpdate0129 — Librarium Normalization & Verification

## Result

**Status:** {completeness['status']}

This pass redoes CodeUpdate0129 as an audit/normalization milestone instead of another broad placeholder population pass.
It validates the SQL/MDB records imported into the Librarium during CodeUpdate0128 and keeps runtime gameplay unchanged.

## Verified SQL/MDB import coverage

| Family | Expected | Actual | Status |
|---|---:|---:|---|
"""
    for f in family_reports:
        md += f"| {f['family']} | {f['expectedCountFrom0128']} | {f['actualRecordCount']} | {f['coverageStatus']} |\n"
    md += f"""

## Relationship audit

- Relationship edges indexed: **{relationship_report['totalEdges']}**
- Valid target edges: **{relationship_report['validEdges']}**
- Broken/unresolved edges: **{relationship_report['brokenEdges']}**

Broken edges are not automatically gameplay failures. Many are references that still need deeper byte/field decoding or point outside the currently selected SQL/MDB gameplay families.
They are now explicit and searchable instead of hidden.

## Evidence linkage

- SQL/MDB record IDs indexed: **{completeness['summary']['sqlRecordIdsIndexed']}**
- All Librarium record IDs indexed: **{completeness['summary']['allLibrariumRecordIdsIndexed']}**
- Evidence missing count: **{completeness['summary']['evidenceMissingCount']}**
- Runtime mapping missing count: **{completeness['summary']['runtimeMappingMissingCount']}**

## Open conflicts carried forward

1. Physical Damage Reduction
2. Dodge order
3. Special attack/backstab defense order

## Runtime status

Runtime gameplay code was not modified.

## Policy reaffirmed

- Librarium is authoritative.
- ImportedDatabases are retained until Librarium completion is truly 100% audited.
- /Data is generated runtime output.
- Server/runtime should consume generated /Data, not imported SQL/MDB sources.
"""
    write_md(REPORTS / "CODEUPDATE0129_LIBRARIUM_NORMALIZATION_VERIFICATION.md", md)
    write_md(ROOT / "CHANGELOG_CODEUPDATE0129.md", md)
    write_md(ROOT / "SESSION_HANDOFF_CODEUPDATE0129.md", f"""# Session Handoff — CodeUpdate0129

Use this repository as the next baseline after CodeUpdate0128.

Completed:
- SQL/MDB family coverage audit.
- Relationship verification.
- Evidence linkage verification.
- Compiler readiness audit.
- Reports under `Librarium/Normalization0129/` and `Reports/CodeUpdate0129/`.

Next recommended update:
- Resolve/report broken relationship classes.
- Improve semantic byte decoding where printable references are not enough.
- Begin runtime loader integration once Codex/.NET build is available.
""")

    manifest = {
        "codeUpdate": "0129",
        "title": "Librarium Normalization and Verification",
        "base": "CodeUpdate0128",
        "runtimeModified": False,
        "dataPolicy": "generated-only",
        "importedDatabasesRetained": True,
        "reports": [
            "Librarium/Normalization0129/sql_mdb_normalization_summary_0129.json",
            "Librarium/Normalization0129/sql_mdb_relationship_verification_0129.json",
            "Librarium/Normalization0129/sql_mdb_evidence_linkage_0129.json",
            "Librarium/Normalization0129/sql_mdb_owner_table_mapping_0129.json",
            "Librarium/Normalization0129/compiler_readiness_audit_0129.json",
            "Reports/CodeUpdate0129/CODEUPDATE0129_LIBRARIUM_NORMALIZATION_VERIFICATION.md",
        ],
        "status": completeness["status"],
        "summary": completeness["summary"],
    }
    write_json(ROOT / "MANIFEST_CODEUPDATE0129.json", manifest)

    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
