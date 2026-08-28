# Classic Native Player Command Boundary — Update 0318

PASS 47 promotes `commands.native-player-command-table` from UNKNOWN to STRONG.

WCCMMHLP message 3 proves 61 unique command-summary identities. Message 123 separately proves BROADCAST and BROADGANG, producing 63 documented player-command inputs. It also prints the exact aliases BS for BACKSTAB, BR for BROADCAST, and BG for BROADGANG.

The Librarium compiler now emits those identities as `native_player_commands.json`. Runtime validation proves all 63 full-name inputs and all three explicit aliases resolve through the live command parser. This pass adds no invented command behavior and does not treat compatibility aliases as native unless WCCMMHLP prints them.

Exact undocumented aliases, minimum prefixes, ambiguity precedence, hidden dispatcher entries, and native argument validation/failure ordering remain unresolved. Parity after this pass is 97 PASS / 88 STRONG / 15 UNKNOWN = 200. Strict parity remains 48.5%; evidence-backed parity is 92.5%.
