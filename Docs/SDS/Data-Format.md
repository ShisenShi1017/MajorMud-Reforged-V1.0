# Data Format

Status: Draft.

Purpose: Plan canonical Reforged data models, imported/reference material policy, and reviewed reconciliation workflow.

Boundary: Planning document only. No `Data/` or `ImportedDatabases/` rewrite is included in CodeUpdate-0037.

## Classic-First Direction

The first real Server should load and run Classic data as the baseline. Future Reforged and Custom modes should be modeled as mode/profile evolution after Classic is working, not as current implementation scope.

The WCC default install imported under `ImportedDatabases/WCCDefaultInstall/` is reference/import material only. It does not replace canonical Reforged `Data/`, and it does not approve a data rewrite.
