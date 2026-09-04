using System.Collections.ObjectModel;
using System.IO;

namespace RealmGate.App.Models;

public sealed class MegaMudPathRecord
{
    public string FileName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string StartLabel { get; init; } = string.Empty;
    public string EndLabel { get; init; } = string.Empty;
    public MegaMudPathEndpoint? Start { get; init; }
    public MegaMudPathEndpoint? End { get; init; }
    public string NativeStartKey { get; init; } = string.Empty;
    public string NativeEndKey { get; init; } = string.Empty;
    public int DeclaredStepCount { get; init; }
    public ObservableCollection<MegaMudPathStep> Steps { get; init; } = [];
    public ObservableCollection<string> Warnings { get; init; } = [];
    public bool IsImported { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(StartLabel) && string.IsNullOrWhiteSpace(EndLabel)
        ? Path.GetFileNameWithoutExtension(FileName)
        : $"{StartLabel} → {(string.IsNullOrWhiteSpace(EndLabel) ? "Loop" : EndLabel)}";
    public string StepSummary => $"{Steps.Count:N0} steps" + (Warnings.Count == 0 ? string.Empty : $" • {Warnings.Count} warning(s)");
}

public sealed class MegaMudPathEndpoint
{
    public string Code { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int? MapNumber { get; init; }
    public int? RoomNumber { get; init; }
    public string? LibrariumRoomId { get; set; }
    public string? LibrariumName { get; set; }
    public string? LibrariumDescription { get; set; }
    public bool IsMatched => !string.IsNullOrWhiteSpace(LibrariumRoomId);
    public string MatchStatus => IsMatched ? $"Matched: {LibrariumName}" : "No map-qualified Librarium match";
}

public sealed class MegaMudPathStep
{
    public int Number { get; init; }
    public string NativeRoomKey { get; init; } = string.Empty;
    public string NativeFlags { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public bool IsMovement { get; init; }
    public string FriendlyCommand { get; init; } = string.Empty;
    public string ExpectedNativeRoomKey { get; init; } = string.Empty;
    public string ExpectedRoomSummary => string.IsNullOrWhiteSpace(ExpectedNativeRoomKey) ? "Room change" : $"Native room {ExpectedNativeRoomKey}";
}

public sealed class ImportedPathProfile
{
    public string ServerProfile { get; set; } = "Local Host";
    public string WorldProfile { get; set; } = "Classic";
    public ObservableCollection<ImportedPathEntry> Paths { get; set; } = [];
}

public sealed class ImportedPathEntry
{
    public string SourceFileName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string StartLabel { get; set; } = string.Empty;
    public string EndLabel { get; set; } = string.Empty;
    public ObservableCollection<string> Commands { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public bool Favorite { get; set; }
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RouteRecordingState
{
    public bool IsRecording { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string RouteName { get; set; } = "Recorded Route";
    public string StartRoom { get; set; } = string.Empty;
    public ObservableCollection<string> Commands { get; } = [];
    public string Summary => IsRecording ? $"Recording • {Commands.Count} command(s)" : "Not recording";
}
