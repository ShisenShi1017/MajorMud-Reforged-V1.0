# Runtime Artifact Handoff

`Bin/` is the normal-user runtime area. Normal users should launch compiled/runtime-ready artifacts from here and should not need the .NET SDK installed locally.

## Current Milestone 1 expectation

Milestone 1 is still source-framework focused, so executable artifacts are not required yet. The expected future executable paths are documented in `Bin/RuntimeArtifacts.json` and mirrored by the Core runtime handoff validator.

## Verification recommendation

Use GitHub Actions as the official build verification path:

1. Push the repository to GitHub.
2. Open the repository's **Actions** tab.
3. Run or review **MajorMud Reforged Cloud Build**.
4. Treat the update as build-verified only after that workflow succeeds.

This keeps the project private if your GitHub repository is private and avoids requiring a local .NET SDK on your computer.
