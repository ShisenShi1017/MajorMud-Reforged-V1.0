namespace Reforged.Server;

/// <summary>
/// MajorMUD 1.11p timing constants that are directly retained in the native
/// WCCMMUD/WCCMMHLP message catalogs. Keep unresolved host phase/order semantics
/// outside this class rather than turning observed constants into a universal clock model.
/// </summary>
public static class RuntimeClassicTimingAuthority
{
    // PASS: stock MajorMUD physical combat round.
    public const int CombatRoundSeconds = 5;

    // PASS: decoded spell/status duration and periodic-processing round.
    public const int SpellSystemRoundSeconds = 3;

    // STRONG: subsystem-specific recovery cadences. These are deliberately not
    // collapsed into a synthetic universal gameplay tick.
    public const int PassiveRegenerationSeconds = 30;
    public const int RestRegenerationSeconds = 20;
    public const int MeditateRegenerationSeconds = 10;
    // WCCMMUD.mcv: "Seconds between saving Player data? 60"
    public const int PlayerDataSaveIntervalSeconds = RuntimeClassicConfigurationAuthority.PlayerDataSaveIntervalSeconds;

    // WCCMMUD.mcv: "Seconds between saving Buffer data? 300"
    // The exact set/order of native "Buffer data" owners is still unresolved, so this
    // constant is evidence only until an owner-specific save path is recovered.
    public const int BufferDataSaveIntervalSeconds = RuntimeClassicConfigurationAuthority.BufferDataSaveIntervalSeconds;

    // WCCMMHLP.mcv HELP EXIT/QUIT/SAVE.
    public const int GracefulExitDelaySeconds = 5;

    // WCCMMHLP.mcv HELP REST/MEDITATE.
    public const int FocusedRegenerationActivationSeconds = 20;

    public static readonly TimeSpan PlayerDataSaveInterval = TimeSpan.FromSeconds(PlayerDataSaveIntervalSeconds);
    public static readonly TimeSpan FocusedRegenerationActivationDelay = TimeSpan.FromSeconds(FocusedRegenerationActivationSeconds);
}
