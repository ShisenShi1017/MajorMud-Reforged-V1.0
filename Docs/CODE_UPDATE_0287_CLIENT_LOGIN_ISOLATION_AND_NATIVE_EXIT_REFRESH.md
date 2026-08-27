# Code Update 0287 — Client Login Isolation and Native Exit Refresh

## Proven live defect

Live testing showed `clientdata stats`, `clientdata spells`, `clientdata party`,
`clientdata pvp`, and `roomdata` appearing in an Omniverse MajorMUD session as
ordinary player speech. The same eager traffic could collide with Reforged login.

## Root cause

`PlayViewModel.SessionStateChanged` treated TCP `Connected` as equivalent to a
completed MajorMUD login and immediately issued the five private Reforged client
protocol commands.

There were two independent violations:

1. TCP connected was not proof that login had completed.
2. Reforged-only `CLIENTDATA`/`ROOMDATA` commands were being sent to Omniverse,
   whose legacy command interpreter does not implement the private protocol.

## Repair

`ClientCommandIsolationPolicy` is now the central fail-closed boundary.

Before a real MajorMUD HP prompt or parsed room is observed, only:

- Human input; and
- `SystemLogin` input from the dedicated login controller

may leave the client. Borin gameplay, client automation, route automation, and
private protocol traffic remain blocked.

Private `CLIENTDATA`/`ROOMDATA` traffic is additionally allowed only for a
Reforged-capable server identity. Omniverse and unknown remote BBS hosts fail
closed and never receive those private commands.

The eager protocol refresh on TCP connect was removed. After gameplay is
verified, the client performs one bootstrap refresh:

- Reforged server: private `clientdata` / `roomdata` refresh.
- Omniverse / legacy BBS: normal `LOOK`, allowing the native MajorMUD room parser
  to recover the visible room title and `Obvious exits` line without protocol
  leakage.

Timed client automation also waits for verified gameplay rather than merely a
connected socket.

## Deterministic regression coverage

The Borin character harness now verifies that:

- Human and SystemLogin commands remain usable before login.
- Borin/client automation/private protocol commands are blocked before login.
- `[HP=51]:` is accepted as verified in-game evidence.
- Borin gameplay unlocks after that evidence.
- Omniverse never unlocks Reforged private protocol.
- A verified local Reforged session does unlock private protocol.
