namespace Reforged.Core.Importing;

public sealed record ImportAuditRecord(
    string SourceFile,
    string SourceKind,
    int RecordsRead,
    int RecordsWritten,
    int DuplicateKeys,
    int Warnings,
    string Status);
