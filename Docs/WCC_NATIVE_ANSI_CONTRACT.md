# Original WCC ANSI terminal contract

This note records derived terminal behavior from user-supplied original WCC/MajorMUD installation evidence. The original binaries/message/map assets are not redistributed in this repository.

## Evidence boundary

Evidence inspected locally includes the original MajorMUD runtime DLL plus its shipped message/help/text and ANSI map assets. These observations are stronger than screenshot-only parity because they expose the actual control sequences WCC emits.

## Confirmed terminal model

- Classic terminal baseline is 80 columns by 25 rows.
- Text/art assets depend on IBM/DOS code page 437 glyph semantics.
- Rendering must be stateful. Appending independently colored text lines is insufficient for original WCC behavior.
- Foreground and background ANSI colors are both used.
- WCC content uses bold/intensity and standard SGR resets.
- The terminal must preserve cursor position and screen cells across input chunks.

## Confirmed control families emitted by original WCC

The supplied WCC runtime/assets contain these terminal operations:

- `CSI J` including full-screen clear (`2J`)
- `CSI K` erase-to-end-of-line
- relative horizontal cursor movement (`CSI n C` / `CSI n D`), including large rewinds such as 79 columns
- absolute cursor positioning (`CSI row;column H`)
- save/restore cursor (`CSI s` / `CSI u`)
- scrolling-region selection (`CSI top;bottom r`)
- standard foreground/background SGR combinations
- mixed foreground/background map-art attributes
- carriage-return-driven line rewriting/layout

The existing RealmGate `ClassicTerminalEmulator` already implements these families plus related VT operations and is therefore the migration baseline for `RealmGate.Client.Core`.

## Rendering requirements

The cross-platform Play terminal must render an authoritative cell buffer rather than re-interpreting ANSI independently in WPF and Avalonia. Each cell/run needs at least:

- character/glyph
- foreground
- background
- bold/intensity
- inverse
- underline
- blink state
- conceal state

Cursor row/column, visibility, scroll region, origin mode and autowrap are terminal state, not presentation-layer state.

## Parity tests derived from original WCC evidence

Permanent client-core regressions should cover:

1. 80x25 default screen.
2. CP437 line/block glyph preservation.
3. foreground + background SGR combinations.
4. full-screen clear.
5. erase-to-end-of-line.
6. 79-column cursor rewind.
7. absolute row/column cursor positioning.
8. save/restore cursor.
9. scroll-region behavior used by WCC.
10. carriage return without line feed.
11. autowrap at column 80.
12. STATUS fixed-column alignment.
13. room/combat/inventory color/layout rules where independently evidenced.

## Provenance policy

Only derived behavioral facts and synthetic/minimal regression streams belong in source control. Original WCC binaries, message files, maps and other copyrighted installation assets remain external user-supplied evidence and must not be packaged into RealmGate.