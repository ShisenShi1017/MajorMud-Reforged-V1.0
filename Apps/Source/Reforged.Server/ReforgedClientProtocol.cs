using System.Text;

namespace Reforged.Server;

internal static class ReforgedClientProtocol
{
    private const string Prefix = "@@REFORGED-PROTOCOL|";

    public static string Encode(string channel, string payload) => string.Join(
        Environment.NewLine,
        payload.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n')
            .Select(line => $"{Prefix}{channel.Trim().ToUpperInvariant()}|{Convert.ToBase64String(Encoding.UTF8.GetBytes(line))}"));
}
