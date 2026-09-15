# Cross-Platform Beta Networking and Combat Terminal Contract

This document records the beta compatibility contract introduced for the WPF-to-Avalonia migration.

## Networking

- Default listener: `0.0.0.0:2323`.
- Local-only preset: `127.0.0.1`.
- LAN/remote preset: `0.0.0.0`.
- A specific IPv4 interface may be configured explicitly.
- RealmGate does not change router port forwarding, UPnP, or firewall rules automatically.
- Bind failures must report the requested address, port, and socket error without crashing the application.
- Local clients remain classified from their actual remote endpoint even when the listener is bound to all IPv4 interfaces.

## Combat terminal completion

When asynchronous combat ends, the session layer must finish the current terminal line, emit the final combat/reward output, restore exactly one normal command prompt, and keep the existing pending input read active. No synthetic Enter/input is injected.

Telnet application output uses CP437 and CRLF line endings through the centralized terminal writer.

## Permanent regression gates

Ubuntu server CI verifies:

- local-only, all-interface, and specific-interface binding;
- invalid address and occupied-port handling;
- listener shutdown and same-port rebind;
- combat completion CRLF bytes and message ordering;
- no duplicate prompt or extra blank line;
- prompt restoration categories for melee, spell, DOT, flee, despawn, cancellation, movement, death, grouped combat, final enemy, reconnect, and script termination;
- existing combat-completion gameplay behavior.

These runtime contracts are UI-framework independent and are intended to be reused by the Avalonia desktop application.
