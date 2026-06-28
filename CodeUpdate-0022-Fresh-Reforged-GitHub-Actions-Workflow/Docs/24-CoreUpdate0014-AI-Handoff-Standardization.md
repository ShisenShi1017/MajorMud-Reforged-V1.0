# Core Update 0014 - AI Handoff Standardization

## Purpose

Core Update 0014 makes `AI_CONTEXT.md` the authoritative handoff document for Reforged. The goal is to let a new ChatGPT window or developer immediately understand the project without depending on previous conversation memory.

## What Changed

- Standardized `AI_CONTEXT.md` into a predictable structure.
- Added a required session startup order.
- Added current repository facts.
- Added a living Milestone 1 deliverable status table.
- Added a living Milestone 1 completion checklist.
- Added permanent project rules in one place.
- Added explicit handoff maintenance requirements for every future Code Update.
- Added update history and next recommended work.

## Required Future Maintenance

Every Code Update must update `AI_CONTEXT.md` before packaging the update. At minimum, each update must refresh:

1. Current Code Update.
2. Last Update Name.
3. Milestone estimate.
4. Deliverable status table.
5. Completion checklist.
6. Update history summary.
7. Handoff notes for the update.
8. Next recommended work.

The update is incomplete if `AI_CONTEXT.md` still points to the previous update number or stale next work.

## Why This Matters

The project is now large enough that continuity cannot depend on chat history. `AI_CONTEXT.md` must carry the project state forward after every update, especially when the user starts a new window or uploads a fresh repository ZIP.

## Verification Expectations

A future AI or developer should be able to open the repository, read `AI_CONTEXT.md`, and know:

- Current milestone.
- Current Code Update.
- Current Database Version.
- Permanent project rules.
- What work is complete.
- What work remains.
- What to do next.

