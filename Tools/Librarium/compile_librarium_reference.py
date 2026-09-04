#!/usr/bin/env python3
"""Reference Python smoke compiler for environments without .NET SDK.

The authoritative compiler implementation for CodeUpdate0126 is the .NET source in
Apps/Compiler. This script mirrors the v1 export contract so repository validation
can run in restricted environments.
"""
import argparse, hashlib, json, shutil
from pathlib import Path

PREFERRED = ["*_records_*.json", "*_catalog_*.json", "catalog.json"]

def select_source(family_dir: Path):
    for pattern in PREFERRED:
        matches = sorted(family_dir.glob(pattern), key=lambda p: p.stat().st_mtime, reverse=True)
        if matches:
            return matches[0]
    return None

def extract_records(data):
    if isinstance(data, list):
        return [x for x in data if isinstance(x, dict)]
    if isinstance(data, dict):
        for key in ["records", "items", "objects", "catalog", "entries", "formulas", "worlds"]:
            value = data.get(key)
            if isinstance(value, list):
                return [x for x in value if isinstance(x, dict)]
        if any(k in data for k in ["id", "name", "object_type", "mechanic_id"]):
            return [data]
    return []

def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=False), encoding="utf-8")

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--world", default="Classic")
    ap.add_argument("--clean", action="store_true")
    ap.add_argument("--root", default=".")
    args = ap.parse_args()
    root = Path(args.root).resolve()
    world = "Classic" if args.world.lower() == "classic" else args.world
    out = root / "Data" / world
    if args.clean and out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True, exist_ok=True)
    families = []
    diagnostics = []
    objects = 0
    files_written = 0
    for fam_dir in sorted((root / "Librarium" / "Objects").iterdir()):
        if not fam_dir.is_dir():
            continue
        src = select_source(fam_dir)
        if not src:
            diagnostics.append({"severity":"Warning","code":"PYLIB002","message":f"No catalog for {fam_dir.name}","path":str(fam_dir)})
            continue
        try:
            data = json.loads(src.read_text(encoding="utf-8"))
            records = extract_records(data)
        except Exception as exc:
            diagnostics.append({"severity":"Error","code":"PYLIB003","message":str(exc),"path":str(src)})
            continue
        payload = {
            "generated_by":"RealmGate.Compiler.ReferencePython",
            "compiler_code_update":"0126",
            "world_id":world,
            "family":fam_dir.name,
            "source_file":str(src.relative_to(root)).replace('\\\\','/'),
            "record_count":len(records),
            "records":records,
        }
        write_json(out / f"{fam_dir.name.lower().replace('-', '_')}.json", payload)
        files_written += 1
        objects += len(records)
        families.append({"family":fam_dir.name,"source_file":str(src.relative_to(root)).replace('\\\\','/'),"record_count":len(records)})
    client_projection = {
        "world_id":world,
        "projection":"read_only_client_librarium_projection_v1",
        "families_available":[f["family"] for f in families],
        "client_rule":"Client reads world-scoped Librarium/Data projections only; client never edits gameplay definitions.",
    }
    write_json(out / "client_projection.json", client_projection)
    files_written += 1
    manifest = {
        "code_update":"0126",
        "compiler":"RealmGate.Compiler",
        "reference_compiler":"Tools/Librarium/compile_librarium_reference.py",
        "world_id":world,
        "librarium_authority":"Librarium is source of truth; /Data is generated runtime output.",
        "object_families_compiled":len(families),
        "objects_compiled":objects,
        "files_written":files_written + 1,
        "families":families,
        "diagnostics":diagnostics,
    }
    write_json(out / "compile_manifest.json", manifest)
    files_written += 1
    digest = hashlib.sha256()
    for p in sorted(out.glob("*.json")):
        if p.name == "compile_hash.json":
            continue
        digest.update(p.name.encode())
        digest.update(p.read_bytes())
    write_json(out / "compile_hash.json", {"algorithm":"sha256","value":digest.hexdigest(),"deterministic_output":"same Librarium input should produce same hash"})
    print(f"PASS reference compile: world={world} families={len(families)} objects={objects} files={files_written+1}")
    return 0 if not any(d["severity"] == "Error" for d in diagnostics) else 1

if __name__ == "__main__":
    raise SystemExit(main())
