# Code Update 0301 — Classic Broadcast Groups

## Scope

Pass 30 closes the exact, shared portion of the native MajorMUD broadcast-group command model using retained WCCMMHLP help bodies 123, 157 and 217. It does not infer group 127, native persistence layout, or exact presentation strings.

## Proven command semantics

- `JOIN <n>` selects a broadcast group.
- `JOIN 0` returns the character to the default broadcast group.
- Groups `1..126` are unambiguously supported by both relevant help bodies.
- `LEAVE` while not following a party switches the character to group 0.
- `BROADCAST <text>` sends to current members of the selected nonzero broadcast group (`1..126`).
- Bare `BROADCAST` lists the current people on that selected nonzero broadcast group.
- `BR` is an explicit native alias for `BROADCAST`.

## Group 127 remains evidence-gated

Native help is internally inconsistent. Message 123 states `1..127`; message 157 states `1..126`. Pass 30 activates only the uncontested intersection, `0..126`. A request for group 127 returns an evidence-conflict boundary rather than silently accepting or rejecting it as native fact.

## Runtime changes

`RuntimeClassicBroadcastGroupAuthority` owns the exact numeric evidence boundary. Classic `JOIN <n>` now persists the selected broadcast group, and party-less `LEAVE` resets it to zero. `BROADCAST` moved from an identity-only native evidence gate to the communication runtime and routes/list-members by selected group.

Current persistence uses the existing character `Conditions` surface. That is a Reforged representation of a proven runtime value, not a claim about the original MajorMUD save-record field or save ordering.

## Still unresolved

Exact native broadcast behavior while group 0 is selected, list formatting, send formatting, failure prose, persistence field ownership/order, and the validity of group 127 remain unresolved. The broader command-table, party-coordination, and communication-persistence UNKNOWN assertions remain intact.
