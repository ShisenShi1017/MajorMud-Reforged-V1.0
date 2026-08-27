# Code Update 0291 — Borin Omniverse Exit Verification Repair

## Live defect

Borin could answer `The exact visible exits are .` because the client stamped
`ExitsUpdatedAt` whenever a room display closed, even if the native exit text
produced zero recognized directions. That made an empty parse look authoritative.

## Repair

- Room observations now carry `ExitsVerified` and the raw native exit text.
- Zero parsed directions are authoritative only when the server explicitly reports
  `none`, `no exits`, or `nowhere`.
- Unrecognized native exit text leaves exits unverified, which allows the existing
  Forge perception refresh path to issue another real `LOOK` instead of answering
  from an empty list.
- Native exit parsing now recognizes decorated direction tokens such as
  `open door west`, `north (closed)`, and `[east]`.
- The session retains the last raw exit-list text for diagnostics.
- Forge can no longer render the grammatically empty sentence; a verified zero-exit
  room is described explicitly as having no visible exits.
- LOOK/movement evidence accepts an explicitly verified zero-exit room without
  requiring `CurrentRoomExits.Count > 0`.

## Regression coverage

The existing visible-exit parser test now covers ordinary exits, decorated native
exits, malformed/unrecognized exit text, and explicit no-exit rooms.
