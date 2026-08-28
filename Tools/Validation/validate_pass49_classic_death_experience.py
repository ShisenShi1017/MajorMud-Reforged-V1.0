import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
failures = []


def require(value, name):
    if not value:
        failures.append(name)


evidence = json.loads((root / "Librarium/Parity/majormud_classic_death_experience_evidence_0320.json").read_text(encoding="utf-8"))
assertions = json.loads((root / "Librarium/Parity/majormud_parity_assertions_0320.json").read_text(encoding="utf-8"))
authority = (root / "Apps/Source/Reforged.Server/RuntimeClassicDeathExperienceAuthority.cs").read_text(encoding="utf-8")
death = (root / "Apps/Source/Reforged.Server/RuntimeCharacterDeath.cs").read_text(encoding="utf-8")
smoke = (root / "Apps/Source/Reforged.Server/RuntimePass49ClassicDeathExperienceSmokeReport.cs").read_text(encoding="utf-8")

require(evidence["ordinaryDeath"]["exactExperienceLoss"] == 0, "ordinary-death-zero-loss")
require(evidence["finalDeathOrReroll"]["selection"]["formula"].startswith("max("), "final-reroll-max-selection")
require(evidence["finalDeathOrReroll"]["twoMillionCapPresent"] is False, "no-two-million-cap")
require(evidence["parityPromotion"]["to"] == "STRONG", "strong-promotion")

current = {row["id"]: row for row in assertions["assertions"]}
require(current["character.death-xp-loss-exact"]["state"] == "STRONG", "assertion-strong")
require("PreserveOrdinaryDeathExperience" in authority, "ordinary-authority")
require("CalculateFinalRerollRetainedExperience" in authority, "final-reroll-calculator")
require("currentExperience / 10" in authority, "ten-percent-floor")
require("Math.Max(halfLevelThreshold, tenPercentFloor)" in authority, "max-selection-runtime")
require("ExperienceLost" in death and "ordinaryDeathExperience" in death, "death-resolver-zero-loss-marker")
require("final-reroll-consumer-remains-explicitly-gated" in smoke, "consumer-gate-smoke")

print(json.dumps({"passed": not failures, "checks": 12, "failures": failures}, indent=2))
raise SystemExit(0 if not failures else 1)
