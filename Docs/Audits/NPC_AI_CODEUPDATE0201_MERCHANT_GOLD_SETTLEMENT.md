# CodeUpdate0201 — Exact NPC Merchant Gold Settlement

This pass adds a transaction boundary for an already-admitted merchant request. The economy owner must supply the exact positive amount and authority. Buy and repair debit actor carried currency and credit NPC gold; sell debits NPC gold and credits the actor.

The boundary validates the living NPC, active request ID, actor identity, service kind, amount, and both balances. Any failure restores both balances and leaves the active request available for an exact retry. Successful settlement completes the exact NPC service request.

No pricing modifier, merchant funding, restock rule, restriction, denomination conversion, or refusal dialogue is inferred.
