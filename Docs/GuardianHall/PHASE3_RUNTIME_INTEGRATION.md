# Guardian Hall Phase 3 — Runtime Integration

## Pass 0150: Realm Keeper Local Telemetry

This pass replaces Realm Keeper placeholder health text with read-only telemetry gathered from the active repository and local host artifacts.

Implemented sources:

- generated world folders and selected generated record families under `Data/`;
- save/session activity under `Saves/`;
- latest runtime validator report;
- Release build reports;
- local log and crash/error evidence discovery.

Behavior:

- initial refresh when Guardian Hall loads;
- automatic refresh every 30 seconds;
- manual **Refresh Telemetry** command;
- observable health, activity, metrics, source status, and refresh time;
- a bounded 30-entry realm timeline retained by Guardian Hall persistence;
- read-only display only; no server or Librarium mutation.

The current connector is intentionally local and evidence-based. LAN and Internet host telemetry remain selected coverage scopes but require future authenticated host telemetry endpoints before they can be reported as connected.

## CodeUpdate0151 — Wanderer Session Telemetry
- Reads local Saves recursively and prefers files matching the assigned character.
- Counts available generated room records as the exploration comparison corpus.
- Reports session availability, latest route evidence, inventory/character persistence evidence, journal evidence, and exploration progress.
- Rebuilds the mission queue from observable gaps without issuing game commands or inventing a connected session.

## Guardian Hall Phase 3-0160
- Added a persisted authenticated Wanderer live-session connector with editable base URL, telemetry path, mission-step path, timeout, and token environment-variable name.
- Added live route, inventory, progress, journal, discovered-room, and mission-queue telemetry with local save evidence fallback when the endpoint is unavailable.
- Added explicit guarded one-step mission dispatch. Dispatch requires the connector, the operator enable switch, and Interact-or-higher permission.
- Mission dispatch sends only structured policy and mission fields; no arbitrary command text or persisted credential is introduced.
