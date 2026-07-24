using System;
using System.Collections.Generic;

namespace Reforged.Server;

public sealed record NativeHostImportLayout(string FileName, string Sha256, uint StartVa, uint EndVa, int SlotCount, int SlotWidthBytes);

public static class NativeHostImportLayoutRegistry
{
    private static readonly IReadOnlyDictionary<string, NativeHostImportLayout> Layouts =
        new Dictionary<string, NativeHostImportLayout>(StringComparer.OrdinalIgnoreCase)
        {
            ["wccmmud.dll"] = new("wccmmud.dll", "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077", 0x0049d488, 0x0049d6d8, 149, 4),
            ["wbbmmud.dll"] = new("wbbmmud.dll", "88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091", 0x0049d488, 0x0049d6d8, 149, 4),
            ["wccmmpls.dll"] = new("wccmmpls.dll", "bc15929af9f0b0693b123683b55f1752ae7f703d620cdcb79d9b2a185e408aa5", 0x00407254, 0x0040732c, 55, 4),
            ["wbbmmpls.dll"] = new("wbbmmpls.dll", "e73973e68dc2b872b97c867731a5be57166fe544c7ba6abb2ee9c219216776bb", 0x00407254, 0x0040732c, 55, 4),
        };

    public static NativeHostImportLayout Require(string fileName, string sha256)
    {
        if (!Layouts.TryGetValue(fileName, out var layout) || !string.Equals(layout.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unknown or hash-mismatched native host import layout.");
        return layout;
    }
}
