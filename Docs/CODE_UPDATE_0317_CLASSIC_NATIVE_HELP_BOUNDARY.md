# Classic Native Help Boundary — Update 0317

PASS 46 promotes `native.help-reference` from UNKNOWN to STRONG.

The retained WCCMMHLP corpus contains 263 nonempty native records: one main help body and 131 complete title/body pairs. The Librarium compiler now emits those records as 132 typed `native_help` runtime records. The server consumes `Data/Classic/native_help.json` and no longer reads Librarium evidence directly for player help.

Exact main-screen aliases remain the previously proven narrow boundary. Unique exact keyword tokens resolve to their paired native body as STRONG compatibility behavior. Exact `HELP TOPICS` presentation, prefix/fuzzy matching, ambiguity precedence, pagination, textblock linkage, and permission filtering remain unresolved and fail closed.

Parity after this pass is 97 PASS / 87 STRONG / 16 UNKNOWN = 200. Strict parity remains 48.5%; evidence-backed parity is 92.0%.
