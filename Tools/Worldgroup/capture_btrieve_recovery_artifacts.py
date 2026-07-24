#!/usr/bin/env python3
"""Capture and compare Btrieve/MKDE recovery artifacts without interpreting unknown formats."""
from __future__ import annotations
import argparse, hashlib, json, os, pathlib, shutil, time
from datetime import datetime, timezone

SCHEMA_CAPTURE = "majormud.worldgroup-recovery-capture.v1"
SCHEMA_DIFF = "majormud.worldgroup-recovery-artifact-diff.v1"
PATTERNS = ("BTRVLOG.TRN", "BTRVLOG.RCV", "BTR_TMP.$??", "????????.LOG", "MKDEWE.TRA", "MKDEMSG.LOG")

def sha256(path: pathlib.Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()

def candidates(root: pathlib.Path):
    seen = set()
    for pattern in PATTERNS:
        for path in root.rglob(pattern):
            if path.is_file() and path not in seen:
                seen.add(path)
                yield path

def snapshot(root: pathlib.Path, output: pathlib.Path, label: str, copy_files: bool) -> dict:
    root = root.resolve(); output.mkdir(parents=True, exist_ok=True)
    records = []
    for path in sorted(candidates(root), key=lambda p: str(p).lower()):
        stat = path.stat(); rel = path.relative_to(root).as_posix()
        record = {
            "relative_path": rel,
            "size": stat.st_size,
            "mtime_ns": stat.st_mtime_ns,
            "sha256": sha256(path),
        }
        if copy_files:
            target = output / "artifacts" / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
            record["captured_path"] = target.relative_to(output).as_posix()
        records.append(record)
    manifest = {
        "schema": SCHEMA_CAPTURE,
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "label": label,
        "source_root": str(root),
        "copy_files": copy_files,
        "patterns": list(PATTERNS),
        "artifact_count": len(records),
        "artifacts": records,
        "policy": "Byte-preserving capture only; unknown recovery records are not interpreted.",
    }
    path = output / f"capture-{label}.json"
    path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return manifest

def load(path: pathlib.Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("schema") != SCHEMA_CAPTURE:
        raise ValueError(f"Unsupported capture schema: {data.get('schema')}")
    return data

def diff(before: dict, after: dict) -> dict:
    b = {x["relative_path"]: x for x in before["artifacts"]}
    a = {x["relative_path"]: x for x in after["artifacts"]}
    created, deleted, changed, unchanged = [], [], [], []
    for key in sorted(set(b) | set(a)):
        if key not in b: created.append(a[key])
        elif key not in a: deleted.append(b[key])
        elif b[key]["sha256"] != a[key]["sha256"] or b[key]["size"] != a[key]["size"]:
            changed.append({"relative_path": key, "before": b[key], "after": a[key]})
        else: unchanged.append(a[key])
    return {
        "schema": SCHEMA_DIFF,
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "before_label": before["label"], "after_label": after["label"],
        "summary": {"created": len(created), "deleted": len(deleted), "changed": len(changed), "unchanged": len(unchanged)},
        "created": created, "deleted": deleted, "changed": changed, "unchanged": unchanged,
        "interpretation_policy": "Differences are evidence only. No record field or undo/redo meaning is inferred from a single capture.",
    }

def main() -> int:
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("snapshot"); s.add_argument("root", type=pathlib.Path); s.add_argument("output", type=pathlib.Path); s.add_argument("--label", required=True); s.add_argument("--metadata-only", action="store_true")
    d = sub.add_parser("diff"); d.add_argument("before", type=pathlib.Path); d.add_argument("after", type=pathlib.Path); d.add_argument("output", type=pathlib.Path)
    args = p.parse_args()
    if args.cmd == "snapshot":
        result = snapshot(args.root, args.output, args.label, not args.metadata_only)
        print(f"Captured {result['artifact_count']} artifact(s)")
    else:
        result = diff(load(args.before), load(args.after)); args.output.parent.mkdir(parents=True, exist_ok=True); args.output.write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8"); print(json.dumps(result["summary"], sort_keys=True))
    return 0
if __name__ == "__main__": raise SystemExit(main())
