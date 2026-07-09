#!/usr/bin/env python3
"""Export validated Librarium runtime definitions into generated /Data world packages.

CodeUpdate 0116 policy:
- Librarium is authoritative.
- /Data is generated runtime output.
- Classic exports are locked/read-only from host perspective.
- Custom/AI worlds may use validated overrides.
"""
from __future__ import annotations
import argparse, json, shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LIB = ROOT / "Librarium" / "RuntimeDefinitions"


def read_json(path: Path):
    if not path.exists():
        raise SystemExit(f"Missing required Librarium definition: {path}")
    return json.loads(path.read_text(encoding="utf-8"))


def validate_mechanics(mechanics_doc: dict) -> list[str]:
    errors = []
    ids = set()
    for i, mech in enumerate(mechanics_doc.get("mechanics", [])):
        mid = mech.get("id")
        if not mid:
            errors.append(f"mechanic[{i}] missing id")
            continue
        if mid in ids:
            errors.append(f"duplicate mechanic id: {mid}")
        ids.add(mid)
        if not mech.get("bucket"):
            errors.append(f"{mid} missing bucket")
        if mech.get("knowledge_status") in (None, ""):
            errors.append(f"{mid} missing knowledge_status")
    return errors


def export_world(world_id: str, clean: bool = False) -> Path:
    world_file = LIB / "worlds" / f"{world_id}.world.json"
    if not world_file.exists() and world_id == "classic":
        world_file = LIB / "worlds" / "classic.world.json"
    world = read_json(world_file)
    mechanics = read_json(ROOT / world["mechanics_source"])
    errors = validate_mechanics(mechanics)
    if errors:
        raise SystemExit("Librarium validation failed:\n" + "\n".join(errors))
    out = ROOT / world["data_export_path"]
    if clean and out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True, exist_ok=True)
    (out / "world.json").write_text(json.dumps(world, indent=2), encoding="utf-8")
    (out / "mechanics.json").write_text(json.dumps(mechanics, indent=2), encoding="utf-8")
    (out / "EXPORT_README.md").write_text(
        "# Generated Runtime Data\n\n"
        "This folder was generated from the Librarium. Do not edit it as source.\n"
        "Edit Librarium definitions or world overrides, then re-export.\n",
        encoding="utf-8",
    )
    manifest = {
        "generated_from": "Librarium/RuntimeDefinitions",
        "world_id": world["world_id"],
        "mode": world["mode"],
        "mechanic_count": len(mechanics.get("mechanics", [])),
        "policy": "Data is generated runtime output, not source of truth.",
    }
    (out / "export_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return out


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--world", default="classic", help="World id to export; default classic")
    parser.add_argument("--clean", action="store_true", help="Remove existing generated world data before export")
    args = parser.parse_args()
    out = export_world(args.world, clean=args.clean)
    print(f"Exported {args.world} runtime data to {out.relative_to(ROOT)}")

if __name__ == "__main__":
    main()
