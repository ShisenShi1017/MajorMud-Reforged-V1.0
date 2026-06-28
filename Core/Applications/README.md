# Core Applications Contracts

This folder contains shared application identity and bootstrapping contracts.

Rules:

- Core may define common application contracts.
- Core must never reference Server, Importer, Editor, Launcher, or Client projects.
- Each application project must reference Core.
- Runtime still launches compiled artifacts from `Bin/`; source projects and project references are development structure only.
