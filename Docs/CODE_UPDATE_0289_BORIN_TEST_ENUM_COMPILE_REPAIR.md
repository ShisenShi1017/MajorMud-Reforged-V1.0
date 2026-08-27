# Code Update 0289 — Borin Test Enum Compile Repair

## Proven build failure

`BorinCharacterTestHarness.cs` contained two stale references to
`ClientCommandOrigin.Borin`.

The current shared client command-origin enum uses:

`ClientCommandOrigin.AutonomousAgent`

Production Borin command routing already uses the current enum member.

## Repair

Both regression-test assertions in `TestClientLoginIsolationPolicyAsync`
now use `ClientCommandOrigin.AutonomousAgent`.

No runtime behavior, login sequencing, protocol isolation, room parsing,
or autonomous-player behavior is changed by this repair.

## Expected result

The two CS0117 build errors reported at the login-isolation regression test
are removed. The existing CS1998 warning in `BorinPlayerSessionLifecycle.cs`
is unrelated and remains non-blocking.
