# Code Update 0269 - Borin Verified Main Incorporation

## Result

The autonomous engineering workflow now has a distinct human-only main-repository incorporation action. Borin continues to investigate, draft, patch, build, test, and correct inside his isolated workspace without supervision. A verified job exposes **Review & Incorporate** only after the isolated build and tests pass.

## Approval UI

The confirmation presents the job ID and stage, build and validation summaries, exact proposed files, and a bounded diff preview. William must explicitly select Yes. Cancel leaves the patch only in the isolated workspace.

## Local validation

Before main application, the local workspace authority:

- Revalidates the exact job path scope.
- Rejects workspace changes outside that scope.
- Rejects file deletions.
- Rejects any target file already modified or untracked in the main working tree.
- Generates a recoverable approved patch artifact.
- Runs `git apply --check` before atomic application.

Unrelated user modifications are preserved. No commit, push, publish, installation, deployment, production mutation, or Worldsmith mutation occurs.

## Verification

- NVIDIA/provider/authority harness: 34/34 PASS.
- New orchestration regression proves an unverified job cannot incorporate and a verified job can reach incorporation only through the explicit local action.

