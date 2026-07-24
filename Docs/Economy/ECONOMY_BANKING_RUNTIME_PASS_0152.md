# Economy Banking Runtime Pass 0152

## Implemented

This pass adds the authoritative banking execution layer on top of the denomination-aware currency runtime.

- Bank policies bind canonical policy and currency IDs.
- Accounts bind canonical account and bank IDs, one or more owners, balance, version, and applied transaction IDs.
- Shared account access can be added or removed only by an existing owner; an account cannot lose its final owner.
- Deposits validate access, currency, minimums, maximum transaction size, wallet funds, fees, and account overflow before commit.
- Withdrawals validate access, currency, minimums, maximum transaction size, account funds including fee, and wallet overflow before commit.
- Account transfers require distinct same-currency accounts and canonical lock ordering.
- Every economic operation snapshots all participating state and restores it on any exception.
- Persisted transaction IDs prevent a retried request from mutating balances twice.
- Versioned persistence validates account, bank, currency, ownership, balance, version, and transaction references.

## Execution order

Deposit: validate -> authorize -> replay check -> limit check -> fee quote -> snapshot -> wallet debit -> account credit -> transaction mark -> commit.

Withdrawal: validate -> authorize -> replay check -> limit check -> fee quote -> snapshot -> account debit -> wallet credit -> transaction mark -> commit.

Account transfer: validate -> authorize source -> replay check -> limit check -> snapshot both accounts -> source debit -> destination credit -> transaction mark -> commit.

## Evidence boundary

The atomic ownership and rollback model is promoted as strongly correlated with the existing currency and commerce runtimes. No native MajorMUD bank fee, limit, interest, service-hour, or cross-bank constant is invented. Those values remain explicit policy inputs and are tracked as Unknown (Native Dependency).

## Persistence impact

Schema version 1 persists account identity, bank and currency references, all owners, base-unit balance, monotonic version, and applied transaction IDs. Deserialization fails closed on duplicate or malformed canonical references and invalid state.

## Validation

The pass validates JSON, Python syntax, runtime contracts, canonical bank/account/currency/owner references, shared ownership, fee arithmetic, limits, atomic rollback, transaction replay protection, persistence, cumulative manifest completeness, and ZIP integrity.
