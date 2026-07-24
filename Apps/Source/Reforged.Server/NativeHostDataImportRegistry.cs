using System;
using System.Collections.Generic;

namespace Reforged.Server;

public sealed record NativeHostDataImportEvidence(
    string Symbol,
    string Classification,
    bool StaticControlTransferObserved,
    string ExactNativeType,
    string EvidenceClass);

public static class NativeHostDataImportRegistry
{
    private static readonly IReadOnlyDictionary<string, NativeHostDataImportEvidence> Entries =
        new Dictionary<string, NativeHostDataImportEvidence>(StringComparer.Ordinal)
        {
            ["_register_agent"] = new("_register_agent", "imported_without_static_reference", false, "unknown_without_host_symbols_or_runtime", "proven"),
        };

    public static bool IsProvenStaticCallback(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return Entries.TryGetValue(symbol, out var entry) && entry.StaticControlTransferObserved;
    }

    public static NativeHostDataImportEvidence ResolveKnownBoundary(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (Entries.TryGetValue(symbol, out var entry))
        {
            return entry;
        }

        return new NativeHostDataImportEvidence(
            symbol,
            "host_data_global_boundary",
            false,
            "unknown_without_host_symbols_or_runtime",
            "proven_static_access_form_only");
    }

    public static void RequireExactNativeType(string symbol)
    {
        throw new InvalidOperationException(
            $"Exact native type for WGSERVER data import '{symbol}' is unavailable without host symbols or runtime memory evidence.");
    }
}
