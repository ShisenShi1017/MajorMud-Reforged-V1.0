using System.Security.Cryptography;
using System.Text;

namespace RealmGate.Server;

public enum RuntimeCompatibilityFormat { Wcc, Wbb, WorldData, CustomData, CharacterSave, ReforgedPackage }
public enum RuntimeCompatibilityOperation { Inspect, Import, Export, Validate, Migrate }
public enum RuntimeCompatibilityMode { Classic, Custom, Ai }
public sealed record RuntimeCompatibilityRequest(
    string OperationId,
    RuntimeCompatibilityOperation Operation,
    RuntimeCompatibilityFormat Format,
    RuntimeCompatibilityMode Mode,
    int SourceVersion,
    int TargetVersion,
    string Payload,
    bool HostAuthorized,
    bool PreserveClassicIds,
    bool DryRun,
    string? NativeFileName = null,
    string? ExpectedNativeFileName = null);
public sealed record RuntimeCompatibilityIssue(string Code, string Detail, bool Fatal);
public sealed record RuntimeCompatibilityResult(bool Success, string Code, string? CanonicalPayload, string? PayloadSha256, int OutputVersion, bool RequiresOwnerCommit, IReadOnlyList<RuntimeCompatibilityIssue> Issues);

/// <summary>
/// Reforged compatibility coordinator.
///
/// Classic MajorMUD does not expose an in-game import/export/migration subsystem. Native data is
/// edited externally, the exact expected file/version is copied into the MajorMUD installation,
/// and the server is started/restarted. A wrong file/version is a hard native load boundary rather
/// than a migration opportunity. Reforged keeps its own internal data/config formats; this service
/// therefore permits Classic inspection/validation only and never pretends to convert native data.
/// Custom/AI compatibility operations remain Reforged extension behavior.
/// </summary>
public sealed class RuntimeImportExportCompatibilityService
{
    public RuntimeCompatibilityResult Execute(RuntimeCompatibilityRequest request)
    {
        var issues = new List<RuntimeCompatibilityIssue>();
        if (string.IsNullOrWhiteSpace(request.OperationId)) return Fail("operation-id-required", issues);
        if (request.SourceVersion < 0 || request.TargetVersion < 0) return Fail("version-invalid", issues);

        if (request.Mode == RuntimeCompatibilityMode.Classic)
            return ExecuteClassic(request, issues);

        if ((request.Operation is RuntimeCompatibilityOperation.Import or RuntimeCompatibilityOperation.Export or RuntimeCompatibilityOperation.Migrate) && !request.HostAuthorized)
            return Fail("host-authorization-required", issues);
        if (request.Operation == RuntimeCompatibilityOperation.Migrate && request.TargetVersion <= request.SourceVersion)
            return Fail("migration-target-must-advance", issues);
        if (request.Payload.Length > 16_777_216) return Fail("payload-too-large", issues);
        if (request.Payload.IndexOf('\0') >= 0) return Fail("payload-binary-content-rejected", issues);

        var canonical = Canonicalize(request.Payload);
        if (string.IsNullOrWhiteSpace(canonical) && request.Operation != RuntimeCompatibilityOperation.Inspect)
            return Fail("payload-empty", issues);

        if (request.Format is RuntimeCompatibilityFormat.Wcc or RuntimeCompatibilityFormat.Wbb)
            issues.Add(new("native-decoder-owner-required", "WCC/WBB binary decoding must be performed by the dedicated decoder before commit.", false));
        if (request.Operation == RuntimeCompatibilityOperation.Migrate)
            issues.Add(new("owner-migration-required", "Owning subsystem must validate and commit migrated Reforged records.", false));

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new(true, request.DryRun ? "compatibility-dry-run-valid" : "compatibility-request-valid", canonical, hash,
            request.Operation == RuntimeCompatibilityOperation.Migrate ? request.TargetVersion : request.SourceVersion,
            request.Operation is RuntimeCompatibilityOperation.Import or RuntimeCompatibilityOperation.Migrate,
            issues);
    }

    private static RuntimeCompatibilityResult ExecuteClassic(RuntimeCompatibilityRequest request, List<RuntimeCompatibilityIssue> issues)
    {
        if (request.Format == RuntimeCompatibilityFormat.ReforgedPackage)
            return Fail("classic-extension-format-rejected", issues);
        if (!request.PreserveClassicIds)
            return Fail("classic-id-preservation-required", issues);

        if (request.Operation is RuntimeCompatibilityOperation.Import or RuntimeCompatibilityOperation.Export or RuntimeCompatibilityOperation.Migrate)
            return Fail("classic-native-file-replacement-is-external", issues);

        // Operator-confirmed Classic contract: the native file and version must match exactly.
        // There is no supported cross-version migration path.
        if (request.SourceVersion != request.TargetVersion)
            return Fail("classic-native-version-mismatch-hard-failure", issues);

        if (request.Format is RuntimeCompatibilityFormat.Wcc or RuntimeCompatibilityFormat.Wbb)
        {
            if (string.IsNullOrWhiteSpace(request.NativeFileName) || string.IsNullOrWhiteSpace(request.ExpectedNativeFileName))
                return Fail("classic-native-file-identity-required", issues);
            if (!request.NativeFileName.Equals(request.ExpectedNativeFileName, StringComparison.OrdinalIgnoreCase))
                return Fail("classic-native-file-mismatch-hard-failure", issues);
            issues.Add(new("classic-native-file-exact-match", "Classic native validation requires the exact expected file identity and exact matching version; conversion is not attempted.", false));
        }

        if (request.Payload.Length > 16_777_216) return Fail("payload-too-large", issues);
        if (request.Payload.IndexOf('\0') >= 0) return Fail("payload-binary-content-rejected", issues);
        var canonical = Canonicalize(request.Payload);
        if (string.IsNullOrWhiteSpace(canonical) && request.Operation == RuntimeCompatibilityOperation.Validate)
            return Fail("payload-empty", issues);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new(true, request.DryRun ? "classic-exact-compatibility-dry-run-valid" : "classic-exact-compatibility-valid",
            canonical, hash, request.SourceVersion, false, issues);
    }

    private static string Canonicalize(string payload) => string.Join("\n", payload.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').Select(line => line.TrimEnd())).Trim();
    private static RuntimeCompatibilityResult Fail(string code, IReadOnlyList<RuntimeCompatibilityIssue> issues) => new(false, code, null, null, 0, false, issues);
}
