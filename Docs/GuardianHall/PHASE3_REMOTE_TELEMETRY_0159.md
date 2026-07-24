# Guardian Hall Phase 3-0159 — Remote Realm Telemetry

Realm Keeper now supports explicit LAN and Internet telemetry endpoints.

- Remote hosts are editable and persisted per Guardian Hall state.
- Each endpoint has name, scope, base URL, telemetry path, timeout, enable state, status, contact time, and error state.
- Authentication uses a bearer token read from a configured environment-variable name. Token values are never serialized.
- Only absolute HTTP/HTTPS endpoints are accepted.
- Unreachable hosts remain visible as unavailable and are never counted as connected.
- Remote telemetry is merged with measured local-host telemetry.

Expected JSON fields are `worlds`, `population`, `activeEvents`, and `health`. Missing numeric fields safely default to zero.
