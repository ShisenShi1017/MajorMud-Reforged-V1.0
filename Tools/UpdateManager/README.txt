MajorMud Reforged Update Manager
================================

Purpose
-------
This folder is reserved for the future compiled update/database-version manager.

Long-term goal
--------------
Instead of relying on large batch files, future update packages should be installed by a proper launcher/update manager that can:

- detect the project root
- create backups
- apply code patches
- apply database versions
- update root README.txt
- update Docs/
- validate Data/
- avoid touching Saves/ and ImportedDatabases/

Current compatibility rule
--------------------------
Until the update manager exists, every update must still include an Apply-*.bat that runs from its own extracted folder.
