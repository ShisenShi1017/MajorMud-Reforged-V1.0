# Runtime Required Register

The following behaviors remain runtime_required unless exact source-backed logic is recovered:

| Area | Behavior | Why unresolved |
|---|---|---|
| Combat | Hit chance | Needs exact engine formula or runtime observation |
| Combat | Damage variance | Requires RNG/formula details |
| Combat | Critical hits | Probability/multiplier not proven |
| Combat | Mitigation order | Must be source-backed or tested in original runtime |
| RNG | Random sequence/seed | Original runtime behavior unavailable |
| Timing | Tick/event scheduling | Runtime timing behavior unavailable |
| Magic | Fizzle/resist/interrupt | Dynamic engine behavior not proven |
| Healing | Scaling/rounding | Requires executable formula hook/source proof |
| Sneak | Sneak/backstab success | Dynamic probability/conditions not proven |
| Economy | Training costs | Requires source-backed formula or runtime observation |
| Economy | Banking edge cases | Dynamic transaction behavior not proven |
| Quests | Dynamic state mutation edge cases | Requires runtime event/state observation |
