#!/usr/bin/env python3
import json
import re
from collections import Counter
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

cmd = text("Apps/Source/Reforged.Server/RuntimeCommandSystem.cs")
routes = text("Apps/Source/Reforged.Server/RuntimeCommandRouting.cs")
live = text("Apps/Source/Reforged.Server/RuntimeLiveWorld.cs")
live_gameplay = text("Apps/Source/Reforged.Server/RuntimeLiveGameplay.cs")
party_game = text("Apps/Source/Reforged.Server/RuntimeClassicPartyGameplay.cs")
party = text("Apps/Source/Reforged.Server/RuntimePartyCoordination.cs")
party_smoke = text("Apps/Source/Reforged.Server/RuntimePartyCoordinationSmokeReport.cs")
comm = text("Apps/Source/Reforged.Server/RuntimeCommunication.cs")
comm_smoke = text("Apps/Source/Reforged.Server/RuntimeCommunicationSmokeReport.cs")
transport = text("Apps/Source/Reforged.Server/RuntimeTransportServices.cs")
transport_smoke = text("Apps/Source/Reforged.Server/RuntimeTransportServiceSmokeReport.cs")
audit = text("Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs")
share = text("Apps/Source/Reforged.Server/RuntimeClassicShare.cs")
characters = text("Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs")
multiplayer = text("Apps/Source/Reforged.Server/RuntimeMultiplayerCompletion.cs")
formula_records = load("Librarium/Objects/formulas/formula_records_0123.json")

# Global command -> SAY fallback.
check("global fallback event exists", '"command-fallback-say"' in cmd)
check("fallback preserves original prompt", 'Say(session, input ?? string.Empty)' in cmd)
check("fallback happens after valid registration test", 'CommandCatalog.IsRegistered(command.Name)' in cmd)
check("speech prefix action checked before fallback", cmd.index("TryExecuteMajorMudSpeechShortcut") < cmd.index('"command-fallback-say"'))
check("data-driven action checked before fallback", cmd.index("TryExecuteUnregisteredAction") < cmd.index('"command-fallback-say"'))
check("raw input is retained", "RawInput" in cmd and "rawInput" in cmd)
check("resolution error no longer directly returned", "return command.ResolutionError;" not in cmd)

# Native speech/channel audit.
check("dot SAY shortcut", 'raw.StartsWith(".", StringComparison.Ordinal)' in live and 'Say(session, raw[1..])' in live)
check("quote YELL shortcut", 'raw.StartsWith("\\\"", StringComparison.Ordinal)' in live and 'Yell(session, raw[1..])' in live)
check("slash telepath shortcut", 'raw.StartsWith("/", StringComparison.Ordinal)' in live and 'Telepath(session' in live)
check("YELL current plus adjacent", "session.CurrentRoom.Exits" in live and "roomIds.Contains(target.CurrentRoom.Id)" in live)
check("GOSSIP command wired", '"gossip" => _gameplay?.Gossip' in cmd and 'new("gossip", [], 6)' in cmd)
check("AUCTION command wired", '"auction" => _gameplay?.Auction' in cmd and 'new("auction", [], 7)' in cmd)
check("FORGET command wired", '"forget" => _gameplay?.ForgetTelepath' in cmd and 'new("forget", [], 6)' in cmd)
check("native TELL is not registered", 'new("tell"' not in cmd and '"tell" =>' not in cmd)
check("generic WHISPER is evidence gated", 'communication-whisper-native-unresolved' in comm)
check("generic TELL is evidence gated", 'communication-tell-native-unresolved' in comm)
check("generic SHOUT is evidence gated", 'communication-shout-native-range-unresolved' in comm)
check("SET TELEPATH polarity is gated", "SET TELEPATH is native, but its exact ON/OFF blocking polarity is not yet recovered." in live)
check("GOSSIP/AUCTION settings wired", 'raw.StartsWith("gossip"' in live and 'raw.StartsWith("auction"' in live)
check("compatibility communication default not labeled Classic", "RuntimeCommunicationPolicy ClassicSafeDefault" not in comm and "CompatibilityShellDefault" in comm)

# Command-table / abbreviation boundary.
gated = ["drag","go","health","map","profile","track","aid","light","stash","mana","invoke","read","suicide","reroll","appraise","eat","drink","rob","ready","exits","picklock","page","broadcast","scan","quiet","stock","unstock","markup"]
for name in gated:
    registered = (
        re.search(rf'new\("{re.escape(name)}", \[\], {len(name)}\)', cmd) is not None
        or name == "ready"
        or (name == "broadcast" and 'new("broadcast", ["br"], 9)' in cmd)
    )
    check(f"native identity registered: {name}", registered)
    check(f"native identity routed: {name}", re.search(rf'\b{re.escape(name)}\b', routes) is not None)
check("READY no longer diagnostic classification", '"audit" or "ready" or "worldstate"' not in cmd and '"ready"' in routes[routes.index("NativeEvidenceGate"):])
check("UNFOLLOW not registered", 'new("unfollow"' not in cmd)
check("native BR alias belongs to BROADCAST", 'new("broadcast", ["br"], 9)' in cmd)
check("BR alias removed from BRIEF", 'new("brief", ["br"]' not in cmd and 'new("brief", [], 5)' in cmd)
check("ambiguous resolution falls through SAY", 'if (!registeredCommand)' in cmd and 'Say(session, input ?? string.Empty)' in cmd)
check("Classic HELP fails closed instead of synthetic fallback", 'if (classicWorld)' in cmd and 'Native HELP topic resolution for' in cmd and cmd.index('Native HELP topic resolution for') < cmd.index('Available commands:'))

# Recovered SHARE semantics (WCCMMHLP message 227).
check("SHARE command registered", 'new("share", [], 5)' in cmd)
check("SHARE routed to live runtime", '"share" => _gameplay?.ExecuteClassicShare' in cmd and re.search(r'RuntimeCommandRouteKind\.CommunicationRuntime[\s\S]{0,800}\"share\"', routes) is not None)
check("SHARE defaults to PARTY", 'var scope = "party";' in share)
check("SHARE supports WITH ROOM/PARTY", 'scope is not ("room" or "party")' in share)
check("SHARE integer division preserves remainder", 'var perRecipient = parsed.Amount / eligible.Length;' in share and 'var remainder = parsed.Amount - distributed;' in share)
check("SHARE excludes hidden recipients", 'IsClassicShareHidden(target)' in share)
check("SHARE excludes RECEIVE-off recipients", 'IsClassicReceiveEnabled(target)' in share)
check("SHARE preserves exact denomination", 'AdjustDenomination' in share and 'ReadDenomination' in share and 'silently making change' in share)
check("SHARE uses batch transaction", 'ExecuteCharacterBatchTransaction' in share and 'ExecuteCharacterBatchTransaction' in characters)
check("batch transaction has rollback", 'character-batch-transaction' in characters and 'originals[id]' in characters)

# Party semantics / non-guesses.
check("JOIN GANG routes gang", '"join" when command.Arguments.FirstOrDefault()?.Equals("gang"' in cmd)
check("LEAVE GANG routes gang", '"leave" when command.Arguments.FirstOrDefault()?.Equals("gang"' in cmd)
check("JOIN and LEAVE route party otherwise", '"join" or "leave" => _gameplay?.ExecuteClassicPartyCommand' in cmd)
check("JOIN and FOLLOW both enable movement-follow", party_game.count('followingLeader: true') >= 2 and 'followingLeader: false' not in party_game)
check("TRACK remains distinct evidence-gated command", '"track"' in cmd and 'track <user>' in cmd)
check("broadcast JOIN is evidence gated", "broadcast-group JOIN is recovered" in party_game)
check("leader LEAVE succession is evidence gated", "party-leader LEAVE succession behavior is not yet recovered" in party_game)
check("no invented invite timeout", "InviteLifetime: null" in party and "TimeSpan.FromMinutes(2)" not in party)
check("no invented six-member cap", "MaximumMembers: int.MaxValue" in party and "MaximumMembers: 6" not in party)
check("leader transfer disabled by evidence-bounded default", "LeaderMayTransfer: false" in party)
check("default party policy labeled compatibility shell", "CompatibilityShellDefault" in party and "EvidenceBoundedDefault" not in party)
check("long-lived invitation smoke exists", "AddDays(30)" in party_smoke)
check("leader leave gate smoke exists", "party-leader-leave-native-unknown" in party_smoke)
check("backrank front-balance enforced", 'party-backrank-front-balance-required' in party and 'frontAfter < backAfter' in party)
check("front-rank slot formula not invented", 'MaximumFront' not in party and 'front-slot' not in party)
check("leader rank change is evidence gated", 'party-leader-rank-native-unknown' in party and 'Party leaders must remain in the Frontrank.' not in party_game)
check("SET FOLLOW BLIND/NORMAL wired", 'ConfigureClassicPartyFollowMode' in live and 'Usage: SET FOLLOW BLIND|NORMAL.' in live)
check("BLIND suppresses party-follow room render", 'ShouldRenderClassicPartyFollowRoom' in live and 'ShouldRenderClassicPartyFollowRoom(follower)' in cmd)
check("explicit follower QUIT stops follow", 'HandleExplicitClassicSessionExit' in party_game and 'HandleExplicitClassicSessionExit(session)' in cmd)
check("live session loss does not infer party disconnect policy", 'SynchronizeClassicPartyActiveLocations' in live_gameplay and 'connected: true' in live_gameplay and 'active.ContainsKey(member.ActorId)' not in live_gameplay)
check("generic multiplayer completion is fenced from Classic parity", 'NOT the Classic' in multiplayer and 'AwardPartyExperience' in multiplayer and 'RuntimeMultiplayerCompletionState' not in live_gameplay)
check("party coordination smoke labels offline behavior compatibility-only", 'compatibility-shell-offline-routing-not-classic-parity' in party_smoke)
# Retained formula probe itself explicitly says party XP is unresolved.
def find_formula_probe(node):
    if isinstance(node, dict):
        if node.get("id") == "formula.probe.prog_0103_xp_award_party": return node
        for value in node.values():
            found = find_formula_probe(value)
            if found is not None: return found
    elif isinstance(node, list):
        for value in node:
            found = find_formula_probe(value)
            if found is not None: return found
    return None
xp_probe = find_formula_probe(formula_records)
check("party XP probe remains unresolved", xp_probe is not None and any("remain unresolved" in json.dumps(ev).lower() for ev in xp_probe.get("evidence", [])))

# Paid room-transfer correction.
transport_catalog = load("Librarium/RemainingSystems/transport_service_catalog.json")
registry = load("Librarium/RemainingSystems/remaining_systems_registry.json")
check("transport catalog renamed paid-room-transfers", transport_catalog.get("subsystem") == "paid-room-transfers")
check("transport owner renamed", transport_catalog.get("runtimeOwner") == "RuntimePaidRoomTransferService")
check("transport has no timer/state APIs", all(token not in transport for token in ["DateTimeOffset", "Timer", "Board(", "Depart(", "Advance(", "ArrivalAt", "DepartureAt"]))
check("transport runtime emits immediate move request", "RuntimePaidRoomTransferRequestKind.MoveActor" in transport)
check("transport runtime has no persistent trip API", all(token not in transport for token in ["Board(", "Depart(", "Advance(", "Passenger", "TripState"]))
check("transport smoke checks source room", "source-room-required" in transport_smoke)
check("registry contains paid-room-transfers", any(x.get("id") == "paid-room-transfers" for x in registry.get("systems", [])))
check("registry removed active transport-services id", not any(x.get("id") == "transport-services" for x in registry.get("systems", [])))

# Active remaining-audit source must not re-add stale transport-services as a second UNKNOWN.
remaining = load("Librarium/Reports/COMBINED_DECODE_REMAINING_AUDIT_AFTER_REMAINING_SYSTEMS.json")
deps = remaining.get("deduplicatedRemainingSystemsNativeDependencies", [])
check("remaining audit uses paid-room-transfers", any(x.get("id") == "paid-room-transfers" for x in deps))
check("remaining audit removed transport-services", not any(x.get("id") == "transport-services" for x in deps))

# Parity ledger and score.
ledger = load("Librarium/Parity/majormud_parity_assertions_0289.json")
count = Counter(x.get("state") for x in ledger.get("assertions", []))
check("ledger update 0289", ledger.get("update") == "0289")
check("parity counts 71/69/44", count == Counter({"PASS": 71, "STRONG": 69, "UNKNOWN": 44}), str(dict(count)))
check("scored total 184", len(ledger.get("assertions", [])) == 184)
by_id = {x["id"]: x for x in ledger["assertions"]}
check("communication range promoted STRONG", by_id.get("native.communications.classic-range-rendering", {}).get("state") == "STRONG")
check("paid-room-transfer dependency remains UNKNOWN", by_id.get("native.paid-room-transfers", {}).get("state") == "UNKNOWN")
check("old transport assertion removed", "native.transport-services" not in by_id)
check("party coordination remains UNKNOWN", by_id.get("native.party-coordination", {}).get("state") == "UNKNOWN")
check("native command table remains UNKNOWN", by_id.get("commands.native-player-command-table", {}).get("state") == "UNKNOWN")
check("runtime parity audit points at 0289", "majormud_parity_assertions_0289.json" in audit and "majormud_parity_assertions_0288.json" not in audit)

score = load("Reports/RuntimeVerification/majormud_parity_scorecard_0289.json")
check("scorecard counts match", (score.get("PassCount"),score.get("StrongCount"),score.get("UnknownCount"),score.get("ScoredRequirementCount")) == (71,69,44,184))
check("scorecard percentages match", (score.get("StrictPercent"),score.get("EvidenceBackedPercent")) == (38.6,76.1))
check("remaining shell counts match", (score.get("PartialSystemShellCount"),score.get("RemainingSystemCount")) == (36,36))

# Smoke source was updated to the new evidence gates and no longer expects generic SAY-ignore behavior.
check("communication smoke tests WHISPER gate", "whisper-evidence-gate" in comm_smoke)
check("communication smoke tests TELL gate", "tell-evidence-gate" in comm_smoke)
check("communication smoke no longer expects SAY ignore", "ignore-filter-failed" not in comm_smoke)

# JSON validity for Pass 19 data artifacts is implicit in load() above; include evidence artifact explicitly.
evidence = load("Librarium/Parity/majormud_commands_communication_party_evidence_0289.json")
check("Pass19 evidence update", evidence.get("update") == "0289")
check("Pass19 evidence records global scope", evidence.get("operator_global_rule", {}).get("scope") == ["Classic","Custom","AI"])
check("Pass19 evidence records SHARE message 227", evidence.get("native_help", {}).get("share", {}).get("message_number") == 227)
check("Pass19 evidence records JOIN follows immediately", any("immediately starts following" in item for item in evidence.get("party_runtime_boundary", {}).get("implemented_from_evidence", [])))
check("Pass19 evidence records rank balance", any("front" in item.lower() and "back" in item.lower() for item in evidence.get("party_runtime_boundary", {}).get("implemented_from_evidence", [])))
check("Pass19 evidence records native follow mode message 153", evidence.get("native_help", {}).get("follow_mode", {}).get("message_number") == 153)
check("Pass19 evidence records native BR alias message 123", evidence.get("native_help", {}).get("broadcasting", {}).get("message_number") == 123 and "BR = BROADCAST" in evidence.get("command_table_audit", {}).get("proven_alias_correction", ""))
check("Pass19 evidence excludes generic multiplayer XP shell", "no MajorMUD parity credit" in evidence.get("party_runtime_boundary", {}).get("generic_multiplayer_shell_exclusion", ""))
check("Pass19 evidence keeps party XP unresolved", "remains unresolved" in evidence.get("party_runtime_boundary", {}).get("party_xp_loot_assist_audit", ""))
check("Pass19 evidence records Classic HELP fail-closed boundary", "no longer falls through" in evidence.get("help_audit", {}).get("classic_fail_closed", ""))

report = {
    "schema": "majormud.pass19-static-validation.v1",
    "update": "0289",
    "generatedUtc": datetime.now(timezone.utc).isoformat(),
    "passed": len(failures) == 0,
    "checkCount": len(checks),
    "passedCount": sum(1 for x in checks if x["passed"]),
    "failedCount": len(failures),
    "failures": failures,
    "checks": checks,
    "compilerValidation": "NOT_RUN_DOTNET_SDK_UNAVAILABLE"
}
out = ROOT / "Reports/RuntimeVerification/commands_communication_party_pass19_static_validation_0289.json"
out.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(f"PASS19 static validation: {'PASS' if report['passed'] else 'FAIL'} ({report['passedCount']}/{report['checkCount']})")
for failure in failures:
    print("FAIL:", failure)
raise SystemExit(0 if report["passed"] else 1)
