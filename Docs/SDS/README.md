# Software Design Specifications

Status: Draft planning documents for CodeUpdate-0037.

These documents preserve the approved future-system topics from the CodeUpdate-0036B and CodeUpdate-0037 documentation packages. They are planning records only.

CodeUpdate-0039 adds the Classic-first Server mode direction. The first real Server target is Classic baseline only; Reforged mode, Custom mode, AI systems, and alternate gameplay rules require explicit future approval.

CodeUpdate-0039 also adds engine architecture principles. The Server should evolve as a reusable engine capable of supporting Classic, Reforged, custom worlds, future expansions, and additional rule sets without separate server codebases.

Boundaries:

- No runtime gameplay code is implemented by these documents.
- No `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- Future implementation still requires a separate approved Code Update.
