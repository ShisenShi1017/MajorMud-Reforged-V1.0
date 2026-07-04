# Evidence-To-Server Regression Contract

## Goal

Convert validated evidence into server regression cases that can run against the actual Reforged server implementation.

## Inputs

- Timestamped or raw character logs.
- Simulator-derived expected execution events.
- Server-emitted actual execution events.
- The current repository `Data/` snapshot under test.

## Flow

Evidence should become comparable typed events. Server scenarios should emit comparable typed events through an adapter layer. The replay harness should compare them and classify results as `PASS`, `CANDIDATE`, `CONTRADICTION`, or `INSUFFICIENT`.

## Non-Goals

- Do not promote `Data/` values from logs alone.
- Do not require runtime sockets or live gameplay to run early regression tests.
- Do not hardcode MajorMUD behavior into server core.
- Do not treat simulator structure as the required server internal architecture.

