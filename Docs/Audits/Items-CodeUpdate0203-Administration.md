# Items CodeUpdate0203 — Administration Runtime

This pass adds executable, permission-agnostic administrative primitives for item inspection, movement, evidence-gated repair, explicit removal, and catalog/runtime auditing.

The Items subsystem does not grant administrative permission. Callers must authenticate and audit the operator. Movement preserves instance identities and validates ownership, location, container hierarchy, limited-item state, and the complete runtime state atomically.

Native MajorMUD sysop syntax, permission levels, messages, and repair exceptions remain unresolved for the final native/file pass.
