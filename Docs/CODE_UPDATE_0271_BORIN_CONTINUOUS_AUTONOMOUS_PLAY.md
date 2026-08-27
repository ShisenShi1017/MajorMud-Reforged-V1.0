# Code Update 0271 - Borin Continuous Autonomous Play

## Purpose

Borin's Player role now treats MajorMUD as a continuously controlled game session rather than a periodically permitted action surface. Once **Play by Himself** is active, Borin chooses his own ordinary player actions and continues as soon as the game server provides the next authoritative prompt.

## Continuous player loop

- Removed the artificial 20-autonomous-commands-per-minute stop.
- Removed the fixed 650 ms post-command delay. The authoritative server prompt now paces the loop.
- Removed the repeated-command stop that could incorrectly terminate legitimate repeated combat/rest/gameplay actions.
- A resolved movement command no longer requires the room to change. Locked doors and other failed movement attempts return control to Borin so he can choose another action.
- Borin is explicitly instructed to gather missing information himself instead of asking William for routine gameplay guidance.

## Gameplay states no longer stop autonomy

Combat, low health, unknown maximum HP, and rooms without a currently verified exit list no longer terminate autonomous play. These are gameplay conditions Borin must reason through. Movement still fails closed unless the requested direction is present in the current verified exit list.

The hard boundaries remain unchanged: Borin's Player role still cannot use account/password operations, SYSOP/GAMEOP/ADMIN controls, scripts/macros, shell/OS commands, or main-repository engineering authority.

## Reflection

Automatic reflection is enabled by default and in `AI/Config/borin-player.json`. The reflection journal remains the durable learning/consolidation layer; the existing event interval and cooldown prevent reflection work from interrupting every individual game command.

## Configuration

Borin's default current goal now tells him to play MajorMUD autonomously, learn through verified gameplay, improve through reflection, and make his own ordinary player decisions. Natural-initiative chat limits remain separate from gameplay autonomy and therefore do not throttle his player-action loop.

## Verification note

The autonomous perception harness was updated to require continued autonomy during combat, low health, scalar-only HP, and missing exit evidence. A local .NET build could not be executed in the packaging environment because the .NET SDK is not installed there; compile/test verification should be run on the Reforged development machine before incorporation.
