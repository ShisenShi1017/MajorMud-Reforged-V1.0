# Capacity and Resource Limits — Update0033

Implemented owner-scoped capacity definitions, reservations, commits, releases, checked arithmetic, hard-limit admission rejection, host-authorized configuration changes, invariant validation, and executable smoke coverage.

## Ownership

The capacity coordinator owns admission reservations and aggregate counters. Character, NPC, scheduler, party, organization, messaging, event, and global-state subsystems retain truth and lifecycle ownership for their resources.

## Native dependencies

Exact MajorMUD 1.11p limits, static versus configurable behavior, integer widths, allocation-failure handling, cleanup behavior, and exact admission messages remain for the final native pass.
