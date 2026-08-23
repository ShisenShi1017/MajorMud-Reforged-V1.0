# Code Update 0259 - Borin Forge Connect/Login Routing and Truthful Connection State

Cumulative through Pass 0018F.

## Purpose

Fix the case where William types a direct Forge instruction such as `connect and login to omniverse` but Borin answers conversationally instead of activating the real Client session. Also prevent configured server identity from being described as active play while the socket or MajorMUD login is not verified.

## Behavior

- Explicit Forge connect/login instructions bypass the language model and call the real Borin session login controller.
- Explicit disconnect/logout instructions bypass the language model and call the real session disconnect path.
- Questions such as `are you connected to omniverse?` remain read-only state questions.
- A configured Omniverse or Reforged profile is described as configured while disconnected, connected while logging in, and playing only after the MajorMUD login state is Ready.
- Connection attempts report the actual post-attempt Client state instead of a canned `connecting` claim.
- The existing prompt monitor, secure credential path, server identity separation, verified perception, real-terminal action routing, NO_REPLY repair, and hard 270-character in-game speech limit remain intact.

## AI data

This update does not include or replace AI models, GGUF files, AI configuration, credentials, knowledge, memories, journals, or telnet logs.
