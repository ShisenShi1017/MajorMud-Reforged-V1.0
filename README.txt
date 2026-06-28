MajorMud Reforged
=================

Current Update
--------------
CodePatch 0002 - Clean Repository / App Platform Foundation

What changed in this update
---------------------------
This update starts the move away from a PowerShell prototype and toward a cleaner standalone application layout.

New direction:
- The project will be organized around Apps, Source, Core, Data, Tools, Docs, and Bin folders.
- Future executable files will live in their own folders under Bin/Windows.
- The user should not need to install the .NET SDK, Visual Studio, or developer tools to run released builds.
- PowerShell is now considered legacy/prototype infrastructure, not the long-term app platform.
- Original MajorMud imported files remain reference-only.
- Reforged Data remains the server-ready editable database.

Important repository rule
-------------------------
When applying updates, run the Apply-*.bat file from the extracted update folder.
Do not copy update files by hand unless specifically instructed.

Current layout focus
--------------------
Apps/       Human-facing application folders and launch documentation.
Bin/        Future compiled executable output folders.
Source/     Clean source layout for Core, Server, Editor, Importer, Launcher, and Client.
Data/       Reforged editable database used by the server.
Docs/       Project history, architecture, rules, release notes, and handoff notes.
Tools/      Import, validation, and update utilities.
Legacy/     Older prototype files moved here instead of being deleted.

Executable policy
-----------------
Executable files must be placed in their own app-specific folder when created, for example:

Bin/Windows/Server/MajorMudReforged.Server.exe
Bin/Windows/Launcher/MajorMudReforged.Launcher.exe
Bin/Windows/Editor/MajorMudReforged.Editor.exe

This update does not claim that those executable files already exist.
