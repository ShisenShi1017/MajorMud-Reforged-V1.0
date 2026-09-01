namespace Reforged.Server;

/// <summary>
/// CodeUpdate0369 native closure for the former combat Speed-setting STRONG row.
/// WCCMMUD.INI UPDATE_SPEED is loaded at global 0x004906C1 and all non-loader
/// references are in Worldgroup update/poll pacing callsites. WCCMMUD!_compute_energy_used
/// has no reference to that global. Therefore UPDATE_SPEED must not be injected into
/// player attack-energy math.
/// </summary>
public static class RuntimeClassicAttackSpeedBoundaryAuthority
{
    public const string WccMudSha256 = "5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077";
    public const uint UpdateSpeedGlobalVa = 0x004906C1u;
    public const uint ComputeEnergyUsedExportVa = 0x0042A0C8u;
    public static readonly uint[] UpdateSpeedNonLoaderReferenceVas = [0x004029E9u, 0x00402A44u, 0x0045E6BFu, 0x0045E739u];

    public const bool UpdateSpeedIsHostUpdatePacing = true;
    public const bool ComputeEnergyUsedReferencesUpdateSpeed = false;
    public const int ClassicLiveAttackSpeedPercent = 100;

    public static IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        Check(UpdateSpeedIsHostUpdatePacing && !ComputeEnergyUsedReferencesUpdateSpeed,
            "update-speed-owner-boundary-demoted", failures);
        Check(ClassicLiveAttackSpeedPercent == 100,
            "classic-live-attack-speed-percent-not-native-default", failures);
        if (UpdateSpeedNonLoaderReferenceVas.Length != 4)
            failures.Add("update-speed-native-xref-count");
        return failures;
    }

    private static void Check(bool condition, string failure, List<string> failures)
    {
        if (!condition) failures.Add(failure);
    }
}
