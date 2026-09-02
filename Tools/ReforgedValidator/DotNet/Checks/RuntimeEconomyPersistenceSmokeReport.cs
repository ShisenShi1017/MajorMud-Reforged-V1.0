namespace Reforged.Server;

public static class RuntimeEconomyPersistenceSmokeReport
{
    public static readonly string[] RequiredScenarios=
    {
        "atomic-multi-component-autosave",
        "logout-and-disconnect-save-coordination",
        "failed-store-write-restores-all-participants",
        "checkpoint-checksum-corruption-rejection",
        "last-known-good-backup-recovery",
        "validated-journal-replay-recovery",
        "component-schema-compatibility-rejection",
        "replay-key-duplicate-save-protection",
        "bounded-journal-retention",
        "versioned-envelope-round-trip"
    };
}
