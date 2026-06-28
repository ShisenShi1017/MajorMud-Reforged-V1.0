# MajorMud Reforged Suggestions

This file records recommendations and ideas. Suggestions are not approved features and must not be implemented unless the user explicitly approves them.

## Suggestion 001: Keep Documentation As A Required Patch Step

### Recommendation

Every official patch should update documentation as needed, especially:

- `03-DevelopmentJournal.md`
- `04-SessionHandoff.md`
- `07-KnownIssues.md`
- `10-DecisionLog.md`, when architectural decisions are made.
- Release notes.
- Patch history.

### Reason

This keeps the project resumable across new chats and future development sessions.

### Status

Accepted as a project practice unless the user later changes the rule.

## Suggestion 002: Start Server Work With An Inspection Patch Or Stability Patch

### Recommendation

Before adding large features, inspect the current server and create a small patch that documents current server behavior, startup flow, data loading, and known gaps.

### Reason

This avoids accidental rewrites and creates a safe baseline before major server work.

### Status

Pending future user approval.

## Suggestion 003: Preserve A Strict Patch Format

### Recommendation

Every official patch should be extractable and runnable from its own patch folder using an `Apply-*.bat` file.

### Reason

This prevents confusion about where to run patches and supports safe, repeatable updates.

### Status

Accepted as a project rule.
