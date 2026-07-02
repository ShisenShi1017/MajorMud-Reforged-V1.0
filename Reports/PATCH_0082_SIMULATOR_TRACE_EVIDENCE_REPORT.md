# Patch 0082 Simulator Trace Evidence Report

## Baseline

Trusted source: `MajorMUD-Reforged-Patch0081-DirectDecoderRuntimeEvidence-Cumulative.zip`.

## Scope

Patch 0082 adds the simulator to the cumulative patch and captures simulator trace evidence without modifying runtime `/Data`.

## Simulator boundary

The included simulator README states it is **not Worldgroup** and does **not certify live gameplay equivalence**. Therefore Patch 0082 classifies simulator output as `expected_model_simulator_trace`, not `original_runtime_verified`.

## Results

- Core `/Data` modified: No.
- Recreated `/Data` included: Yes.
- Simulator included: Yes, at `Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype`.
- Nested ZIP archives included: No. Simulator `SourceCheckpoints/*.zip` archives were excluded to comply with the no-old-ZIPs rule.
- Room endpoint gaps resolved by direct original runtime/decoder evidence: 0.
- Room endpoint gaps remaining: 210.
- Runtime parity issues remaining: 366.
- Special-exit behavior meanings promoted: 0.
- Special-exit types still blocked: 13.
- Simulator unknown trace events captured: 27.

## Generated evidence artifacts

- `/Evidence/SimulatorTrace/simulator_summary_0082.txt`
- `/Evidence/SimulatorTrace/simulator_unknowns_0082.txt`
- `/Evidence/SimulatorTrace/simulator_unknown_simulation_0082.txt`
- `/Evidence/SimulatorTrace/unknown_trace_latest_0082.csv`
- `/Evidence/SimulatorTrace/simulator_room_probe_0082.txt`
- `/Validation/SimulatorEndpointTrace_0082.csv`
- `/Validation/SimulatorSpecialExitBehaviorTrace_0082.csv`

## Conclusion

The simulator is now part of the cumulative patch stream. It is useful for repeatable expected-model validation and trace harvesting, but it cannot by itself promote remaining room endpoints or special-exit behavior semantics as original runtime facts. All unresolved blockers remain documented and blocked.
