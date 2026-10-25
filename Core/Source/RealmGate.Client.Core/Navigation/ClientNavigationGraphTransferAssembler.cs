using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RealmGate.Client.Protocol;

namespace RealmGate.Client.Navigation;

public sealed record ClientNavigationGraphTransferPayload(
    string WorldId,
    string Sha256,
    int UncompressedLength,
    byte[] CompressedBytes)
{
    public byte[] DecompressAndVerify()
    {
        using var compressed = new MemoryStream(CompressedBytes, writable: false);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var output = new MemoryStream(UncompressedLength);
        var buffer = new byte[81920];
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > UncompressedLength)
                throw new InvalidDataException("The navigation graph exceeds its declared uncompressed size.");
            output.Write(buffer, 0, read);
        }
        var bytes = output.ToArray();
        if (bytes.Length != UncompressedLength)
            throw new InvalidDataException("The navigation graph length does not match its transfer manifest.");

        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!actual.Equals(Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The navigation graph failed SHA-256 verification.");
        return bytes;
    }
}

public sealed class ClientNavigationGraphTransferAssembler
{
    private const int MaximumUncompressedBytes = 32 * 1024 * 1024;
    private const int MaximumCompressedBytes = 16 * 1024 * 1024;
    private const int MaximumChunkCount = 4096;
    private TransferState? _state;

    public void Reset() => _state = null;

    public ClientNavigationGraphTransferPayload? Process(RealmGateClientProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.Channel.ToUpperInvariant() switch
        {
            "NAVGRAPHBEGIN" => Begin(message.Payload),
            "NAVGRAPHCHUNK" => AddChunk(message.Payload),
            "NAVGRAPHEND" => End(message.Payload),
            "NAVGRAPHERROR" => throw ReadServerError(message.Payload),
            _ => null
        };
    }

    private ClientNavigationGraphTransferPayload? Begin(string payload)
    {
        var frame = Deserialize<BeginFrame>(payload);
        if (string.IsNullOrWhiteSpace(frame.TransferId) || string.IsNullOrWhiteSpace(frame.WorldId) ||
            frame.UncompressedLength is <= 0 or > MaximumUncompressedBytes ||
            frame.CompressedLength is <= 0 or > MaximumCompressedBytes ||
            frame.ChunkCount is <= 0 or > MaximumChunkCount ||
            !IsSha256(frame.Sha256))
        {
            throw new InvalidDataException("The navigation graph transfer manifest is invalid.");
        }

        _state = new TransferState(frame);
        return null;
    }

    private ClientNavigationGraphTransferPayload? AddChunk(string payload)
    {
        var frame = Deserialize<ChunkFrame>(payload);
        var state = RequireState(frame.TransferId);
        if (frame.Index < 0 || frame.Index >= state.Manifest.ChunkCount || state.Chunks[frame.Index] is not null)
            throw new InvalidDataException("The navigation graph contains an invalid or duplicate chunk.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(frame.Data ?? string.Empty);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("A navigation graph chunk is not valid base64.", exception);
        }
        if (bytes.Length == 0 || state.ReceivedLength + bytes.Length > state.Manifest.CompressedLength)
            throw new InvalidDataException("A navigation graph chunk exceeds the declared transfer size.");

        state.Chunks[frame.Index] = bytes;
        state.ReceivedCount++;
        state.ReceivedLength += bytes.Length;
        return null;
    }

    private ClientNavigationGraphTransferPayload End(string payload)
    {
        var frame = Deserialize<EndFrame>(payload);
        var state = RequireState(frame.TransferId);
        try
        {
            if (!NormalizeWorldId(frame.WorldId).Equals(
                    NormalizeWorldId(state.Manifest.WorldId),
                    StringComparison.OrdinalIgnoreCase) ||
                !frame.Sha256.Equals(state.Manifest.Sha256, StringComparison.OrdinalIgnoreCase) ||
                state.ReceivedCount != state.Manifest.ChunkCount ||
                state.ReceivedLength != state.Manifest.CompressedLength)
            {
                throw new InvalidDataException("The navigation graph transfer is incomplete or does not match its manifest.");
            }

            var compressed = new byte[state.ReceivedLength];
            var offset = 0;
            foreach (var chunk in state.Chunks)
            {
                if (chunk is null)
                    throw new InvalidDataException("The navigation graph transfer is missing a chunk.");
                Buffer.BlockCopy(chunk, 0, compressed, offset, chunk.Length);
                offset += chunk.Length;
            }

            return new(
                state.Manifest.WorldId,
                state.Manifest.Sha256,
                state.Manifest.UncompressedLength,
                compressed);
        }
        finally
        {
            _state = null;
        }
    }

    private TransferState RequireState(string? transferId)
    {
        if (_state is null || string.IsNullOrWhiteSpace(transferId) ||
            !_state.Manifest.TransferId.Equals(transferId, StringComparison.Ordinal))
            throw new InvalidDataException("The navigation graph frame does not belong to the active transfer.");
        return _state;
    }

    private static InvalidOperationException ReadServerError(string payload)
    {
        var frame = Deserialize<ErrorFrame>(payload);
        var message = string.IsNullOrWhiteSpace(frame.Message)
            ? "The server could not provide the selected world's navigation graph."
            : frame.Message.Trim();
        return new(message);
    }

    private static T Deserialize<T>(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payload, JsonOptions)
                   ?? throw new InvalidDataException("The navigation graph frame is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The navigation graph frame is malformed.", exception);
        }
    }

    private static bool IsSha256(string? value) =>
        value?.Length == 64 && value.All(Uri.IsHexDigit);

    private static string NormalizeWorldId(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record BeginFrame(
        string TransferId,
        string WorldId,
        int UncompressedLength,
        int CompressedLength,
        int ChunkCount,
        string Sha256);

    private sealed record ChunkFrame(string TransferId, int Index, string Data);
    private sealed record EndFrame(string TransferId, string WorldId, string Sha256);
    private sealed record ErrorFrame(string WorldId, string Message);

    private sealed class TransferState(BeginFrame manifest)
    {
        public BeginFrame Manifest { get; } = manifest;
        public byte[]?[] Chunks { get; } = new byte[manifest.ChunkCount][];
        public int ReceivedCount { get; set; }
        public int ReceivedLength { get; set; }
    }
}
