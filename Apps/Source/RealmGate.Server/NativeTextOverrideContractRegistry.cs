using System;
using System.Text;

namespace RealmGate.Server;

public sealed record NativeTextOverride(string FileName, string Title, string Body, byte[] RawBytes);

public static class NativeTextOverrideContractRegistry
{
    public const string EvidenceId = "native-text-override-contract-0027";

    public static NativeTextOverride ParseHse(string fileName, byte[] raw)
    {
        ValidateAsciiLfOnly(raw);
        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, @"^[A-Za-z]+\d+15\.HSE$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new FormatException("Invalid HSE filename grammar.");
        var text = Encoding.ASCII.GetString(raw);
        var lines = text.Split('\n');
        var logicalCount = lines.Length - (text.EndsWith("\n", StringComparison.Ordinal) ? 1 : 0);
        if (logicalCount is not (5 or 6)) throw new FormatException("HSE must contain five or six logical lines.");
        var title = lines[0];
        var body = string.Join("\n", lines, 1, logicalCount - 1);
        return new NativeTextOverride(fileName, title, body, (byte[])raw.Clone());
    }

    public static NativeTextOverride ParseSto(string fileName, byte[] raw)
    {
        ValidateAsciiLfOnly(raw);
        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, @"^[A-Za-z]+\d+\.STO$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new FormatException("Invalid STO filename grammar.");
        var text = Encoding.ASCII.GetString(raw);
        if (!text.EndsWith("\n", StringComparison.Ordinal)) throw new FormatException("STO requires terminal LF.");
        var lines = text[..^1].Split('\n');
        if (lines.Length != 4) throw new FormatException("STO must contain exactly four logical lines.");
        var id = System.Text.RegularExpressions.Regex.Match(fileName, @"\d+").Value;
        if (!lines[3].Contains($"WCC{id}.STO", StringComparison.OrdinalIgnoreCase)) throw new FormatException("STO identity marker does not match canonical WCC filename.");
        return new NativeTextOverride(fileName, lines[3], string.Join("\n", lines, 0, 3), (byte[])raw.Clone());
    }

    private static void ValidateAsciiLfOnly(byte[] raw)
    {
        if (raw is null || raw.Length == 0) throw new FormatException("Override payload is empty.");
        foreach (var value in raw)
        {
            if (value == 0x0D) throw new FormatException("CR is not present in the supplied native contract.");
            if (value >= 0x80) throw new FormatException("Non-ASCII byte is not proven by supplied native assets.");
        }
    }
}
