# Global Random Service Exhaustion — Update0025 (superseded by PASS60 / Update0331)

Update0025 introduced `RuntimeGlobalRandomService`, a deterministic Reforged xorshift128+ multi-stream service with snapshots and replay. That service remains **Reforged extension infrastructure** and is not the Classic MajorMUD RNG owner.

The Classic native RNG boundary was subsequently recovered in Passes 34-35 and consolidated to **STRONG** in PASS60 / Update0331. See:

- `Docs/CODE_UPDATE_0331_CLASSIC_GLOBAL_RNG_CONTRACT_BOUNDARY.md`
- `Reports/RemainingSystems/global_random_contract_exhaustion_0037.md`
- `Librarium/Parity/majormud_classic_global_rng_contract_boundary_evidence.json`

Exact gameplay caller-to-thread mapping, indirect paths, instruction-equivalent bounded-range helper execution, and gameplay-wide draw ordering remain evidence-gated.
