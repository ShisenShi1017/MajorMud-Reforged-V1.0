using System.Collections.Generic;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicDataReconstructionSmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicDataReconstructionSmokeReport Run(string projectRoot)
    {
        var decode = ClassicDecodeExhaustionAudit.Run(projectRoot);
        var explicitEmpty = decode.Fields
            .Where(field => field.Status == "connected-empty")
            .OrderBy(field => field.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(field => field.JsonPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var nativeMessages = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot);
        var checks = new List<string>
        {
            "decode-exhaustion-passed:" + decode.Passed,
            "decode-families-49:" + (decode.FamilyCount == 49),
            "decode-families-all-connected:" + (decode.ConnectedFamilyCount == decode.FamilyCount),
            "decode-families-zero-disconnected:" + (decode.DisconnectedFamilyCount == 0),
            "decode-families-zero-pending:" + (decode.PendingFamilyCount == 0),
            "decode-fields-2341:" + (decode.TotalFields == 2341),
            "decode-fields-all-connected:" + (decode.ConnectedFieldCount == decode.TotalFields),
            "decode-fields-zero-disconnected:" + (decode.DisconnectedFieldCount == 0),
            "decode-explicit-empty-count-2:" + (explicitEmpty.Length == 2),
            "decode-explicit-empty-equipment-alignment:" + explicitEmpty.Any(field =>
                field.Family.Equals("equipment", StringComparison.OrdinalIgnoreCase)
                && field.JsonPath.Equals("$.records[*].alignment", StringComparison.OrdinalIgnoreCase)
                && field.NonNullCount == 0
                && field.EvidenceReferenceCount > 0
                && field.RuntimeConsumerCount > 0),
            "decode-explicit-empty-equipment-gender:" + explicitEmpty.Any(field =>
                field.Family.Equals("equipment", StringComparison.OrdinalIgnoreCase)
                && field.JsonPath.Equals("$.records[*].gender", StringComparison.OrdinalIgnoreCase)
                && field.NonNullCount == 0
                && field.EvidenceReferenceCount > 0
                && field.RuntimeConsumerCount > 0),
            "native-message-catalog-3869:" + (nativeMessages.NativeMessageCount == 3869),
            "native-message-reserved-1-empty:" +
                (nativeMessages.TryResolveMessage(1, out var message1) && message1.Length == 0),
            "native-message-reserved-66-empty:" +
                (nativeMessages.TryResolveMessage(66, out var message66) && message66.Length == 0)
        };

        var passed = checks.All(check => check.EndsWith(":True", StringComparison.OrdinalIgnoreCase));
        return new RuntimeClassicDataReconstructionSmokeReport(passed, checks);
    }
}
