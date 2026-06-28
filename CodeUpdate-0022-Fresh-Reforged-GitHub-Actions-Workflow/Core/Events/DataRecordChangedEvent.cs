namespace Reforged.Core.Events;

public sealed class DataRecordChangedEvent : ReforgedEventBase
{
    public override string EventName => "DataRecordChanged";
    public string EntityType { get; init; } = string.Empty;
    public string StableId { get; init; } = string.Empty;
    public string ChangeKind { get; init; } = string.Empty;
}
