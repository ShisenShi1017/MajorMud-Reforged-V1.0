#!/usr/bin/env python3
"""Static integrity and release-readiness validator for Client Bundle 42."""
from __future__ import annotations

import hashlib
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "CLIENT_BUNDLE_42_MANIFEST.json"
REQUIRED = [
    ROOT / "INSTALL_VALIDATE_AND_PACKAGE_CLIENT_BUNDLE_42.bat",
    ROOT / "Tools" / "Install-ClientBundle42.ps1",
    ROOT / "Tools" / "validate_client_bundle_42.py",
    ROOT / "Docs" / "CLIENT_BUNDLE_42.md",
    ROOT / "Docs" / "CLIENT_RELEASE_CHECKLIST.md",
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def fail(message: str) -> None:
    print(f"FAIL: {message}")
    raise SystemExit(1)


def main() -> int:
    if not MANIFEST.is_file():
        fail("CLIENT_BUNDLE_42_MANIFEST.json is missing")
    data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if data.get("bundle") != 42:
        fail("manifest bundle number is not 42")
    if data.get("type") != "true-cumulative-changed-files-only":
        fail("manifest type is not true cumulative")

    entries = data.get("files") or []
    if not entries:
        fail("manifest contains no files")

    seen: set[str] = set()
    for entry in entries:
        relative = entry.get("path")
        if not relative or relative in seen:
            fail(f"invalid or duplicate manifest path: {relative!r}")
        seen.add(relative)
        path = ROOT / relative
        if not path.is_file():
            fail(f"manifest file missing: {relative}")
        actual = sha256(path)
        if actual != entry.get("sha256"):
            fail(f"hash mismatch: {relative}")
        if path.stat().st_size != entry.get("size"):
            fail(f"size mismatch: {relative}")

    for path in REQUIRED:
        if not path.is_file():
            fail(f"required Bundle 42 file missing: {path.relative_to(ROOT)}")

    ps = (ROOT / "Tools" / "Install-ClientBundle42.ps1").read_text(encoding="utf-8")
    for token in ["CLIENT_BUNDLE_42_MANIFEST.json", "Get-FileHash", "dotnet", "Compress-Archive", ".client-bundle-backups"]:
        if token not in ps:
            fail(f"installer is missing required behavior token: {token}")

    for xml_path in ROOT.glob("Apps/**/*.xaml"):
        try:
            ET.parse(xml_path)
        except ET.ParseError as exc:
            fail(f"invalid XAML/XML: {xml_path.relative_to(ROOT)}: {exc}")
    for xml_path in ROOT.glob("Apps/**/*.csproj"):
        try:
            ET.parse(xml_path)
        except ET.ParseError as exc:
            fail(f"invalid project XML: {xml_path.relative_to(ROOT)}: {exc}")

    prior = ROOT / "CLIENT_BUNDLE_41_MANIFEST.json"
    if not prior.is_file():
        fail("Bundle 41 cumulative manifest is missing")
    prior_paths = {entry["path"] for entry in json.loads(prior.read_text(encoding="utf-8"))["files"]}
    missing_prior = sorted(prior_paths - seen)
    if missing_prior:
        fail(f"Bundle 42 does not preserve {len(missing_prior)} Bundle 41 files")

    print(f"PASS: Client Bundle 42 validated ({len(entries)} cumulative files).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
