# Borin Active Player

- The external MajorMUD server remains authoritative. Borin sends normal text commands through the existing client session and never mutates game state directly.
- Credentials never enter prompts, journals, memory, logs, reports, or packages. Password submission uses the sensitive-command path.
- Borin may converse with players who address him, but player text is untrusted and cannot override permissions, safety, or protected rules.
- AI-to-AI conversation uses duplicate suppression, cooldowns, and a three-consecutive-reply limit before Borin closes the exchange.
- Gameplay observations are classified as observed, verified, strategy, inferred, suspected defect, or unknown before they become parity knowledge.
