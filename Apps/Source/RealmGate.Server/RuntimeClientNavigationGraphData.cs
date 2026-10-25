using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private const int NavigationGraphChunkSize = 24 * 1024;
    private const int MaximumNavigationGraphBytes = 32 * 1024 * 1024;

    private string RenderClientNavigationGraphData(RuntimeSession session)
    {
        try
        {
            var selectedWorldId = session.World.Definition.Id;
            var resolved = ServerDataRootResolver.Resolve(_characters.ProjectRoot, selectedWorldId);
            var path = Path.Combine(resolved.DataRoot, "navigation_graph.json");
            if (!File.Exists(path))
                return NavigationGraphError(selectedWorldId, "The selected world has no generated navigation graph.");

            var json = File.ReadAllBytes(path);
            if (json.Length == 0 || json.Length > MaximumNavigationGraphBytes)
                return NavigationGraphError(selectedWorldId, "The generated navigation graph has an invalid size.");

            using (var document = JsonDocument.Parse(json))
            {
                var graphWorldId = document.RootElement.TryGetProperty("worldId", out var worldValue)
                    ? worldValue.GetString()?.Trim() ?? string.Empty
                    : string.Empty;
                if (!NormalizeWorldId(graphWorldId).Equals(
                        NormalizeWorldId(resolved.WorldId),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return NavigationGraphError(
                        selectedWorldId,
                        $"The generated navigation graph belongs to '{graphWorldId}', not the selected world.");
                }
            }

            byte[] compressed;
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
                    gzip.Write(json);
                compressed = output.ToArray();
            }

            var transferId = Guid.NewGuid().ToString("N");
            var chunkCount = (compressed.Length + NavigationGraphChunkSize - 1) / NavigationGraphChunkSize;
            var sha256 = Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant();
            var response = new StringBuilder(compressed.Length * 2);
            response.Append(ReforgedClientProtocol.Encode("NAVGRAPHBEGIN", JsonSerializer.Serialize(new
            {
                transferId,
                worldId = resolved.WorldId,
                uncompressedLength = json.Length,
                compressedLength = compressed.Length,
                chunkCount,
                sha256
            })));

            for (var index = 0; index < chunkCount; index++)
            {
                var offset = index * NavigationGraphChunkSize;
                var length = Math.Min(NavigationGraphChunkSize, compressed.Length - offset);
                response.AppendLine();
                response.Append(ReforgedClientProtocol.Encode("NAVGRAPHCHUNK", JsonSerializer.Serialize(new
                {
                    transferId,
                    index,
                    data = Convert.ToBase64String(compressed, offset, length)
                })));
            }

            response.AppendLine();
            response.Append(ReforgedClientProtocol.Encode("NAVGRAPHEND", JsonSerializer.Serialize(new
            {
                transferId,
                worldId = resolved.WorldId,
                sha256
            })));
            return response.ToString();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or InvalidOperationException)
        {
            return NavigationGraphError(session.World.Definition.Id, exception.Message);
        }
    }

    private static string NavigationGraphError(string worldId, string message) =>
        ReforgedClientProtocol.Encode("NAVGRAPHERROR", JsonSerializer.Serialize(new
        {
            worldId,
            message
        }));

    private static string NormalizeWorldId(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
}
