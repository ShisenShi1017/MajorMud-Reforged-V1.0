from __future__ import annotations
from .util import norm


def format_rows(rows: list[dict], fields: list[str] | None = None, limit: int = 20) -> str:
    lines: list[str] = []
    if not rows:
        return "(none)"
    for r in rows[:limit]:
        use_fields = fields or list(r.keys())[:8]
        lines.append(" | ".join(f"{f}={norm(r.get(f))}" for f in use_fields if f in r))
    if len(rows) > limit:
        lines.append(f"... {len(rows) - limit} more")
    return "\n".join(lines)
