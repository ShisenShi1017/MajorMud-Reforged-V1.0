# Guardian Hall Phase 3-0155 — Archivist Reports and Local Tools

Brother Caelum now produces complete preview-only investigation outputs in Markdown, JSON, HTML, and PDF. Codex packages include a handoff, investigation summary, decoded structures, evidence, relationships, suggested-runtime cautions, open questions, and a package manifest.

The local-tool registry remains user-editable and persisted. Each tool supports an executable path, arguments, working directory, enabled state, validation status, launch time, and launch error. Argument tokens are `{file}`, `{folder}`, and `{root}`. Paths are never hardcoded.

Tool validation checks enabled state, executable existence, and working-directory existence before launch. Librarium and runtime data are never modified by report, evidence, Codex-export, validation, or tool-launch operations.
