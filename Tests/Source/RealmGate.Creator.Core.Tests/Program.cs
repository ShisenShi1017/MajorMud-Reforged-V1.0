using System.Security.Cryptography;
using RealmGate.App.Services;

var projectRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var editor = new CustomWorldEditorService(projectRoot);
var watched = new[] { editor.ConfigPath, editor.OverridePath };
var before = watched.ToDictionary(path => path, HashOrMissing, StringComparer.OrdinalIgnoreCase);

var families = editor.GetEditableFamilies();
Require(families.Any(family => family.Value.Equals("rooms", StringComparison.OrdinalIgnoreCase)),
    "rooms-category-available");
Require(families.Any(family => family.Value.Equals("items", StringComparison.OrdinalIgnoreCase)),
    "items-category-available");
var rooms = editor.BrowseObjects("rooms", null, 10);
Require(rooms.Count > 0, "room-records-browsable");
var roomJson = editor.ReadBaseRecordJson("rooms", rooms[0].Id);
Require(roomJson.Contains(rooms[0].Id, StringComparison.OrdinalIgnoreCase),
    "room-record-readable");
var globalSearch = editor.GlobalSearch(rooms[0].Id, 50);
Require(globalSearch.Any(hit =>
        hit.Family.Equals("rooms", StringComparison.OrdinalIgnoreCase) &&
        hit.Id.Equals(rooms[0].Id, StringComparison.OrdinalIgnoreCase)),
    "global-effective-world-search");
var keyDoors = editor.BrowseKeyDoors(null, 1000);
Require(keyDoors.Count > 0 && keyDoors.All(row =>
        !string.IsNullOrWhiteSpace(row.ExitId) &&
        !string.IsNullOrWhiteSpace(row.FromRoomId)),
    "keyed-door-index-readable");
var monsterIndex = editor.BrowseMonsterIndex(null, limit: 5000);
Require(monsterIndex.Count > 0 && monsterIndex.All(row =>
        !string.IsNullOrWhiteSpace(row.NpcId) && row.Level >= 0),
    "monster-spawn-index-readable");
var controlRooms = editor.BrowseControlRooms(null, 5000);
Require(controlRooms.Count > 0 && controlRooms.All(row =>
        !string.IsNullOrWhiteSpace(row.RoomId) &&
        row.InteractionCount + row.TriggerCount + row.ScriptCount > 0),
    "control-room-index-readable");
var controlRoomReferences = editor.FindReferences("rooms", controlRooms[0].RoomId, 1000);
Require(controlRoomReferences.Count > 0,
    "effective-world-reference-search");
var parityAudit = editor.BuildNightmareParityAudit();
Require(parityAudit.Count > 0 && parityAudit.All(row =>
        !string.IsNullOrWhiteSpace(row.NightmareFeature) &&
        !string.IsNullOrWhiteSpace(row.Status) &&
        !string.IsNullOrWhiteSpace(row.ReforgedEquivalent)),
    "creator-capability-parity-audit-readable");
var npc = editor.BrowseObjects("npcs", "abomination", 10).FirstOrDefault();
Require(npc is not null, "npc-record-browsable");
var npcAttacks = editor.LoadNpcAttacks(npc!.Id);
var npcAbilities = editor.LoadNpcAbilities(npc.Id);
Require(npcAttacks.Count > 0 && npcAttacks.All(attack => attack.Slot > 0),
    "npc-attacks-readable");
Require(npcAbilities.Count > 0 && npcAbilities.All(ability => ability.AbilityId != 0),
    "npc-abilities-readable");
var quest = editor.BrowseObjects("quests", "Textblock quest flow", 20).FirstOrDefault();
Require(quest is not null, "executable-quest-browsable");
var questStages = editor.LoadQuestStages(quest!.Id);
Require(questStages.Count > 0 && questStages.Any(stage => !string.IsNullOrWhiteSpace(stage.ScriptId)),
    "quest-stage-flow-readable");
var questScript = questStages.First(stage => !string.IsNullOrWhiteSpace(stage.ScriptId));
var scriptOperations = editor.LoadScriptOperations(questScript.ScriptId);
Require(scriptOperations.Count > 0 && scriptOperations.All(operation => operation.Line > 0 && operation.Sequence > 0),
    "quest-script-operations-readable");

var administration = new ClassicAdministrationEditorService(projectRoot);
var administrationSnapshot = administration.Load();
var administrationPolicyBefore = HashOrMissing(administrationSnapshot.PolicyPath);
var managedCharacters = administration.LoadAllCharacters();
Require(administrationSnapshot.Policy.WorldId.Equals("classic", StringComparison.OrdinalIgnoreCase) &&
        administrationSnapshot.Characters.All(character =>
            !string.IsNullOrWhiteSpace(character.CharacterId) &&
            !string.IsNullOrWhiteSpace(character.DisplayName)) &&
        managedCharacters.All(character =>
            !string.IsNullOrWhiteSpace(character.CharacterId) &&
            !string.IsNullOrWhiteSpace(character.DisplayName) &&
            !string.IsNullOrWhiteSpace(character.WorldId)),
    "administration-policy-and-characters-readable");
Require(HashOrMissing(administrationSnapshot.PolicyPath).Equals(administrationPolicyBefore, StringComparison.Ordinal),
    "administration-browse-does-not-mutate-policy");

var librarium = new ClassicLibrariumBrowserService(projectRoot);
var librariumCategories = librarium.GetCategories();
Require(new[] { "weapons", "armor", "items", "monsters", "rooms", "classes", "races", "spells", "abilities", "quests" }
        .All(key => librariumCategories.Any(category => category.Key.Equals(key, StringComparison.OrdinalIgnoreCase))),
    "classic-librarium-game-facing-categories");
var librariumRooms = librarium.Browse("rooms", null, 10);
Require(librariumRooms.Count == 10 && librariumRooms.All(room =>
        !string.IsNullOrWhiteSpace(room.Id) && !string.IsNullOrWhiteSpace(room.Name)),
    "classic-librarium-records-browsable");
var librariumWeapons = librarium.Browse("weapons", null, 10);
Require(librariumWeapons.Count == 10 && librariumWeapons.All(weapon =>
        !string.IsNullOrWhiteSpace(weapon.Id) && !string.IsNullOrWhiteSpace(weapon.Name)),
    "classic-librarium-string-backed-weapon-fields-readable");
var librariumQuests = librarium.Browse("quests", null, 1000);
Require(librariumQuests.Count > 0 &&
        librariumQuests.All(quest => quest.Id.StartsWith("quest.summary.", StringComparison.OrdinalIgnoreCase)) &&
        librariumQuests.Select(quest => quest.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == librariumQuests.Count,
    "classic-librarium-quest-flows-grouped");
var librariumQuestDetails = librarium.Read("quests", librariumQuests[0].Id);
Require(librariumQuestDetails.Fields.Any(field => field.Name.Equals("Quest Flag", StringComparison.OrdinalIgnoreCase)) &&
        librariumQuestDetails.Fields.Any(field => field.Name.Contains("NPC", StringComparison.OrdinalIgnoreCase)) &&
        librariumQuestDetails.Fields.Any(field => field.Name.StartsWith("Step ", StringComparison.OrdinalIgnoreCase)),
    "classic-librarium-quest-steps-readable");
var librariumRoomDetails = librarium.Read("rooms", librariumRooms[0].Id);
var leakedLibrariumMetadata = librariumRoomDetails.Fields.Where(field =>
        field.Name.Contains("Evidence", StringComparison.OrdinalIgnoreCase) ||
        field.Name.Equals("Source", StringComparison.OrdinalIgnoreCase) ||
        field.Name.Equals("Source file", StringComparison.OrdinalIgnoreCase) ||
        field.Name.Equals("Source files", StringComparison.OrdinalIgnoreCase) ||
        field.Name.Contains("Provenance", StringComparison.OrdinalIgnoreCase) ||
        field.Name.Contains("Public reference", StringComparison.OrdinalIgnoreCase))
    .Select(field => field.Name)
    .ToArray();
Require(librariumRoomDetails.Fields.Count > 0 && leakedLibrariumMetadata.Length == 0,
    "classic-librarium-details-hide-evidence-metadata" +
    (leakedLibrariumMetadata.Length == 0 ? string.Empty : ": " + string.Join(", ", leakedLibrariumMetadata)));

var evidenceRoot = Path.Combine(Path.GetTempPath(), "realmgate-creator-evidence-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(evidenceRoot);
try
{
    var evidencePath = Path.Combine(evidenceRoot, "WCCMP001.DAT");
    File.WriteAllBytes(evidencePath, [0x52, 0x47]);
    var evidenceHash = HashOrMissing(evidencePath);
    var bridge = new NightmareMdbBridgeService(editor);
    var evidence = bridge.AnalyzeWccEvidenceFolder(evidenceRoot);
    Require(evidence.FileCount == 1 && evidence.RecognizedFiles == 1 &&
            HashOrMissing(evidencePath).Equals(evidenceHash, StringComparison.Ordinal),
        "native-evidence-inventory-is-read-only");
}
finally
{
    Directory.Delete(evidenceRoot, recursive: true);
}

var publishRoot = Path.Combine(Path.GetTempPath(), "realmgate-creator-publish-" + Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(Path.Combine(publishRoot, "Librarium"));
    Directory.CreateDirectory(Path.Combine(publishRoot, "Config"));
    File.WriteAllText(Path.Combine(publishRoot, "Config", "server.worlds.json"), "{\"worlds\":[]}");
    var publishEditor = new CustomWorldEditorService(publishRoot);
    Directory.CreateDirectory(publishEditor.TestRealmDataPath);
    foreach (var file in new[] { "rooms.json", "exits.json", "npcs.json", "spawns.json", "compile_manifest.json", "navigation_graph.json" })
        File.WriteAllText(Path.Combine(publishEditor.TestRealmDataPath, file), $"{{\"file\":\"{file}\"}}");
    File.WriteAllText(Path.Combine(publishEditor.TestRealmDataPath, "world_manifest.json"), "{\"world_id\":\"Custom1\"}");
    Directory.CreateDirectory(publishEditor.LiveDataPath);
    File.WriteAllText(Path.Combine(publishEditor.LiveDataPath, "legacy.json"), "{\"live\":true}");

    var publishPreview = publishEditor.PreviewTestRealmPublish();
    Require(publishPreview.AddedFiles == 7 && publishPreview.RemovedFiles == 1,
        "test-realm-publish-diff-preview");
    var publishResult = publishEditor.PublishTestRealm(publishPreview.TestRealmFingerprint, acknowledged: true);
    Require(File.Exists(Path.Combine(publishEditor.LiveDataPath, "rooms.json")) &&
            !File.Exists(Path.Combine(publishEditor.LiveDataPath, "legacy.json")),
        "test-realm-atomic-publish");
    Require(publishResult.RollbackPath is not null && File.Exists(Path.Combine(publishResult.RollbackPath, "legacy.json")),
        "test-realm-rollback-snapshot");
    publishEditor.RollbackLatestPublish(acknowledged: true);
    Require(File.Exists(Path.Combine(publishEditor.LiveDataPath, "legacy.json")),
        "test-realm-rollback-restore");
}
finally
{
    if (Directory.Exists(publishRoot)) Directory.Delete(publishRoot, recursive: true);
}

var after = watched.ToDictionary(path => path, HashOrMissing, StringComparer.OrdinalIgnoreCase);
Require(before.All(pair => after[pair.Key].Equals(pair.Value, StringComparison.Ordinal)),
    "read-only-browse-does-not-mutate-world-files");

Console.WriteLine("RealmGate Creator Core Regression");
Console.WriteLine("PASS categories");
Console.WriteLine("PASS record browsing");
Console.WriteLine("PASS global search, references, keyed doors, monster index, control rooms, and capability audit");
Console.WriteLine("PASS NPC attack and ability collections");
Console.WriteLine("PASS quest stages and runtime script operations");
Console.WriteLine("PASS read-only Classic policy and realm character administration");
Console.WriteLine("PASS read-only Classic Librarium categories, records, and curated details");
Console.WriteLine("PASS Test Realm publish preview, snapshot, swap, and rollback");
Console.WriteLine("PASS read-only integrity");
Console.WriteLine("PASS native evidence inventory");
Console.WriteLine("Passed 30 checks.");
return;

static string HashOrMissing(string path)
{
    if (!File.Exists(path)) return "missing";
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static void Require(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"FAIL {name}");
}
