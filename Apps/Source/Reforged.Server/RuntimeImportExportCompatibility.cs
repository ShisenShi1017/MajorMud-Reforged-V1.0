using System.Security.Cryptography;
using System.Text;

namespace Reforged.Server;

public enum RuntimeCompatibilityFormat { Wcc, Wbb, WorldData, CustomData, CharacterSave, ReforgedPackage }
public enum RuntimeCompatibilityOperation { Inspect, Import, Export, Validate, Migrate }
public enum RuntimeCompatibilityMode { Classic, Custom, Ai }
public sealed record RuntimeCompatibilityRequest(string OperationId, RuntimeCompatibilityOperation Operation, RuntimeCompatibilityFormat Format, RuntimeCompatibilityMode Mode, int SourceVersion, int TargetVersion, string Payload, bool HostAuthorized, bool PreserveClassicIds, bool DryRun);
public sealed record RuntimeCompatibilityIssue(string Code, string Detail, bool Fatal);
public sealed record RuntimeCompatibilityResult(bool Success, string Code, string? CanonicalPayload, string? PayloadSha256, int OutputVersion, bool RequiresOwnerCommit, IReadOnlyList<RuntimeCompatibilityIssue> Issues);

public sealed class RuntimeImportExportCompatibilityService
{
    public RuntimeCompatibilityResult Execute(RuntimeCompatibilityRequest request)
    {
        var issues = new List<RuntimeCompatibilityIssue>();
        if (string.IsNullOrWhiteSpace(request.OperationId)) return Fail("operation-id-required", issues);
        if (request.SourceVersion < 0 || request.TargetVersion < 0) return Fail("version-invalid", issues);
        if (request.Operation is RuntimeCompatibilityOperation.Import or RuntimeCompatibilityOperation.Export or RuntimeCompatibilityOperation.Migrate && !request.HostAuthorized)
            return Fail("host-authorization-required", issues);
        if (request.Mode == RuntimeCompatibilityMode.Classic && request.Format == RuntimeCompatibilityFormat.ReforgedPackage)
            return Fail("classic-extension-format-rejected", issues);
        if (request.Mode == RuntimeCompatibilityMode.Classic && !request.PreserveClassicIds)
            return Fail("classic-id-preservation-required", issues);
        if (request.Operation == RuntimeCompatibilityOperation.Migrate && request.TargetVersion <= request.SourceVersion)
            return Fail("migration-target-must-advance", issues);
        if (request.Payload.Length > 16_777_216) return Fail("payload-too-large", issues);
        if (request.Payload.IndexOf('\0') >= 0) return Fail("payload-binary-content-rejected", issues);

        var canonical = Canonicalize(request.Payload);
        if (string.IsNullOrWhiteSpace(canonical) && request.Operation != RuntimeCompatibilityOperation.Inspect)
            return Fail("payload-empty", issues);

        if (request.Format is RuntimeCompatibilityFormat.Wcc or RuntimeCompatibilityFormat.Wbb)
        {
            issues.Add(new("native-decoder-owner-required", "WCC/WBB binary decoding must be performed by the dedicated decoder before commit.", false));
        }
        if (request.Operation == RuntimeCompatibilityOperation.Migrate)
        {
            issues.Add(new("owner-migration-required", "Owning subsystem must validate and commit migrated records.", false));
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new(true, request.DryRun ? "compatibility-dry-run-valid" : "compatibility-request-valid", canonical, hash,
            request.Operation == RuntimeCompatibilityOperation.Migrate ? request.TargetVersion : request.SourceVersion,
            request.Operation is RuntimeCompatibilityOperation.Import or RuntimeCompatibilityOperation.Migrate,
            issues);
    }

    private static string Canonicalize(string payload) => string.Join("\n", payload.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').Select(line => line.TrimEnd())).Trim();
    private static RuntimeCompatibilityResult Fail(string code, IReadOnlyList<RuntimeCompatibilityIssue> issues) => new(false, code, null, null, 0, false, issues);
}
