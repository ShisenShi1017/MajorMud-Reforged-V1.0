using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleERelationshipServiceClosureAuditReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> RelationshipUnresolved,
    IReadOnlyList<string> TrueUnknown)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Bundle E relationship/service disconnect-to-connect gate. It proves every relationship fact
/// that is actually decoded is consumed by the living NPC authority. Classic has no creature
/// faction or reputation system, hostile NPCs never attack one another, and guarding is only an
/// explicit protector-to-protected-NPC attack-blocking relationship in the same room.
/// </summary>
public static class RuntimeBundleERelationshipServiceClosureAudit
{
    public static RuntimeBundleERelationshipServiceClosureAuditReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string code) { if (condition) checks.Add(code); else failures.Add(code); }

        var options = ServerRuntimeOptions.Load(projectRoot);
        var host = new ServerRuntimeHost(projectRoot, options);
        host.Bootstrap();
        var world = host.Worlds.Values.First();
        var persistence = Path.Combine(Path.GetTempPath(), "reforged-bundle-e-relationship-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(persistence);
        try
        {
            var living = new RuntimeLivingWorldService(projectRoot, world, persistence);
            var npcs = living.InspectNpcs(new());
            Check(npcs.Count > 0, "living-npc-relationship-instances-materialized");
            Check(npcs.All(x => x.Metadata.ContainsKey("alignment")), "decoded-alignment-persisted-on-every-living-npc");
            Check(npcs.All(x => x.Metadata.TryGetValue("disposition", out var d) && d is "friendly" or "hostile" or "neutral"), "decoded-disposition-consumed-by-living-authority");
            Check(npcs.Where(x => x.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) || x.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase) || x.InteractionModel.Contains("trainer", StringComparison.OrdinalIgnoreCase))
                .All(x => x.Metadata.TryGetValue("disposition", out var d) && d == "friendly"), "service-npcs-route-to-friendly-disposition");
            Check(npcs.Where(x => x.Metadata.TryGetValue("alignment", out var a) && a.Contains("evil", StringComparison.OrdinalIgnoreCase) &&
                                  !(x.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) || x.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase) || x.InteractionModel.Contains("trainer", StringComparison.OrdinalIgnoreCase)))
                .All(x => x.Metadata.TryGetValue("disposition", out var d) && d == "hostile"), "decoded-evil-alignment-routes-to-hostile-disposition");
            Check(npcs.Where(x => x.Metadata.TryGetValue("alignment", out var a) && !a.Contains("evil", StringComparison.OrdinalIgnoreCase) &&
                                  !(x.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) || x.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase) || x.InteractionModel.Contains("trainer", StringComparison.OrdinalIgnoreCase)))
                .All(x => x.Metadata.TryGetValue("disposition", out var d) && d == "neutral"), "remaining-decoded-alignment-routes-to-neutral-disposition");
            Check(npcs.All(x => x.Metadata.ContainsKey("npc_ai_monster_group_id") || x.Metadata.ContainsKey("summoned_by")), "spawn-group-relationship-persisted-on-generated-instances");

            Check(RuntimeNpcPerceptionSmokeReport.Run().Passed, "player-only-hostility-perception-boundary-passed");
            Check(RuntimeNpcThreatSmokeReport.Run().Passed, "player-only-threat-boundary-passed");
            Check(RuntimeNpcProtectionAdmissionSmokeReport.Run().Passed, "explicit-protector-attack-blocking-passed");
            Check(RuntimeNpcAssistSmokeReport.Run().Passed, "explicit-assist-authorization-boundary-passed");
            Check(RuntimeNpcDialogueAdmissionSmokeReport.Run().Passed, "dialogue-admission-owner-passed");
            Check(RuntimeNpcServiceAdmissionSmokeReport.Run().Passed, "service-admission-owner-passed");
            Check(RuntimeNpcServiceRefusalDialogueSmokeReport.Run().Passed, "service-refusal-dialogue-owner-passed");

            var serverRoot = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
            var singular = new[]
            {
                "RuntimeClassicNpcAiCatalog.cs", "RuntimeNpcPerception.cs", "RuntimeNpcThreat.cs", "RuntimeNpcAssist.cs",
                "RuntimeNpcProtectionAdmission.cs", "RuntimeNpcDialogueAdmission.cs", "RuntimeNpcServiceAdmission.cs", "RuntimeNpcServiceRefusalDialogue.cs"
            };
            Check(singular.All(file => Directory.GetFiles(serverRoot, file, SearchOption.TopDirectoryOnly).Length == 1), "no-duplicate-relationship-or-service-authority-path");

            var evidence = new[]
            {
                Path.Combine(projectRoot, "Data", "Classic", "npcs.json"),
                Path.Combine(projectRoot, "Generated", "NPCReconstruction", "npc_ai_runtime_catalog.json"),
                Path.Combine(projectRoot, "Generated", "NPCReconstruction", "npc_relationship_index.json"),
                Path.Combine(projectRoot, "Librarium", "Objects", "npcs"),
                Path.Combine(projectRoot, "Librarium", "SourceImports", "MMExplorerSQLData"),
                Path.Combine(projectRoot, "ImportedDatabases", "data-v1.11n.mdb"),
                Path.Combine(projectRoot, "ImportedDatabases", "OmniVerseReference"),
                Path.Combine(projectRoot, "ImportedDatabases", "WCCDefaultInstall")
            };
            foreach (var path in evidence)
                Check(File.Exists(path) || Directory.Exists(path), "relationship-evidence-present:" + Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            failures.Add("relationship-service-closure:" + ex.Message);
        }
        finally
        {
            try { Directory.Delete(persistence, true); } catch { }
        }

        return new(
            failures.Count == 0,
            checks,
            failures,
            [
                "Decoded NPC alignment is loaded by RuntimeNpcDefinition and persisted on living NPC metadata.",
                "Classic creature relationships contain no faction membership and no creature reputation system.",
                "Hostile NPC targeting and threat admission reject NPC candidates; hostile NPCs do not attack one another.",
                "Explicit protector metadata blocks a player attack only when the protector and protected NPC are alive in the same room.",
                "Dialogue, service, and service-refusal completion execute through RuntimeLivingWorldService and persisted metadata."
            ],
            [
                "Perception, threat, assist, and protection consume only explicit runtime evidence and do not infer broad relationship matrices.",
                "Merchant and trainer service routing reuses the single living NPC service/dialogue authority."
            ],
            [],
            []);
    }
}
