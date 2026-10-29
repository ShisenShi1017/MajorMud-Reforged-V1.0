#!/usr/bin/env python3
"""Reproduce the Classic monster group/index evidence without altering inputs.

Reads the codex-monster-wcc.json preserved inside the original uploaded archive.
Produces the deterministic Brotli/base64 snapshot consumed by the Classic compiler.
Original monster identity, group and index fields are preserved without rewriting
or assigning artificial NPC home locations.
"""
from __future__ import annotations

import argparse
import base64
import brotli
import hashlib
import json
from pathlib import Path

SOURCE_SHA256 = "83d8fb4098107324b454db4299668624a20c838a094204917d75ca4861e12024"
ROWS_SHA256 = "48ac6a8ac5b4df0edd2bf2288b0c20175c06a960ceb6b7f8c4cb40a5e7856cc5"


def extract(codex_path: Path) -> tuple[str, dict[str, str | int]]:
    source_bytes = codex_path.read_bytes()
    actual_source = hashlib.sha256(source_bytes).hexdigest()
    if actual_source != SOURCE_SHA256:
        raise ValueError("Original codex source hash mismatch; refusing uncertain monster identities")
    data = json.loads(source_bytes)
    if not isinstance(data, list) or len(data) != 1100:
        raise ValueError("Expected exactly 1,100 source monster records")
    records = sorted(
        (int(item["legacy_number"]), int(item["group_id"]), int(item["index_number"]))
        for item in data
    )
    if len(set(record[0] for record in records)) != 1100:
        raise ValueError("Duplicate native monster IDs")
    raw = ("\n".join(":".join(map(str, row)) for row in records) + "\n").encode("ascii")
    if hashlib.sha256(raw).hexdigest() != ROWS_SHA256:
        raise ValueError("Native monster snapshot contents differ from verified original")
    output = base64.b64encode(brotli.compress(raw, quality=11)).decode("ascii")
    return output, {
        "monster_records": len(records),
        "raw_bytes": len(raw),
        "compressed_base64_bytes": len(output),
        "source_sha256": actual_source,
        "rows_sha256": hashlib.sha256(raw).hexdigest(),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("original_codex_json", type=Path)
    parser.add_argument("output_snapshot_br64", type=Path)
    args = parser.parse_args()
    output, stats = extract(args.original_codex_json)
    args.output_snapshot_br64.parent.mkdir(parents=True, exist_ok=True)
    args.output_snapshot_br64.write_text(output + "\n", encoding="ascii")
    for key, value in stats.items():
        print(f"{key}={value}")


if __name__ == "__main__":
    main()
