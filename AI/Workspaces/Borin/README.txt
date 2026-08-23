Borin Controlled Engineering Workspace
======================================

Stage 6 Pass 0002 adds an approval-gated engineering job orchestrator over the
isolated workspace introduced in Pass 0001.

Natural workflow
----------------
1. Say: Fix <problem>
2. Review the diagnosis and proposed unified diff.
3. Say: Show me the proposed changes
4. Say: Approve this draft
5. Say: Build and test it
6. Say: Create the update zip

Recorded job stages
-------------------
Investigating -> Drafted -> AwaitingApproval -> AppliedToWorkspace -> Built ->
Validated -> Packaged

The orchestrator will not skip stages. Approval applies a draft only to the
private repository snapshot. Build and validation execute only in that snapshot.
Packaging creates a review-only update ZIP under AI\Workspaces\Borin\Packages.

Job metadata is stored locally under AI\Workspaces\Borin\Jobs. No operation in
this pass applies source changes to the main repository, runs an installer,
commits, or pushes.
