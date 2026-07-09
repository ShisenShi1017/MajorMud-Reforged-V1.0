#!/usr/bin/env python3
"""MajorMUD Reforged Runtime Trace Harness 0080.

This tool validates and summarizes NDJSON trace logs captured from MBBSEmu,
Reforged, simulator, or future MajorMUD instrumentation. It intentionally does
not claim that probes have been executed; it provides the reproducible harness
for execution and review.
"""
from __future__ import annotations

import argparse
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Dict, Iterable, List

REQUIRED = {"schema_version", "probe_id", "event", "timestamp_utc", "source", "sequence"}
ALLOWED_SCHEMA = "0080.1"


def load_ndjson(path: Path) -> List[Dict[str, Any]]:
    events: List[Dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as handle:
        for line_no, line in enumerate(handle, 1):
            line = line.strip()
            if not line:
                continue
            try:
                event = json.loads(line)
            except json.JSONDecodeError as exc:
                raise SystemExit(f"{path}:{line_no}: invalid JSON: {exc}") from exc
            missing = REQUIRED - set(event)
            if missing:
                raise SystemExit(f"{path}:{line_no}: missing required fields: {sorted(missing)}")
            if event.get("schema_version") != ALLOWED_SCHEMA:
                raise SystemExit(f"{path}:{line_no}: unsupported schema_version {event.get('schema_version')!r}")
            events.append(event)
    return events


def summarize(events: Iterable[Dict[str, Any]]) -> Dict[str, Any]:
    items = list(events)
    by_probe: Dict[str, Counter] = defaultdict(Counter)
    by_source = Counter()
    phases = Counter()
    sequence_errors: List[str] = []
    last_seq_by_probe: Dict[str, int] = {}

    for event in items:
        probe_id = str(event["probe_id"])
        by_probe[probe_id][str(event["event"])] += 1
        by_source[str(event["source"])] += 1
        phases[str(event.get("phase"))] += 1
        seq = int(event["sequence"])
        if probe_id in last_seq_by_probe and seq < last_seq_by_probe[probe_id]:
            sequence_errors.append(f"{probe_id}: sequence decreased from {last_seq_by_probe[probe_id]} to {seq}")
        last_seq_by_probe[probe_id] = seq

    return {
        "codeupdate": "0080",
        "status": "trace_summary_generated",
        "event_count": len(items),
        "probe_count": len(by_probe),
        "events_by_probe": {probe: dict(counter) for probe, counter in sorted(by_probe.items())},
        "events_by_source": dict(by_source),
        "events_by_phase": dict(phases),
        "sequence_errors": sequence_errors,
        "promotion_policy": "This summary is observed evidence input only; promotions require human/evidence review."
    }


def emit_template(path: Path) -> None:
    template = [
        {
            "schema_version": "0080.1",
            "probe_id": "TRACE-0080-001",
            "event": "host.loop.enter",
            "timestamp_utc": "YYYY-MM-DDTHH:MM:SS.sssZ",
            "monotonic_ms": 0.0,
            "sequence": 0,
            "source": "MBBSEmu",
            "module": "MajorMUD",
            "channel": None,
            "routine": "WorkerThread",
            "phase": "enter",
            "payload": {"timer_hertz": None, "load_profile": "idle|busy"},
            "evidence_state": "Observed",
            "notes": "Replace with instrumentation-captured values."
        }
    ]
    with path.open("w", encoding="utf-8") as handle:
        for row in template:
            handle.write(json.dumps(row, sort_keys=True) + "\n")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate and summarize MajorMUD runtime trace NDJSON logs.")
    parser.add_argument("trace", nargs="*", type=Path, help="NDJSON trace files to validate/summarize")
    parser.add_argument("--out", type=Path, default=None, help="Write summary JSON to this path")
    parser.add_argument("--emit-template", type=Path, default=None, help="Write an example NDJSON template and exit")
    args = parser.parse_args()

    if args.emit_template:
        emit_template(args.emit_template)
        print(f"wrote template: {args.emit_template}")
        return 0

    if not args.trace:
        parser.error("provide at least one trace file or --emit-template")

    events: List[Dict[str, Any]] = []
    for trace in args.trace:
        events.extend(load_ndjson(trace))
    summary = summarize(events)
    text = json.dumps(summary, indent=2, sort_keys=True)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text + "\n", encoding="utf-8")
    print(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
