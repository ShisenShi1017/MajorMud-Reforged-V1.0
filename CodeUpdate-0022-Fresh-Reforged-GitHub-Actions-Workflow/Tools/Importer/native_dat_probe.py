#!/usr/bin/env python3
"""Reforged native DAT probe.

This tool is intentionally conservative. It catalogs record-size patterns,
extracts safe printable strings, and cross-checks known CSV names against native
DAT offsets. It does not claim final semantics for unknown bytes.
"""
from __future__ import annotations
import argparse, csv, hashlib, json, os, re
from pathlib import Path

PRINTABLE = set(range(32, 127))

KNOWN_SPECS = {
    "WCCSHOPS.DAT": {"record_size": 512, "id_offset": 6, "name_offset": 10, "confidence": "high"},
    "WCCSPELS.DAT": {"record_size": 256, "id_offset": 6, "name_offset": 8, "confidence": "medium"},
    "WCCITEMS.DAT": {"record_size": 1024, "id_offset": None, "name_offset": "7 or 10", "confidence": "medium"},
    "WCCRACE.DAT": {"record_size": None, "id_offset": 6, "name_offset": 8, "confidence": "partial/name-anchored"},
    "WCCCLASS.DAT": {"record_size": None, "id_offset": 6, "name_offset": 8, "confidence": "partial/name-anchored"},
}


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def extract_strings(data: bytes, min_len: int = 3):
    out = []
    i = 0
    n = len(data)
    while i < n:
        if data[i] in PRINTABLE:
            j = i
            while j < n and data[j] in PRINTABLE:
                j += 1
            raw = data[i:j]
            s = raw.decode("latin-1", errors="replace").strip()
            s = re.sub(r" +", " ", s)
            if len(s) >= min_len and not set(s) <= {" "}:
                out.append({"offset": i, "length": j - i, "text": s})
            i = j
        else:
            i += 1
    return out


def fixed_records(data: bytes, size: int):
    for start in range(0, len(data) - (len(data) % size), size):
        yield start, data[start:start + size]


def read_csv_names(csv_dir: Path):
    names = {}
    for p in csv_dir.glob("*.TXT"):
        try:
            with p.open("r", encoding="utf-8", errors="replace", newline="") as f:
                reader = csv.DictReader(f)
                rows = list(reader)
        except Exception:
            continue
        table = p.name
        names[table] = []
        for row in rows:
            if "Name" in row and row.get("Name"):
                names[table].append({"number": row.get("Number") or row.get("Map Number") or "", "name": row["Name"]})
    return names


def crosscheck(dat_path: Path, csv_rows):
    data = dat_path.read_bytes().lower()
    found = []
    missing = []
    seen_positions = {}
    for row in csv_rows:
        name = row["name"].encode("latin-1", errors="ignore").lower()
        if not name:
            continue
        pos = data.find(name)
        if pos >= 0:
            found.append({"number": row["number"], "name": row["name"], "offset": pos})
            seen_positions.setdefault(pos, []).append(row)
        else:
            missing.append(row)
    return {"found": found, "missing": missing, "duplicate_name_offsets": {str(k): v for k, v in seen_positions.items() if len(v) > 1}}


def analyze(dat_dir: Path, csv_dir: Path | None, out_dir: Path):
    out_dir.mkdir(parents=True, exist_ok=True)
    csv_names = read_csv_names(csv_dir) if csv_dir and csv_dir.exists() else {}
    manifest = []
    for dat_path in sorted(dat_dir.glob("*.DAT")):
        data = dat_path.read_bytes()
        div = [d for d in [64, 128, 153, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768] if len(data) % d == 0]
        strings = extract_strings(data, 4)
        spec = KNOWN_SPECS.get(dat_path.name, {})
        entry = {
            "file": dat_path.name,
            "size": len(data),
            "sha256": sha256(dat_path),
            "evenly_divisible_record_sizes": div,
            "known_spec": spec,
            "printable_string_count": len(strings),
            "first_strings": strings[:100],
        }
        (out_dir / f"{dat_path.stem}.strings.json").write_text(json.dumps(strings[:5000], indent=2), encoding="utf-8")
        if spec.get("record_size"):
            recs = []
            for idx, (start, rec) in enumerate(fixed_records(data, int(spec["record_size"]))):
                s = extract_strings(rec, 3)
                if s:
                    rec_info = {"index": idx, "offset": start, "strings": s[:10]}
                    if spec.get("id_offset") is not None:
                        off = int(spec["id_offset"])
                        rec_info["u16_at_id_offset"] = int.from_bytes(rec[off:off + 2], "little", signed=False)
                    recs.append(rec_info)
            (out_dir / f"{dat_path.stem}.records.json").write_text(json.dumps(recs, indent=2), encoding="utf-8")
            entry["records_with_strings"] = len(recs)
        # CSV mapping by file name convention
        table_map = {
            "WCCCLASS.DAT": "classes.TXT",
            "WCCRACE.DAT": "races.TXT",
            "WCCSHOPS.DAT": "shops.TXT",
            "WCCSPELS.DAT": "spells.TXT",
            "WCCITEMS.DAT": "items.TXT",
        }
        if dat_path.name in table_map and table_map[dat_path.name] in csv_names:
            cc = crosscheck(dat_path, csv_names[table_map[dat_path.name]])
            (out_dir / f"{dat_path.stem}.csv-crosscheck.json").write_text(json.dumps(cc, indent=2), encoding="utf-8")
            entry["csv_crosscheck"] = {
                "csv_table": table_map[dat_path.name],
                "found_by_name": len(cc["found"]),
                "missing_by_name": len(cc["missing"]),
                "duplicate_name_offset_count": len(cc["duplicate_name_offsets"]),
            }
        manifest.append(entry)
    (out_dir / "native_dat_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return manifest


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dat-dir", required=True)
    ap.add_argument("--csv-dir")
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    manifest = analyze(Path(args.dat_dir), Path(args.csv_dir) if args.csv_dir else None, Path(args.out))
    print(f"Analyzed {len(manifest)} DAT files")

if __name__ == "__main__":
    main()
