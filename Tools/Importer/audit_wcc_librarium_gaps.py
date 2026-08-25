#!/usr/bin/env python3
"""Audit available WCC/WBB source files against Librarium coverage.

This is a read-only exhaustion report. It inventories every local native source
family that can currently inform Classic, checks the matching Librarium object
family counts, and records blockers where the source evidence is not present.
It does not promote fields or edit generated Data/Classic.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


@dataclass(frozen=True)
class FamilySpec:
    key: str
    librarium_family: str
    native_names: tuple[str, ...]
    default_names: tuple[str, ...]
    wbb_names: tuple[str, ...] = ()
    expected_native_record_size: int | None = None
    expected_default_record_size: int | None = None
    notes: str = ""


FAMILIES: tuple[FamilySpec, ...] = (
    FamilySpec("areas", "areas", (), (), notes="No direct native DAT family identified in the current repo."),
    FamilySpec("banks", "banks", ("WCCBANKS.DAT",), ("wccbank2.dat",), ("WBBBANK2.DAT",), 1536, 24576),
    FamilySpec("classes", "classes", ("WCCCLASS.DAT",), ("wccclas2.dat",), ("WBBCLAS2.DAT",), 6144, 40960),
    FamilySpec("gangs", "guilds", ("WCCGANGS.DAT",), ("wccgang2.dat",), ("WBBGANG2.DAT",), 2048, 28672),
    FamilySpec("items", "items", ("WCCITEMS.DAT",), ("wccitem2.dat",), ("WBBITEM2.DAT",), 1024, 1024),
    FamilySpec("item_world", "loot_rules", (), ("wccitow2.dat",), ("WBBITOW2.DAT",), None, 73728),
    FamilySpec("known_messages", "npcs", ("WCCKNMSR.DAT",), ("wccknms2.dat",), ("WBBKNMS2.DAT",), 561152, 973824),
    FamilySpec("map_room_text", "room_descriptions", ("WCCMP001.DAT",), ("wccmp002.dat",), ("WBBMP002.DAT",), 1536, 1536),
    FamilySpec("messages", "events", ("WCCMSG.DAT",), ("wccmsg2.dat",), ("WBBMSG2.DAT",), 870912, 1220608),
    FamilySpec("races", "races", ("WCCRACE.DAT",), ("wccrace2.dat",), ("WBBRACE2.DAT",), 3072, 40960),
    FamilySpec("shops", "shops", ("WCCSHOPS.DAT",), ("wccshop2.dat",), ("WBBSHOP2.DAT",), 512, 512),
    FamilySpec("spells", "spells", ("WCCSPELS.DAT",), ("wccspel2.dat",), ("WBBSPEL2.DAT",), 256, 256),
    FamilySpec("textblocks", "quests", ("WCCTEXT.DAT",), ("wcctext2.dat",), ("WBBTEXT2.DAT",), 1073152, 7442432),
    FamilySpec("updates", "validation_rules", ("WCCUPDAT.DAT",), ("wccupda2.dat",), ("WBBUPDA2.DAT",), None, None),
    FamilySpec("users", "character_foundation", ("WCCUSERS.DAT",), ("wccuser2.dat",), ("WBBUSER2.DAT",), None, None),
)


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def find_file(root: Path, names: Iterable[str]) -> Path | None:
    wanted = {name.lower() for name in names}
    for path in root.glob("*"):
        if path.is_file() and path.name.lower() in wanted:
            return path
    return None


def count_librarium_records(repo: Path, family: str) -> dict[str, int]:
    root = repo / "Librarium" / "Objects" / family
    if not root.exists():
        return {"files": 0, "records": 0}

    file_count = 0
    record_count = 0
    for path in sorted(root.glob("*.json")):
        try:
            node = json.loads(path.read_text(encoding="utf-8"))
        except Exception:
            continue
        file_count += 1
        if isinstance(node, list):
            record_count += len(node)
        elif isinstance(node, dict) and isinstance(node.get("records"), list):
            record_count += len(node["records"])
        elif isinstance(node, dict):
            record_count += 1
    return {"files": file_count, "records": record_count}


def file_info(path: Path | None, record_size: int | None = None) -> dict[str, object] | None:
    if path is None:
        return None
    size = path.stat().st_size
    info: dict[str, object] = {
        "path": str(path),
        "size": size,
        "sha256": sha256(path),
    }
    if record_size and record_size > 0:
        info["record_size_hint"] = record_size
        info["record_count_hint"] = size // record_size
        info["record_size_evenly_divides"] = size % record_size == 0
    return info


def audit(repo: Path) -> dict[str, object]:
    native = repo / "ImportedDatabases" / "NativeRuntime"
    default = repo / "ImportedDatabases" / "WCCDefaultInstall" / "wcc default"
    wbb = repo / "ImportedDatabases" / "WCCLabeledWBB" / "Extracted"

    families = []
    for spec in FAMILIES:
        native_path = find_file(native, spec.native_names) if spec.native_names else None
        default_path = find_file(default, spec.default_names) if spec.default_names else None
        wbb_path = find_file(wbb, spec.wbb_names) if spec.wbb_names else None
        librarium = count_librarium_records(repo, spec.librarium_family)

        status = "source_present"
        if native_path is None and default_path is None and wbb_path is None:
            status = "no_local_source"
        elif librarium["records"] == 0:
            status = "source_present_no_librarium_records"

        families.append({
            "key": spec.key,
            "librarium_family": spec.librarium_family,
            "status": status,
            "native_runtime": file_info(native_path, spec.expected_native_record_size),
            "wcc_default": file_info(default_path, spec.expected_default_record_size),
            "wbb_secondary": file_info(wbb_path, spec.expected_default_record_size),
            "librarium": librarium,
            "notes": spec.notes,
        })

    map_files = sorted(
        path.name.upper()
        for root in (native, default)
        if root.exists()
        for path in root.glob("wccmp*.dat")
    )
    map_files.extend(sorted(
        path.name.upper()
        for root in (native, default)
        if root.exists()
        for path in root.glob("WCCMP*.DAT")
    ))
    map_files = sorted(set(map_files))
    missing_maps = []

    room_description_records = count_librarium_records(repo, "room_descriptions")["records"]
    room_records = count_librarium_records(repo, "rooms")["records"]

    return {
        "audit_scope": "current",
        "mode": "read_only_wcc_librarium_gap_audit",
        "repo": str(repo),
        "families": families,
        "room_text": {
            "available_wccmp_files": map_files,
            "missing_expected_wccmp_files": missing_maps,
            "authority_note": "WCCMP002 is the current 1.11p package; WCCMP001 is legacy supplemental. WCCMP003-017 are not expected.",
            "librarium_room_records": room_records,
            "librarium_room_description_records": room_description_records,
            "coverage_percent_against_librarium_rooms": round((room_description_records / room_records) * 100, 2) if room_records else 0,
        },
    }


def write_markdown(report: dict[str, object], output: Path) -> None:
    lines = [
        "# WCC Librarium Exhaustion Audit",
        "",
        "This read-only audit inventories the local WCC/WBB evidence currently available to complete Classic Librarium coverage.",
        "",
        "It does not mutate `ImportedDatabases/`, generated `/Data`, or Librarium records.",
        "",
        "## Room Text Source Gate",
        "",
    ]
    room_text = report["room_text"]
    lines.extend([
        f"- Available WCC map files: {', '.join(room_text['available_wccmp_files']) or 'none'}.",
        f"- Missing expected map files: {', '.join(room_text['missing_expected_wccmp_files']) or 'none'}.",
        f"- Librarium room records: {room_text['librarium_room_records']:,}.",
        f"- Librarium room-description records: {room_text['librarium_room_description_records']:,}.",
        f"- Room-description coverage: {room_text['coverage_percent_against_librarium_rooms']}%.",
        "",
        "## Family Inventory",
        "",
        "| Family | Librarium family | Status | NativeRuntime | WCC default | WBB secondary | Librarium records |",
        "| --- | --- | --- | --- | --- | --- | ---: |",
    ])

    for family in report["families"]:
        native = "yes" if family["native_runtime"] else "no"
        default = "yes" if family["wcc_default"] else "no"
        wbb = "yes" if family["wbb_secondary"] else "no"
        lines.append(
            f"| {family['key']} | {family['librarium_family']} | {family['status']} | "
            f"{native} | {default} | {wbb} | {family['librarium']['records']:,} |"
        )

    lines.extend([
        "",
        "## Interpretation",
        "",
        "- Available local WCC/WBB families can continue to be decoded and reconciled into Librarium only when evidence is explicit.",
        "- Room descriptions are currently evidence-backed for the available map files only.",
        "- WCCMP002 is the current 1.11p primary package and WCCMP001 is legacy supplemental; WCCMP003-017 are not expected. OmniVerse, Redux, MegaMud, and MudInfo may corroborate native evidence.",
    ])

    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".", help="Repository root")
    parser.add_argument("--out-dir", default="Reports/DataDecode", help="Report output folder")
    args = parser.parse_args()

    repo = Path(args.repo).resolve()
    out_dir = (repo / args.out_dir).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    report = audit(repo)
    json_path = out_dir / "wcc_librarium_exhaustion_audit.json"
    md_path = out_dir / "wcc_librarium_exhaustion_audit.md"
    json_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_markdown(report, md_path)

    room_text = report["room_text"]
    print("WCC Librarium exhaustion audit complete")
    print(f"Room descriptions: {room_text['librarium_room_description_records']:,}/{room_text['librarium_room_records']:,}")
    print(f"Missing WCCMP files: {len(room_text['missing_expected_wccmp_files'])}")
    print(f"Wrote {json_path}")
    print(f"Wrote {md_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
