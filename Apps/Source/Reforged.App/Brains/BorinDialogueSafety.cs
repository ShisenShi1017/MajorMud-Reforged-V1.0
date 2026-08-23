using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.App.Brains;

/// <summary>
/// Shared, deterministic cleanup for text that will be sent through MajorMUD
/// SAY or TELEPATH. This is deliberately model-independent so the production
/// send path and the automated character harness test the exact same boundary.
/// </summary>
public static class BorinDialogueSafety
{
    public static bool IsNoReplyMarker(string? response)
    {
        var normalized = (response ?? string.Empty)
            .Replace("<|im_end|>", string.Empty, StringComparison.Ordinal)
            .Replace("<|endoftext|>", string.Empty, StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        normalized = Regex.Replace(
            normalized,
            @"^(?:assistant|borin)\s*:\s*",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = normalized.Trim('"', '\'', '“', '”', '`', ' ');

        return Regex.IsMatch(
            normalized,
            @"^\[?\s*NO(?:[ _-]*)REPLY\s*\]?[.!]?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static string CleanInGameReply(
        string? response,
        int maximumCharacters = BorinGameStateSnapshot.HardInGameReplyCharacterLimit)
    {
        maximumCharacters = Math.Clamp(
            maximumCharacters,
            1,
            BorinGameStateSnapshot.HardInGameReplyCharacterLimit);

        var cleaned = (response ?? string.Empty)
            .Replace("<|im_end|>", string.Empty, StringComparison.Ordinal)
            .Replace("<|endoftext|>", string.Empty, StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (IsNoReplyMarker(cleaned))
            return string.Empty;

        if (cleaned.StartsWith('{') && cleaned.EndsWith('}'))
        {
            try
            {
                using var document = JsonDocument.Parse(cleaned);
                cleaned = ReadNullableString(document.RootElement, "speech");
            }
            catch (JsonException)
            {
                // Treat malformed JSON as ordinary speech and continue cleaning.
            }
        }

        foreach (var prefix in new[] { "Assistant:", "Borin:", "SAY ", "TELEPATH ", "TELL " })
        {
            if (!cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            cleaned = cleaned[prefix.Length..].Trim();
            break;
        }

        if (IsNoReplyMarker(cleaned))
            return string.Empty;

        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim('"', '\'', '“', '”', ' ');
        if (cleaned.Length <= maximumCharacters)
            return cleaned;
        if (maximumCharacters == 1)
            return "…";

        // Reserve one character for the ellipsis. This prevents the historical
        // off-by-one case where the final result could contain 271 characters.
        var contentLimit = maximumCharacters - 1;
        var cut = cleaned.LastIndexOf(' ', contentLimit - 1, contentLimit);
        if (cut < maximumCharacters / 2)
            cut = contentLimit;
        var shortened = cleaned[..cut].TrimEnd() + "…";

        return shortened.Length <= maximumCharacters
            ? shortened
            : shortened[..maximumCharacters];
    }

    public static bool FitsInGameLimit(string? response) =>
        (response?.Length ?? 0) <= BorinGameStateSnapshot.HardInGameReplyCharacterLimit;

    private static string ReadNullableString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : value.ToString().Trim();
    }
}
