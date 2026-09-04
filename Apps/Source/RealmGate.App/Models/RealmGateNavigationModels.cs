using System.Collections.ObjectModel;

namespace RealmGate.App.Models;

public enum RealmGateSavedDestinationKind
{
    Favorite,
    Stash
}

public sealed class RealmGateSavedDestination
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Saved location";
    public string RoomKey { get; set; } = string.Empty;
    public RealmGateSavedDestinationKind Kind { get; set; } = RealmGateSavedDestinationKind.Favorite;
    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string DisplayName => $"{(Kind == RealmGateSavedDestinationKind.Stash ? "Stash" : "Favorite")} • {Name} • {RoomKey}";
}

public sealed class RealmGateNavigationPreferences
{
    public bool AvoidDamageTraps { get; set; } = true;
    public bool AvoidSpellTraps { get; set; } = true;
    public bool AvoidRestrictedExits { get; set; } = true;
    public bool AvoidHiddenExits { get; set; }
    public bool AllowOrdinaryDoors { get; set; } = true;
    public bool AvoidHighLevelNpcRooms { get; set; } = true;
    public int NpcLevelBuffer { get; set; } = 5;
    public bool AvoidRoomSpellEffects { get; set; }
    public bool AllowRiskyFallback { get; set; }
    public int LoopMinimumSteps { get; set; } = 4;
    public int LoopMaximumSteps { get; set; } = 12;
}

public sealed class RealmGateNavigationProfile
{
    public int Version { get; set; } = 1;
    public string ServerKey { get; set; } = string.Empty;
    public RealmGateNavigationPreferences Preferences { get; set; } = new();
    public ObservableCollection<RealmGateSavedDestination> SavedDestinations { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record RealmGateNavigationRoom(
    string Key,
    string Name,
    int MapNumber,
    int RoomNumber,
    int LairId,
    int RoomSpellId,
    int MaximumNpcLevel,
    int ExitCount)
{
    public string DisplayName => $"{Name} [{Key}]";
}

public sealed record RealmGateNavigationLair(
    int LairId,
    string EntryRoomKey,
    string Name,
    int RoomCount)
{
    public string DisplayName => $"Lair {LairId} • {Name} • {RoomCount:N0} room(s)";
}

public sealed record RealmGateNavigationBankLocation(
    string Name,
    string RoomKey)
{
    public string DisplayName => $"{Name} • {RoomKey}";
}

public sealed record RealmGateNavigationHazard(
    string RoomKey,
    string Category,
    string Description,
    int Penalty);

public sealed class RealmGateNavigationPlan
{
    public string StartRoomKey { get; init; } = string.Empty;
    public string DestinationRoomKey { get; init; } = string.Empty;
    public string DestinationName { get; init; } = string.Empty;
    public MegaMudPathRecord? Route { get; init; }
    public bool UsedRiskyFallback { get; init; }
    public int RiskScore { get; init; }
    public IReadOnlyList<RealmGateNavigationHazard> Hazards { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; set; } = [];
    public bool Success => Route is not null;
    public string Summary
    {
        get
        {
            if (Route is null)
                return Warnings.Count == 0 ? "No route was found." : string.Join(Environment.NewLine, Warnings);

            var risk = RiskScore == 0 ? "no scored hazards" : $"risk score {RiskScore}";
            var fallback = UsedRiskyFallback ? " • risky fallback used" : string.Empty;
            return $"{Route.Steps.Count:N0} step(s) • {risk}{fallback}";
        }
    }
}
