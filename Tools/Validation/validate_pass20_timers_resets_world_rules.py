#!/usr/bin/env python3
import json
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
checks = []
failures = []

def check(name, condition, detail=""):
    checks.append({"name": name, "passed": bool(condition), "detail": detail})
    if not condition:
        failures.append(name + (": " + detail if detail else ""))

def text(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def load(rel):
    return json.loads(text(rel))

native_support = load("Librarium/Evidence/native_support_catalog_0020.json")
native_help = load("Librarium/Evidence/native_message_catalog_correlation_0022.json")
timing = text("Apps/Source/Reforged.Server/RuntimeClassicTimingAuthority.cs")
host = text("Apps/Source/Reforged.Server/ServerRuntimeHost.cs")
regen = text("Apps/Source/Reforged.Server/RuntimeCharacterRegeneration.cs")
cmd = text("Apps/Source/Reforged.Server/RuntimeCommandSystem.cs")
living = text("Apps/Source/Reforged.Server/RuntimeLivingWorld.cs")
spawn = text("Apps/Source/Reforged.Server/RuntimeNpcSpawnLifecycle.cs")
spawn_timing = text("Apps/Source/Reforged.Server/RuntimeMajorMudSpawnTiming.cs")
timer_audit = text("Apps/Source/Reforged.Server/RuntimeMajorMudTimerAudit.cs")
parity_audit = text("Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs")
remaining = load("Librarium/RemainingSystems/remaining_systems_registry.json")
ledger = load("Librarium/Parity/majormud_parity_assertions_0290.json")
evidence = load("Librarium/Parity/majormud_timers_resets_world_rules_evidence_0290.json")
score = load("Reports/RuntimeVerification/majormud_parity_scorecard_0290.json")

# Direct native WCCMMUD configuration strings.
wcc = next(c for c in native_support.get("configuration_catalogs", []) if c.get("file", "").lower() == "wccmmud.mcv")
prompts = {p.get("text", "") for p in wcc.get("configuration_prompts", [])}
check("native player save prompt 60s", "Seconds between saving Player data? 60" in prompts)
check("native buffer save prompt 300s", "Seconds between saving Buffer data? 300" in prompts)
check("timing authority player save constant", "PlayerDataSaveIntervalSeconds = 60" in timing)
check("timing authority buffer save constant", "BufferDataSaveIntervalSeconds = 300" in timing)
check("host uses native player-save interval", '"character-autosave", RuntimeClassicTimingAuthority.PlayerDataSaveInterval' in host)
check("host autosave admission uses same native interval", "AutosaveSessions(_sessions.ActiveSessions, DateTimeOffset.UtcNow, RuntimeClassicTimingAuthority.PlayerDataSaveInterval)" in host)
check("old five-minute character autosave removed", '"character-autosave", TimeSpan.FromMinutes(5)' not in host)

# Native HELP REST/MEDITATE contract and runtime repair.
help_cat = next(c for c in native_help.get("catalogs", []) if c.get("mcv_file", "").lower() == "wccmmhlp.mcv")
help_text = "\n".join(e.get("decoded_cp437", "") for e in help_cat.get("entries", []) if e.get("defined"))
check("native rest activation says 20 seconds", "remain resting for at least 20 seconds" in help_text)
check("native meditate activation says 20 seconds", "remain meditating for at least 20 seconds" in help_text)
check("native rest interruption names move attack attacked", "If you move, attack, or are attacked" in help_text)
check("focused activation uses timing authority", "FocusedRegenerationActivationDelay" in regen)
check("ordinary commands no longer blanket-cancel focus", "NonInterruptingFocusedRegenerationCommands" not in regen)
check("movement flag controls command-side focus cancellation", "NotifyCharacterAction(RuntimeSession session, string commandName, bool isMovement)" in regen and "if (!isMovement) return;" in regen)
check("dispatcher supplies movement flag", "NotifyCharacterAction(session, command.Name, command.IsMovement)" in cmd)
check("PVE combat entry cancels focus", "CancelFocusedRegeneration(restingActor.Id)" in text("Apps/Source/Reforged.Server/RuntimeLiveCombat.cs"))
check("PVP combat entry cancels both sides", "CancelFocusedRegeneration(attackerActor.Id)" in text("Apps/Source/Reforged.Server/RuntimeLivePvp.cs") and "CancelFocusedRegeneration(defenderActor.Id)" in text("Apps/Source/Reforged.Server/RuntimeLivePvp.cs"))

# Room-entry NPC respawn admission.
kb = load("Librarium/MajorMUDKnowledgeBase/phase1_exhaustive_evidence_hits.json")
last_killed = [x for x in kb if isinstance(x, dict) and "regen the next time someone walks into the room" in x.get("text", "")]
check("Last Killed room-entry evidence retained", len(last_killed) >= 1)
correlation = text("Librarium/BinaryCorrelation0085/wcc_wbb_binary_correlation_0085.json")
check("native Last Killed Delay diagnostic retained", "Last Killed: %s Delay: %d" in correlation)
check("native minutes-before-regen diagnostic retained", "Minutes before regenerating: %d minutes" in correlation)
check("room-entry respawn API exists", "AdmitDueNpcRespawnsForRoom" in spawn)
check("room-entry admission filters exact home room", "x.HomeRoomId.Equals(admissionRoomId, StringComparison.OrdinalIgnoreCase)" in spawn)
check("global living tick no longer respawns due NPCs", "events.AddRange(TickNpcSpawnLifecycleLocked(nowUtc, null)" not in living)
check("player movement admits destination respawns", "AdmitClassicRoomEntryNpcRespawns(session)" in cmd)
check("party follower movement admits destination respawns", "AdmitClassicRoomEntryNpcRespawns(follower)" in cmd)
check("admission occurs before leader room render", cmd.index("AdmitClassicRoomEntryNpcRespawns(session)") < cmd.index("movementResult = _gameplay?.RenderRoom(session)"))
check("zero-delay remains UNKNOWN", '"generated-room-delay-zero"' in spawn_timing and '"UNKNOWN"' in spawn_timing)
check("room-entry pending flag closed in timing projection", spawn_timing.count("NativeRoomEntryAdmissionPending") >= 1 and "Room-entry admission is active" in spawn_timing)
check("timer audit reports player save interval", "Native WCCMMUD player-data save interval" in timer_audit)
check("timer audit says global heartbeat no longer materializes", "do not materialize from the global heartbeat" in timer_audit)

# Ledger / score / unresolved boundaries.
assertions = {a["id"]: a for a in ledger.get("assertions", [])}
check("ledger update 0290", ledger.get("update") == "0290")
check("player save assertion PASS", assertions.get("timers.player-data-save-60s", {}).get("state") == "PASS")
check("room-entry assertion STRONG", assertions.get("timers.room-entry-respawn-admission", {}).get("state") == "STRONG")
check("clock scheduling remains UNKNOWN", assertions.get("native.clock-scheduling", {}).get("state") == "UNKNOWN")
check("world reset remains UNKNOWN", assertions.get("native.world-reset-cleanup", {}).get("state") == "UNKNOWN")
check("world rules config remains UNKNOWN", assertions.get("native.world-rules-configuration", {}).get("state") == "UNKNOWN")
check("save coordination remains UNKNOWN", assertions.get("native.save-coordination", {}).get("state") == "UNKNOWN")
check("runtime parity audit points at 0290", "majormud_parity_assertions_0290.json" in parity_audit)
check("scorecard counts", (score.get("PassCount"), score.get("StrongCount"), score.get("UnknownCount"), score.get("ScoredRequirementCount")) == (72, 70, 44, 186))
check("scorecard percentages", (score.get("StrictPercent"), score.get("EvidenceBackedPercent")) == (38.7, 76.3))
check("Pass20 evidence update", evidence.get("update") == "0290")
check("Pass20 evidence names direct native save", any(x.get("classification") == "PASS" for x in evidence.get("native_wccmmud_configuration", {}).get("direct_findings", [])))
check("remaining registry updated", remaining.get("updatedByPass") == "Pass 20 — Timers / Resets / World Rules Closure")

# Ensure generated JSON artifacts parse.
for rel in [
    "Librarium/Parity/majormud_parity_assertions_0290.json",
    "Librarium/Parity/majormud_timers_resets_world_rules_evidence_0290.json",
    "Librarium/RemainingSystems/remaining_systems_registry.json",
    "Reports/RuntimeVerification/majormud_parity_scorecard_0290.json",
]:
    try:
        load(rel)
        check("json parses: " + rel, True)
    except Exception as exc:
        check("json parses: " + rel, False, str(exc))

result = {
    "schema": "majormud.pass20-timers-resets-world-rules-validation.v1",
    "update": "0290",
    "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    "passed": not failures,
    "checkCount": len(checks),
    "failureCount": len(failures),
    "checks": checks,
    "failures": failures,
}
out = ROOT / "Reports/RuntimeVerification/timers_resets_world_rules_pass20_static_validation_0290.json"
out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(f"Pass20 static validation: {'PASS' if not failures else 'FAIL'} {len(checks)-len(failures)}/{len(checks)}")
for f in failures:
    print("FAIL:", f)
raise SystemExit(0 if not failures else 1)
