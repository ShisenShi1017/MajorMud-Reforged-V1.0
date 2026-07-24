# Worldgroup Native Closure Update0005 — Callback Registration and Hook Dispatch

## Proven findings

- Eight lifecycle hook exports are thin cdecl wrappers over the shared dispatcher at `0x004381F2`.
- Each wrapper passes a distinct hook-list head (`0x0045D3C0` through `0x0045D3DC`) and the address of its first argument, preserving varargs forwarding.
- `_register_pseudok`, `_register_pseudok2`, and `_register_textvar` use 20-byte entries: a 16-byte copied identifier and a callback pointer at offset `0x10`.
- `_register_stascn` is a growable array of four-byte callback pointers.
- `_register_agent` copies 28-byte descriptors and installs host defaults for missing callback slots.
- `_register_module` validates a 3–24 character module name and installs a host default at descriptor offset `0x24` when null.
- `_registerDSModule` stores 12-byte records and grows capacity in groups of ten.

## Runtime impact

The host callback boundary is now represented explicitly in runtime evidence instead of as unidentified imported globals. The exact shared dispatcher node layout and callback return-policy remain fail-closed until the next implementation trace.
