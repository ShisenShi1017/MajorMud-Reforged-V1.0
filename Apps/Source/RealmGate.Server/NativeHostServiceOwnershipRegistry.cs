namespace RealmGate.Server;

public sealed record NativeHostServiceDomain(string Domain, int SymbolCount, int DirectCallsiteCount);

public static class NativeHostServiceOwnershipRegistry
{
    public const string EvidenceId = "FinalNativeClosure0004";
    public const string Scope = "Proven WGSERVER import ownership domains; host algorithms remain fail-closed.";
    public static readonly IReadOnlyList<NativeHostServiceDomain> Proven = new NativeHostServiceDomain[]
    {
        new("host_message_rendering_transport", 10, 8092),
        new("host_persistence_dfa", 18, 1596),
        new("host_identity_access", 5, 1254),
        new("host_general_runtime", 90, 862),
        new("host_buffer_state", 3, 526),
        new("host_input_parser", 9, 508),
        new("host_randomness", 2, 376),
        new("host_memory", 5, 374),
        new("host_session_user_state", 6, 246),
        new("host_clock_calendar", 5, 130),
        new("host_polling_scheduler", 3, 92),
    };
}
