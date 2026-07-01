from __future__ import annotations

from pathlib import Path
from typing import Any
import csv


def norm(value: Any) -> str:
    return "" if value is None else str(value).strip()


def as_int(value: Any, default: int = 0) -> int:
    s = norm(value)
    if not s:
        return default
    try:
        return int(float(s))
    except (TypeError, ValueError):
        return default


def as_float(value: Any, default: float = 0.0) -> float:
    s = norm(value)
    if not s:
        return default
    try:
        return float(s)
    except (TypeError, ValueError):
        return default


def contains(row: dict[str, Any], text: str) -> bool:
    needle = text.lower()
    return any(needle in norm(v).lower() for v in row.values())


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(newline="", encoding="utf-8-sig", errors="replace") as f:
        return list(csv.DictReader(f))
