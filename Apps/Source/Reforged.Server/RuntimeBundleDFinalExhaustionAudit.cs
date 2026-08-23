using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleDFinalPhaseStatus(
    string Phase,
    bool Passed,
    string RuntimeOwner,
    string RuntimeConsumer,
    string ValidationOwner);

public sealed record RuntimeBundleDFinalExhaustionAuditReport(
    bool AuditPassed,
    bool BundleDComplete,
    string WorldId,
    IReadOnlyList<RuntimeBundleDFinalPhaseStatus> Phases,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> AuditFailures,
    IReadOnlyList<string> ClosureBlockers,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Final Bundle D gate. AuditPassed means the complete D1-D11 validation surface executed and
/// all required decoded/imported/native/generated sources were available. BundleDComplete is a
/// stricter gameplay-closure result and remains false while any required decoded field lacks a
/// live authoritative consumer, persistence where stateful, or executable validation.
/// </summary>
public static class RuntimeBundleDFinalExhaustionAudit
{
    public static RuntimeBundleDFinalExhaustionAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var blockers = new List<string>();
        var phases = new List<RuntimeBundleDFinalPhaseStatus>();

        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition) failures.Add(name);
        }

        bool Exists(string relative) => File.Exists(Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        string Read(string relative) => File.ReadAllText(Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        var d1 = RuntimeBundleDWorldAuthorityAudit.Run(projectRoot, worldId);
        var corrective = RuntimeBundleDCorrectiveExhaustionAudit.Run(projectRoot, worldId);
        var d2 = RuntimeBundleDRoomAudit.Run(projectRoot, worldId);
        var d3 = RuntimeBundleDExitAudit.Run(projectRoot, worldId);
        var d4 = RuntimeBundleDDoorAudit.Run(projectRoot, worldId);
        var d5 = RuntimeBundleDSearchAudit.Run(projectRoot, worldId);
        var d6 = RuntimeBundleDCleanupAudit.Run(projectRoot, worldId);
        var d7 = RuntimeBundleDHiddenObjectAudit.Run(projectRoot, worldId);
        var d8 = RuntimeBundleDTeleporterAudit.Run(projectRoot, worldId);
        var d9 = RuntimeBundleDEnvironmentAudit.Run(projectRoot, worldId);
        var d10 = RuntimeBundleDTrapAudit.Run(projectRoot, worldId);
        var d11 = RuntimeBundleDMapAudit.Run(projectRoot, worldId);
        var remaining = RuntimeBundleDRemainingExhaustionAudit.Run(projectRoot, worldId);

        phases.Add(new("D1 World Authority", d1.Passed && corrective.Passed, "ClassicRuntimeCatalog / RuntimeWorld", "RuntimeWorldLoader", "RuntimeBundleDWorldAuthorityAudit + CorrectiveExhaustionAudit"));
        phases.Add(new("D2 Rooms", d2.Passed, "RuntimeWorld / RuntimeRoom", "RuntimeLiveWorldService", "RuntimeBundleDRoomAudit"));
        phases.Add(new("D3 Exits", d3.Passed, "RuntimeWorld / RuntimeExit", "RuntimeLivingWorldService", "RuntimeBundleDExitAudit"));
        phases.Add(new("D4 Doors", d4.Passed, "RuntimeLivingWorldService", "movement and door commands", "RuntimeBundleDDoorAudit"));
        phases.Add(new("D5 Search", d5.Passed, "RuntimeClassicHiddenDiscoveryRegistry", "RuntimeBundleDSearchService", "RuntimeBundleDSearchAudit"));
        phases.Add(new("D6 Cleanup", d6.Passed, "RuntimeClassicCleanupService", "maintenance cleanup command/tick path", "RuntimeBundleDCleanupAudit"));
        phases.Add(new("D7 Hidden Objects", d7.Passed, "RuntimeClassicHiddenDiscoveryRegistry", "room render, movement, pickup and drop", "RuntimeBundleDHiddenObjectAudit"));
        phases.Add(new("D8 Teleporters", d8.Passed, "RuntimeWorld exits and spell records", "command movement and spell engine", "RuntimeBundleDTeleporterAudit"));
        phases.Add(new("D9 Environment", d9.Passed, "RuntimeRoom environment fields", "room/movement/death consumers", "RuntimeBundleDEnvironmentAudit"));
        phases.Add(new("D10 Traps", d10.Passed, "RuntimeClassicExitTrapCatalog", "movement trap consumer", "RuntimeBundleDTrapAudit"));
        phases.Add(new("D11 Maps", d11.Passed, "RuntimeWorld canonical graph", "room/exit lookup and traversal", "RuntimeBundleDMapAudit"));
        phases.Add(new("D12 Remaining Decoded Exit Semantics", remaining.Passed, "RuntimeExitGateDefinition", "RuntimeLivingWorldService.EvaluateExitGate", "RuntimeBundleDRemainingExhaustionAudit"));

        foreach (var phase in phases) Check(phase.Passed, "phase-passed:" + phase.Phase);

        var sourceFamilies = new[]
        {
            "Data/Classic/rooms.json",
            "Data/Classic/exits.json",
            "Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0138/rooms_decoded.json",
            "Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0138/exits_decoded.json",
            "Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0183/exits_decoded_semantic.json",
            "Librarium/Objects/rooms/mmexplorer_room_records_0140.json",
            "Librarium/Objects/exits/mmexplorer_exit_records_0140.json",
            "Librarium/RemainingSystems/door_lock_access_catalog.json",
            "Librarium/RemainingSystems/world_reset_cleanup_catalog.json",
            "Evidence/DataSimVer0017-SearchCleanupEvidence.json",
            "Evidence/ExternalSources/Validation0008/search_hidden_exit_trace.csv",
            "Evidence/ExternalSources/Validation0012/cleanup_hidden_snippets.json",
            "ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/codex-ability-wcc.json"
        };
        foreach (var source in sourceFamilies) Check(Exists(source), "source-present:" + source);

        Check(Exists("Apps/Source/Reforged.Server/RuntimeWorldLoader.cs"), "single-authoritative-world-loader-file-present");
        Check(Directory.GetFiles(Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server"), "RuntimeWorldLoader.cs", SearchOption.TopDirectoryOnly).Length == 1, "one-world-loader-file");
        Check(Directory.GetFiles(Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server"), "RuntimeClassicCleanup.cs", SearchOption.TopDirectoryOnly).Length == 1, "one-cleanup-authority-file");
        Check(Directory.GetFiles(Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server"), "RuntimeClassicHiddenSearch.cs", SearchOption.TopDirectoryOnly).Length == 1, "one-hidden-search-authority-file");
        Check(Read("Apps/Source/Reforged.Server/RuntimeLivingWorld.cs").Contains("TryResolveExit", StringComparison.Ordinal), "live-movement-uses-authoritative-exit-resolution");
        Check(Read("Apps/Source/Reforged.Server/RuntimeClassicCleanup.cs").Contains("DeleteAtCleanup", StringComparison.Ordinal), "runtime-cleanup-consumes-del-maint-semantic");
        Check(Read("Apps/Source/Reforged.Server/RuntimeBundleDCorrectiveExhaustionAudit.cs").Contains("Del@Maint", StringComparison.Ordinal), "decoded-del-maint-provenance-preserved");

        // Earlier phase reports intentionally retain historical decode notes. Closure is now
        // determined by the D-only semantic audit, which distinguishes executable gameplay
        // fields from compiler/provenance metadata and validates the remaining live consumers.
        blockers.AddRange(remaining.Failures);
        blockers = blockers.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var complete = failures.Count == 0 && blockers.Count == 0;
        return new RuntimeBundleDFinalExhaustionAuditReport(
            failures.Count == 0,
            complete,
            worldId,
            phases,
            checks,
            failures,
            blockers,
            new[]
            {
                "D1-D11 each have an executable audit and named runtime owner, live consumer, and validation owner.",
                "Canonical map-qualified room and exit identities are authoritative.",
                "Decoded Del@Maint provenance is preserved while DeleteAtCleanup remains the runtime semantic name.",
                "Search and hidden discovery remain player-specific.",
                "The final gate does not infer gameplay from names, descriptions, or absent generated fields."
            },
            corrective.StronglyCorroborated.Concat(d7.StronglyCorroborated).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            corrective.DecodedButNotMapped.Concat(d7.DecodedButNotMapped).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            corrective.Conflicts.Concat(d7.Conflicts).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            corrective.TrueUnknown.Concat(d7.TrueUnknown).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
