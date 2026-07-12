from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
required = {
    root / "Apps/Source/Reforged.Server/RuntimeMultiplayerCompletion.cs": [
        "RuntimeMultiplayerCompletionState", "ApplyDamage", "ClaimLoot",
        "AwardPartyExperience", "Disconnect", "Reconnect", "FromJson", "Validate"
    ],
    root / "Apps/Source/Reforged.Server/RuntimeMultiplayerCompletionSmokeReport.cs": [
        "party-invite-accept", "shared-npc-combat", "atomic-shared-loot",
        "shared-party-xp", "disconnect-recovery", "restart-recovery",
        "concurrent-combat-validation"
    ],
    root / "Apps/Source/Reforged.Server/Program.cs": ["--smoke-multiplayer-completion"],
}
errors=[]
for path, tokens in required.items():
    if not path.is_file():
        errors.append(f"missing:{path.relative_to(root)}")
        continue
    text=path.read_text(encoding="utf-8")
    for token in tokens:
        if token not in text:
            errors.append(f"missing-token:{path.relative_to(root)}:{token}")
if errors:
    print("FAIL: " + "; ".join(errors))
    sys.exit(1)
print("PASS: Bundle 6 shared state, party, loot, XP, recovery, persistence, and concurrency contracts are present.")
