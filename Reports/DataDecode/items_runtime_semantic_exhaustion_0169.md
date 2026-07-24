# Items Runtime Semantic Exhaustion — CodeUpdate0169

## Implemented

CodeUpdate0169 adds identity-preserving bank and merchant stock locations and atomic item-side transitions for deposit, withdrawal, sale, and purchase. Carried-state checks, explicit no-bank/no-sell metadata, binding, quest ownership, merchant ownership, rollback, validation, smoke coverage, Librarium policy, and provenance are included.

Pricing, merchant acceptance formulas, currency movement, restock scheduling, bank fees, and capacity remain outside this pass or blocked on additional evidence.

## Evidence classification

Strongly correlated and active only when explicit metadata or authoritative runtime location state is present. Unknown native flags and exact Classic messages remain fail-closed for the final native pass.
