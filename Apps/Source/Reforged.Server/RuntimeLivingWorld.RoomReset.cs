namespace Reforged.Server;

public sealed record RuntimeRoomAreaResetResult(
    string AreaId,
    IReadOnlyList<string> RoomIds,
    int MatchedObjectCount,
    int ChangedObjectCount,
    IReadOnlyList<string> ResetObjectIds,
    DateTimeOffset ExecutedAtUtc);

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeRoomAreaResetResult DispatchScheduledRoomReset(RuntimeScheduledEventSnapshot scheduledEvent, DateTimeOffset nowUtc)
    {
        if (!scheduledEvent.Kind.Equals("room-area-reset", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("scheduled-event-is-not-room-area-reset");

        if (!scheduledEvent.Metadata.TryGetValue("area_id", out var areaId) || string.IsNullOrWhiteSpace(areaId))
            throw new InvalidOperationException("room-area-reset-area-id-required");

        if (!scheduledEvent.Metadata.TryGetValue("room_ids", out var roomIdsText) || string.IsNullOrWhiteSpace(roomIdsText))
            throw new InvalidOperationException("room-area-reset-room-ids-required");

        var roomIds = roomIdsText
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roomIds.Length == 0)
            throw new InvalidOperationException("room-area-reset-room-ids-required");

        return ResetRoomArea(areaId, roomIds, nowUtc);
    }

    public RuntimeRoomAreaResetResult ResetRoomArea(string areaId, IEnumerable<string> roomIds, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(areaId)) throw new ArgumentException("area-id-required", nameof(areaId));
        ArgumentNullException.ThrowIfNull(roomIds);

        var normalizedRoomIds = roomIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedRoomIds.Length == 0)
            throw new ArgumentException("room-ids-required", nameof(roomIds));

        var roomIdSet = normalizedRoomIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            var matchingDefinitions = _objectDefinitions.Values
                .Where(definition => roomIdSet.Contains(definition.RoomId))
                .OrderBy(definition => definition.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var resetObjectIds = new List<string>();
            foreach (var definition in matchingDefinitions)
            {
                if (!_objects.TryGetValue(definition.Id, out var current)) continue;

                var reset = new RuntimeLivingWorldObjectState(
                    definition.Id,
                    definition.InitiallyOpen,
                    definition.InitiallyLocked,
                    false,
                    nowUtc);

                if (current.Open == reset.Open
                    && current.Locked == reset.Locked
                    && current.Activated == reset.Activated)
                    continue;

                _objects[definition.Id] = reset;
                resetObjectIds.Add(definition.Id);
            }

            if (resetObjectIds.Count > 0) Save();

            return new RuntimeRoomAreaResetResult(
                areaId.Trim(),
                normalizedRoomIds,
                matchingDefinitions.Length,
                resetObjectIds.Count,
                resetObjectIds,
                nowUtc);
        }
    }
}
