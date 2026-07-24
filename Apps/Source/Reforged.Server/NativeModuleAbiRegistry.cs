namespace Reforged.Server;

public enum NativeDistributionIdentity { Wcc, Wbb }
public sealed record NativeModuleAbiIdentity(string Module, NativeDistributionIdentity Distribution, string Sha256, long Size, IReadOnlyList<string> Imports, int ExportCount, bool SharedAbi, string EvidenceId);

public static class NativeModuleAbiRegistry
{
    private static readonly IReadOnlyDictionary<string, NativeModuleAbiIdentity> Entries =
        new Dictionary<string, NativeModuleAbiIdentity>(StringComparer.OrdinalIgnoreCase)
        {
            ["mmud:wcc"] = new("mmud", NativeDistributionIdentity.Wcc, "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077", 643072, new[]{"cw3220mt.DLL","KERNEL32.dll","WGSERVER.EXE","GALGSBL.dll","GALME.dll"}, 592, true, "NATIVE-ABI-0002-01"),
            ["mmud:wbb"] = new("mmud", NativeDistributionIdentity.Wbb, "88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091", 643072, new[]{"cw3220mt.DLL","KERNEL32.dll","WGSERVER.EXE","GALGSBL.dll","GALME.dll"}, 592, true, "NATIVE-ABI-0002-01"),
            ["mmpls:wcc"] = new("mmpls", NativeDistributionIdentity.Wcc, "bc15929af9f0b0693b123683b55f1752ae7f703d620cdcb79d9b2a185e408aa5", 28672, new[]{"cw3220mt.DLL","KERNEL32.dll","WGSERVER.EXE","GALGSBL.dll","WCCMMUD.dll"}, 14, true, "NATIVE-ABI-0002-01"),
            ["mmpls:wbb"] = new("mmpls", NativeDistributionIdentity.Wbb, "e73973e68dc2b872b97c867731a5be57166fe544c7ba6abb2ee9c219216776bb", 28672, new[]{"cw3220mt.DLL","KERNEL32.dll","WGSERVER.EXE","GALGSBL.dll","WBBMMUD.dll"}, 14, true, "NATIVE-ABI-0002-01"),
            ["mmutl:wcc"] = new("mmutl", NativeDistributionIdentity.Wcc, "e509a3f73bd032e5bed8218f100ce0e664d5d22a6b0be30e0853a031788dc4b7", 106496, new[]{"cw3220mt.DLL","KERNEL32.dll","USER32.dll","WSOCK32.dll","ADVAPI32.dll","wbtrv32.dll"}, 74, true, "NATIVE-ABI-0002-01"),
            ["mmutl:wbb"] = new("mmutl", NativeDistributionIdentity.Wbb, "c9f558b23f35b61ca85c5e799014294fda75b561e77d6d2963c3823337320790", 106496, new[]{"cw3220mt.DLL","KERNEL32.dll","USER32.dll","WSOCK32.dll","ADVAPI32.dll","wbtrv32.dll"}, 74, true, "NATIVE-ABI-0002-01")
        };

    public static NativeModuleAbiIdentity Resolve(string module, NativeDistributionIdentity distribution)
    {
        var key = $"{module}:{distribution.ToString().ToLowerInvariant()}";
        return Entries.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"Unknown native module identity: {key}");
    }

    public static bool ValidateArtifact(string module, NativeDistributionIdentity distribution, string sha256, long size)
    {
        var identity = Resolve(module, distribution);
        return identity.Size == size && identity.Sha256.Equals(sha256, StringComparison.OrdinalIgnoreCase);
    }
}
