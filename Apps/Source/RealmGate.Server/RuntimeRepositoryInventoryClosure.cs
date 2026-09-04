using System;
using System.Collections.Generic;
using System.Linq;

namespace RealmGate.Server;

public sealed record RuntimeInventoryDeclaration(string Id, string OwnerSubsystem, string RuntimeOwner, string PersistenceOwner);
public sealed record RuntimeInventoryClosureResult(bool Passed, IReadOnlyList<string> Errors);

public sealed class RuntimeRepositoryInventoryClosureService
{
    public RuntimeInventoryClosureResult Validate(IEnumerable<RuntimeInventoryDeclaration> declarations, ISet<string> knownSubsystems)
    {
        var errors = new List<string>();
        var rows = declarations?.ToArray() ?? Array.Empty<RuntimeInventoryDeclaration>();
        foreach (var group in rows.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            errors.Add("duplicate-inventory-id:" + group.Key);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Id)) errors.Add("missing-inventory-id");
            if (!knownSubsystems.Contains(row.OwnerSubsystem)) errors.Add("unknown-owner:" + row.Id);
            if (string.IsNullOrWhiteSpace(row.RuntimeOwner)) errors.Add("missing-runtime-owner:" + row.Id);
            if (string.IsNullOrWhiteSpace(row.PersistenceOwner)) errors.Add("missing-persistence-owner:" + row.Id);
        }
        return new RuntimeInventoryClosureResult(errors.Count == 0, errors);
    }
}
