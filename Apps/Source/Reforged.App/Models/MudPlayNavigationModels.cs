using System.Collections.ObjectModel;

namespace Reforged.App.Models;

public enum MudPlaySavedDestinationKind
{
    Favorite,
    Stash
}

public sealed class MudPlaySavedDestination
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Saved location";
    public string RoomKey { get; set; } = string.Empty;
    public MudPlaySavedDestinationKind Kind { get; set; } = MudPlaySavedDestinationKind.Favorite;
    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string DisplayName => $"{(Kind == MudPlaySavedDestinationKind.Stash ? "Stash" : "Favorite")} • {Name} • {RoomKey}";
}

public sealed class MudPlayNavigationPreferences
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

public sealed class MudPlayNavigationProfile
{
    public int Version { get; set; } = 1;
    public string ServerKey { get; set; } = string.Empty;
    public MudPlayNavigationPreferences Preferences { get; set; } = new();
    public ObservableCollection<MudPlaySavedDestination> SavedDestinations { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record MudPlayNavigationRoom(
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

public sealed record MudPlayNavigationLair(
    int LairId,
    string EntryRoomKey,
    string Name,
    int RoomCount)
{
    public string DisplayName => $"Lair {LairId} • {Name} • {RoomCount:N0} room(s)";
}

public sealed record MudPlayNavigationBankLocation(
    string Name,
    string RoomKey)
{
    public string DisplayName => $"{Name} • {RoomKey}";
}

public sealed record MudPlayNavigationHazard(
    string RoomKey,
    string Category,
    string Description,
    int Penalty);

public sealed class MudPlayNavigationPlan
{
    public string StartRoomKey { get; init; } = string.Empty;
    public string DestinationRoomKey { get; init; } = string.Empty;
    public string DestinationName { get; init; } = string.Empty;
    public MegaMudPathRecord? Route { get; init; }
    public bool UsedRiskyFallback { get; init; }
    public int RiskScore { get; init; }
    public IReadOnlyList<MudPlayNavigationHazard> Hazards { get; init; } = [];
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
