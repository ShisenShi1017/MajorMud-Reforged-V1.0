# RE0021 Packet Evidence Expansion Validation Report

## Scope
RE0021 is a bounded follow-up to RE0020. It expands protocol evidence without inventing binary packets. The inspected evidence still supports a Telnet/text-command model and route-script command emissions, not opcode-level packet layouts.

## Baseline
- Input Living baseline: `ReforgedRealms-MegaMUD2-Living-RE0020-ClientServerProtocol-Real-Cumulative.zip`.
- Evidence source: frozen `ReforgedRealms-MegaMUD2-ReferencePack-RE0016(2).zip`.

## Evidence processed in this pass
- `1958` `.mp` route/path script files from the Reference Pack.
- `47760` command-bearing `.mp` rows parsed into `Data/RE0021_MP_COMMAND_LEDGER.csv`.
- `155` distinct first-token command categories summarized in `Data/RE0021_MP_COMMAND_TOKEN_COUNTS.csv`.
- `192` BBS protocol/profile keys captured in `Evidence/RE0021_BBS_PROTOCOL_PROFILE_KEYS.csv`.
- `1550` trace protocol events captured in `Evidence/RE0021_TRC_PROTOCOL_EVENT_TIMELINE.csv`.
- `219` observed or unpaired Telnet session windows captured in `Data/RE0021_TRC_SESSION_WINDOWS.csv`.

## Verified findings
1. Path scripts contain client-issued text commands associated with route nodes. These include movement directions and literal command strings such as party/buy/drop actions where present.
2. Movement commands are represented as text tokens in `.mp` evidence. Counted direction-token rows: `46248`.
3. Non-direction command rows were also observed: `1512`. These remain text-command evidence, not binary packet evidence.
4. BBS profiles continue to support a prompt/reply Telnet login model, including Telnet host/port, menu prompt, command prefixes, and max input size configuration.
5. TRC evidence continues to confirm connection/session lifecycle behavior and parser-visible WHO events.

## Top command tokens from route scripts
`w`=7303, `e`=7264, `n`=6883, `s`=6831, `sw`=4361, `ne`=4343, `nw`=3828, `se`=3805, `d`=820, `u`=810, `go`=156, `W`=88

## Unknowns preserved
- No raw byte-level wire capture was promoted.
- No opcode map, packet header size, encryption, compression, binary field offsets, or checksums were inferred.
- `.mp` hash/state fields preceding commands are cataloged but not decoded in this pass.

## Deliverables added
- `Data/RE0021_MP_COMMAND_LEDGER.csv`
- `Data/RE0021_MP_COMMAND_TOKEN_COUNTS.csv`
- `Data/RE0021_TRC_SESSION_WINDOWS.csv`
- `Evidence/RE0021_BBS_PROTOCOL_PROFILE_KEYS.csv`
- `Evidence/RE0021_TRC_PROTOCOL_EVENT_TIMELINE.csv`
- `Documentation/RE0021_Packet_Evidence_Expansion_Notes.md`
- `Reports/RE0021_PACKET_EVIDENCE_EXPANSION_REPORT.md`
- `Codex/RE0022_NEXT_EXECUTION_TARGETS.md`
- `SESSION_HANDOFF_RE0021.md`
