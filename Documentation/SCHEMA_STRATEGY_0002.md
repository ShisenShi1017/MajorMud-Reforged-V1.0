# Runtime Schema Strategy 0002

The runtime schema layer is an adapter layer between `/Data` and executable systems.

Rules:
1. `/Data` remains canonical.
2. Adapters normalize names and references only.
3. Raw source records are preserved on every schema object.
4. Missing or unresolved references become validation issues.
5. No classic formula is promoted unless evidence-backed.
6. Reforged formulas must be explicitly tagged as Reforged and not classic-verified.

Primary runtime IDs are preserved from source when available. Legacy numeric IDs are indexed for cross-reference resolution.

This milestone enables later systems to consume stable objects instead of repeatedly parsing raw JSON.
