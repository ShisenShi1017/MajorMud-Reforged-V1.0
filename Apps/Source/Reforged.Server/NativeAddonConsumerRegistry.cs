using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MajorMud.Reforged.Server;

public static class NativeAddonConsumerRegistry
{
    public const string EvidenceId = "native-addon-consumer-0028";

    public static IReadOnlyDictionary<int,string> Parse(TextReader reader)
    {
        var records = new Dictionary<int,string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length > 99) throw new FormatException("Native loader uses a 100-byte fgets buffer.");
            var split = line.IndexOf(':');
            if (split < 0) continue;
            if (!int.TryParse(line[..split], NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot)) continue;
            var token = line[(split + 1)..];
            records[slot] = token; // Native prepend semantics make the last physical duplicate win.
        }
        return records;
    }

    public static bool TryGetToken(IReadOnlyDictionary<int,string> records, int slot, out string token)
        => records.TryGetValue(slot, out token!);

    // Exact equality is delegated to the Worldgroup-compatible _sameas implementation.
    // This registry does not invent host collation or case-folding behavior.
}
