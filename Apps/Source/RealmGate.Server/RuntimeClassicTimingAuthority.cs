namespace RealmGate.Server;

/// <summary>
/// MajorMUD 1.11p timing constants recovered from WCCMMUD.DLL plus retained
/// WCCMMUD/WCCMMHLP configuration/help. Native background work is driven by a
/// one-second fast callback and owner-specific logical-tick buckets; it is not a
/// synthetic universal wall-clock tick.
/// </summary>
public static class RuntimeClassicTimingAuthority
{
    // PASS: stock MajorMUD physical combat round / energy bucket.
    public const int CombatRoundSeconds = 5;

    // PASS: decoded spell/status duration and native medium bucket.
    public const int SpellSystemRoundSeconds = 3;

    // CodeUpdate0336 direct WCCMMUD.DLL background scheduler constants.
    public const int FastSchedulerSeconds = 1;
    public const int MediumSchedulerFastTicks = 3;
    public const int EnergySchedulerFastTicks = 5;
    public const int MonsterCreateSchedulerFastTicks = 5;
    public const int SlowSchedulerFastTicks = 30;

    // Player regeneration is owned by the native slow/fast character update paths.
    public const int PassiveRegenerationSeconds = SlowSchedulerFastTicks;
    public const int RestCounterThreshold = 20;
    public const int RestRegenerationFastTicks = RestCounterThreshold + 1;
    public const int MeditateCounterThreshold = 14;
    public const int MeditateRegenerationFastTicks = MeditateCounterThreshold + 1;

    // Compatibility/reporting names. These are logical fast-tick counts, which are
    // seconds only while the one-shot fast callback is being serviced on time.
    public const int RestRegenerationSeconds = RestRegenerationFastTicks;
    public const int MeditateRegenerationSeconds = MeditateRegenerationFastTicks;

    // WCCMMUD.mcv: "Seconds between saving Player data? 60"
    public const int PlayerDataSaveIntervalSeconds = RuntimeClassicConfigurationAuthority.PlayerDataSaveIntervalSeconds;

    // WCCMMUD.mcv: "Seconds between saving Buffer data? 300". This is a setup
    // value separate from the recovered background-save callback constant; owner
    // mapping remains evidence-gated.
    public const int BufferDataSaveIntervalSeconds = RuntimeClassicConfigurationAuthority.BufferDataSaveIntervalSeconds;

    // WCCMMHLP HELP EXIT/QUIT/SAVE.
    public const int GracefulExitDelaySeconds = 5;

    // WCCMMHLP says REST/MEDITATE need "at least 20 seconds". This is historical
    // presentation guidance, not the executable scheduling source. Native REST fires
    // on fast counter >20 (21st callback); native MEDITATE fires on >14 (15th).
    public const int FocusedRegenerationHelpSeconds = 20;
    public const int FocusedRegenerationActivationSeconds = FocusedRegenerationHelpSeconds;

    public static readonly TimeSpan PlayerDataSaveInterval = TimeSpan.FromSeconds(PlayerDataSaveIntervalSeconds);
    public static readonly TimeSpan FocusedRegenerationActivationDelay = TimeSpan.FromSeconds(FocusedRegenerationHelpSeconds);
}
