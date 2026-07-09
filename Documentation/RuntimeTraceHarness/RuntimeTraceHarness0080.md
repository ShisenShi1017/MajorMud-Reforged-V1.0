# CodeUpdate 0080 — Runtime Trace Probe Harness

## Purpose

CodeUpdate 0080 converts the host lifecycle probes from CodeUpdate 0079 into a reproducible trace harness. It does not execute the probes against a live Classic runtime. It defines what must be captured, how trace events should be represented, and how trace logs can be summarized for later promotion review.

## Canonical rule

The Librarium remains the canonical source of truth. Runtime trace logs are observed evidence inputs. They may support promotion only after reconciliation with provenance, repeatability, and the promotion rules already established by the Librarium architecture.

## Probe families

1. Scheduler cadence — measures host loop and module polling cadence.
2. Callback registration — records which callbacks MajorMUD registers and invokes.
3. Timed callback ordering — compares RTKICK, RTIHDLR, SYSCYC, status handlers, and polling.
4. Output flush ordering — captures enqueue, drain, flush, and socket send order.
5. World iteration — captures player/session/NPC/combat/regen/quest/spawn update order.

## Harness files

- `Librarium/RuntimeTraceHarness/runtime_trace_probe_catalog_0080.json`
- `Librarium/RuntimeTraceHarness/runtime_trace_event_schema_0080.json`
- `Librarium/RuntimeTraceHarness/gap_to_probe_mapping_0080.json`
- `Tools/RuntimeTraceHarness/runtime_trace_runner.py`
- `Validation/RuntimeTraceHarness0080/example_trace_template_0080.ndjson`
- `Validation/RuntimeTraceHarness0080/example_trace_summary_0080.json`

## How to use

Generate or capture NDJSON events matching schema version `0080.1`, then run:

```bash
python Tools/RuntimeTraceHarness/runtime_trace_runner.py trace.ndjson --out summary.json
```

The summary is evidence input only. It does not promote mechanics automatically.

## Promotion requirements

A timing behavior may move upward only when:

1. The trace is captured from an instrumented host/module path.
2. The captured events match the 0080 schema.
3. Multiple runs produce stable ordering or explicitly documented variance.
4. The trace is reconciled with the matching host timing gap.
5. The promotion rationale is documented in the Librarium.

## Non-goals

0080 does not:

- Replace runtime formulas.
- Promote gameplay formulas to Verified.
- Claim modem ping drives gameplay ticks.
- Claim Classic MajorBBS cadence without observed or documentary evidence.
