# Quest/Boss Respawn and Occupancy Hypothesis

DataSimVer0023 records these expected simulator rules as hypotheses only:

- Boss/unique monsters may use explicit timers, quest gates, or special persistence behavior.
- Quest monsters should not be assumed to follow normal creature respawn if quest state blocks them.
- Respawn should avoid creating duplicate monsters when a room or spawn slot is already occupied.
- Cleanup flags and respawn flags should be treated as separate until direct evidence proves they share fields.

Do not promote these semantics into `/Data` until confirmed by reverse engineering, runtime logs, or direct original engine evidence.
