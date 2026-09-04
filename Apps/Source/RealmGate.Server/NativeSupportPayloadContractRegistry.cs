using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace RealmGate.Server;

public static class NativeSupportPayloadContractRegistry
{
    public const string EvidenceId = "native-support-payload-contracts-0025";
    public static readonly byte[] ProvenDosAttributes = { 7, 9, 11, 12, 14, 15 };
    private static readonly Regex Addon = new(@"^(?<slot>[1-9]):(?<token>[A-Z]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex Dma = new(@"^(?<ip>\d{1,3}(?:\.\d{1,3}){3}) (?<id>\d{8}) (?<token>[A-Z]+)$", RegexOptions.CultureInvariant);

    public static bool TryParseAddonRecord(string line, out int slot, out string token)
    {
        var m = Addon.Match(line); slot = 0; token = string.Empty;
        if (!m.Success || !int.TryParse(m.Groups["slot"].Value, out slot)) return false;
        token = m.Groups["token"].Value; return true;
    }

    public static bool TryParseDmaRecord(string line, out string address, out string identifier, out string token)
    {
        var m = Dma.Match(line); address = identifier = token = string.Empty;
        if (!m.Success || !IPAddress.TryParse(m.Groups["ip"].Value, out _)) return false;
        address=m.Groups["ip"].Value; identifier=m.Groups["id"].Value; token=m.Groups["token"].Value; return true;
    }

    public static bool IsProvenDosAttribute(byte value) => ProvenDosAttributes.Contains(value);
    public static void Validate()
    {
        if (ProvenDosAttributes.Distinct().Count()!=6) throw new InvalidOperationException("Native DOS attribute set mismatch.");
    }
}
