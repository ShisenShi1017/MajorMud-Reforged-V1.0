# Android ChatGPT Decode Workflow

This workflow is for phone-based ChatGPT decode work where PowerShell, local scripts, and direct repository editing are not available.

## Purpose

Use Android ChatGPT for analysis, planning, field review, discrepancy review, and handoff drafting while keeping the GitHub repository as the authoritative working copy.

Phone ChatGPT should not create a separate working repository. It should produce structured handoff text that Codex can apply to the local repository after the current pushed update is green.

## Source of Truth

Authoritative order:

1. Original MajorMUD binaries.
2. `WGSERVER.EXE`.
3. Native DAT files.
4. GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0`.
5. WBB comparison files.
6. Imported datasets.
7. HTTrack/OmniVerse mirror as verification only.

OmniVerse must never override binary evidence.

OmniVerse public IDs must never be assumed to equal binary record indexes.

## What Phone ChatGPT Can Safely Do

- Review decode reports pasted from the repository.
- Compare field interpretations conceptually.
- Draft next-pass plans.
- Draft discrepancy lists.
- Draft confidence updates.
- Draft report sections.
- Produce proposed CSV rows in fenced code blocks.
- Produce a handoff using the template below.

## What Phone ChatGPT Must Not Claim

- Do not claim files were changed in the repository.
- Do not claim GitHub Actions passed unless the user says the pushed commit is green.
- Do not claim a field is promoted unless binary evidence is cited.
- Do not claim OmniVerse proves a field by itself.
- Do not claim local scripts or builds ran.

## Required Phone Handoff Template

Copy this template into phone ChatGPT for each decode pass.

```text
MajorMUD Reforged Phone Decode Handoff

Decode Pass:
DataDecode00XX - [name]

Repository Baseline:
- Last pushed CodeUpdate:
- User-reported GitHub Actions status:
- Relevant repo files reviewed:

Authoritative Evidence Reviewed:
- Original binary/DAT evidence:
- WGSERVER evidence:
- Existing repository report evidence:
- OmniVerse/MudInfo/WBB comparison evidence:

Findings:
1.
2.
3.

Candidate Fields:
- Field:
  Evidence:
  Confidence:
  Promotion status: candidate only / ready for Codex review / blocked

Discrepancies:
- Source A:
- Source B:
- Difference:
- Proposed next verification:

Promotions:
- None, unless binary-backed proof is listed below.

Binary-backed Promotions Proposed:
- Field:
  Binary evidence:
  Corroborating evidence:
  Affected schema/report:

Files Codex Should Update:
- AI_CONTEXT.md
- CHANGELOG.md
- VERSION.md
- ReleaseNotes.md
- Docs/Architecture/PROJECT_ROADMAP.md
- DataDecodeCheckpoints/...
- Reports/...

Boundaries:
- No gameplay.
- No canonical Data rewrite unless explicitly approved.
- No OmniVerse-only promotion.
- No U16/U32-only promotion.

Next Recommended Pass:
```

## Phone-to-Codex Rule

When returning from phone ChatGPT to Codex, paste the complete handoff and identify any files or ZIPs you want Codex to inspect.

Codex then:

1. Verifies the current repository state.
2. Verifies the current update number, milestone, and database version.
3. Applies only reviewed repository changes.
4. Updates continuity documents.
5. Runs local verification when available.
6. Commits locally.
7. Waits for the user to push and report green GitHub Actions.

## Current Next Pass

After CodeUpdate-0048, the likely next decode pass remains:

DataDecode0051 - Combat Routine Correlation.

Focus:

- Damage calculations.
- Armor calculations.
- Spell calculations.
- Resistance calculations.
- Ability calculations.

No field should be promoted without binary-backed proof.
