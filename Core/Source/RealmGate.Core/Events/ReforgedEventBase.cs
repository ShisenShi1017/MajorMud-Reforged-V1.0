namespace RealmGate.Core.Events;

/// <summary>
/// Base event record that supplies a UTC timestamp for diagnostics and smoke tests.
/// </summary>
public abstract record ReforgedEventBase(DateTimeOffset TimestampUtc) : IReforgedEvent;
