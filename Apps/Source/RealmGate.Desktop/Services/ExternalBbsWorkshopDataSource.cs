using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RealmGate.Client.Profiles;

namespace RealmGate.Desktop.Services;

public sealed record ExternalBbsWorkshopDataSource(string ClassicDataRoot, string OverlayPath)
{
    public static ExternalBbsWorkshopDataSource Resolve(ClientConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var classicRoot = FindClassicDataRoot();
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var serverKey = BuildServerKey(profile);
        var current = Path.Combine(local, "RealmGate", "Client", "GameDataOverlays", serverKey, "overlay.json");
        var legacy = Path.Combine(local, "MajorMUD Reforged", "Client", "GameDataOverlays", serverKey, "overlay.json");
        var overlay = File.Exists(current) || !File.Exists(legacy) ? current : legacy;
        return new ExternalBbsWorkshopDataSource(classicRoot, overlay);
    }

    private static string FindClassicDataRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var current = new DirectoryInfo(start); current is not null; current = current.Parent)
            {
                var candidate = Path.Combine(current.FullName, "Data", "Classic");
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }
        throw new DirectoryNotFoundException(
            "Generated Data/Classic was not found. External-BBS item planning requires the local Classic client base.");
    }

    private static string BuildServerKey(ClientConnectionProfile profile)
    {
        var raw = $"{profile.ServerProfileName.Trim()}|{profile.HostName.Trim().ToLowerInvariant()}|{profile.Port.ToString(CultureInfo.InvariantCulture)}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..12];
        return $"{Slug(profile.ServerProfileName)}-{hash}";
    }

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }
        return builder.ToString().Trim('-');
    }
}
