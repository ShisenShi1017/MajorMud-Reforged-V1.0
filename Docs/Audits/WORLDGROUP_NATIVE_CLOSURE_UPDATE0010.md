# Worldgroup Native Closure Update0010 — WCCMMUD Session-State Dispatcher

Static analysis of the supplied `wccmmud.dll` (5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077) closes the session-state jump-table boundary at `0x00405221`.

## Proven dispatch layout

- Session state is read from the Worldgroup session structure at offset `0x1C`.
- Values greater than `0x88` bypass the table to `0x0040537B`.
- The 137-byte selector table spans `0x00405221–0x004052A9`.
- The selector indexes a 38-entry target table beginning at `0x004052AA`.
- The table resolves 137 state values to 37 distinct code targets plus the shared fallback.

## Active gameplay boundary

State `0x82` dispatches to `0x004088CC`. That handler calls internal routine `0x0045003C`, performs the common input cleanup, and returns the routine result. States `0x83–0x88` are modal input/confirmation handlers; each validates input and restores state `0x82` when its operation completes or is cancelled. This makes `0x0045003C` the next proven gameplay parser/executor boundary.

## Exact high-state mapping

| State | Target | Role |
|---|---:|---|
| `0x82` | `0x004088CC` | Active gameplay/default execution |
| `0x83` | `0x004087E6` | Modal confirmation/input |
| `0x84` | `0x004085C4` | Modal input |
| `0x85` | `0x004086DC` | Modal input |
| `0x86` | `0x004084C8` | Modal input |
| `0x87` | `0x00408853` | Modal confirmation/input |
| `0x88` | `0x004083DC` | Modal confirmation/input |

The complete 137-state mapping is preserved in the machine-readable evidence file.
