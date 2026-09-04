namespace RealmGate.Server;

public static class NativeWccDmaConsumerRegistry
{
    public const string EvidenceId = "native_wcc_dma_consumer_0029";
    public const string WccFileName = "WCCDMA.SYS";
    public const int RequiredRecordType = 3;
    public const int IdentifierBytes = 8;
    public const int IdentifierBufferBytes = 9;
    public const int TokenCopyBound = 50;
    public const int TokenTerminatorOffset = 49;

    public static bool IsStructurallyValid(string? ip, string? identifier, string? token)
        => !string.IsNullOrWhiteSpace(ip)
           && identifier is { Length: 8 }
           && token is { Length: > 0 and <= TokenTerminatorOffset };

    public static bool CanGenerateOrValidateToken => false;
}
