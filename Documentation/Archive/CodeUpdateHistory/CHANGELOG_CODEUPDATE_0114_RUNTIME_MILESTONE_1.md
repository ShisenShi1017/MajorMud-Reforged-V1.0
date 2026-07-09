# CodeUpdate 0114 — Runtime Milestone 1

## Summary

Runtime Milestone 1 converts the 0113 local runtime bootstrap into a Classic-first network-capable host foundation.

## Changes

- Added opt-in telnet/network host mode through `--run-network` / `--run-telnet`.
- Added `ServerNetworkHost` for TCP line-oriented client sessions.
- Added `RuntimeSessionManager` for multiple concurrent sessions.
- Extended runtime config with network settings.
- Added world identity fields for description, banner, MOTD, and branding foundation.
- Preserved Classic-only default world and enforced Classic protections.
- Preserved local console mode.
- Added world and session diagnostic commands.
- Added `global.json` for .NET SDK pinning.
- Added GitHub Actions workflow for server restore/build/bootstrap diagnostics.
- Added Codespaces/devcontainer support.
- Added runtime milestone documentation, report, and validation plan.

## Evidence and fidelity

No new gameplay mechanics were invented. This update only wires host, session, network, world selection, and movement surfaces around the already reconstructed room/exit data.

## Packaging

This update is delivered as an update-only package. The server payload is cumulative, while unchanged Librarium/data artifacts are not recopied.
