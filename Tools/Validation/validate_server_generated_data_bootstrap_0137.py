from pathlib import Path
import json
import sys

root = Path(__file__).resolve().parents[2]
errors = []
config_path = root / "Config" / "server.worlds.json"

if not config_path.exists():
    errors.append(f"Missing configuration: {config_path.relative_to(root)}")
else:
    try:
        config = json.loads(config_path.read_text(encoding="utf-8"))
    except Exception as exc:
        errors.append(f"Invalid server world configuration: {exc}")
        config = {}

    default_id = str(config.get("defaultWorldId", "")).strip()
    worlds = config.get("worlds", [])
    if not default_id:
        errors.append("server.worlds.json has no defaultWorldId")
    if not isinstance(worlds, list):
        errors.append("server.worlds.json worlds must be an array")
        worlds = []

    world = next((entry for entry in worlds if str(entry.get("id", "")).lower() == default_id.lower()), None)
    if world is None and default_id:
        errors.append(f"Default world '{default_id}' is not declared")
    elif world is not None:
        configured_root = world.get("dataRoot")
        if not isinstance(configured_root, str) or not configured_root.strip():
            errors.append(f"Default world '{default_id}' has no dataRoot")
        else:
            data_root = (root / configured_root).resolve()
            try:
                data_root.relative_to(root.resolve())
            except ValueError:
                errors.append(f"Configured dataRoot escapes repository root: {configured_root}")

            manifest_path = data_root / "compile_manifest.json"
            if not manifest_path.exists():
                errors.append(f"Missing compile manifest: {manifest_path}")
            else:
                try:
                    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
                except Exception as exc:
                    errors.append(f"Invalid compile manifest: {exc}")
                    manifest = {}

                families = manifest.get("families")
                if not isinstance(families, list) or not families:
                    errors.append("compile_manifest.json has no non-empty families array")
                else:
                    for entry in families:
                        family = entry.get("family") if isinstance(entry, dict) else None
                        if not isinstance(family, str) or not family.strip():
                            errors.append("compile_manifest.json contains an unnamed family")
                            continue
                        path = data_root / f"{family}.json"
                        if not path.exists():
                            errors.append(f"Missing generated family: {path.relative_to(root)}")
                            continue
                        try:
                            payload = json.loads(path.read_text(encoding="utf-8"))
                        except Exception as exc:
                            errors.append(f"Invalid generated family {path.relative_to(root)}: {exc}")
                            continue
                        if not isinstance(payload, (dict, list)):
                            errors.append(f"Generated family root must be object or array: {path.relative_to(root)}")

source_checks = {
    root / "Apps" / "Source" / "Reforged.Server" / "ServerDataRootResolver.cs": [
        "server.worlds.json", "defaultWorldId", "dataRoot", "Path.GetRelativePath"
    ],
    root / "Apps" / "Source" / "Reforged.Server" / "ServerDataCatalogAudit.cs": [
        "compile_manifest.json", "families", "ServerDataRootResolver.ResolveDefault"
    ],
    root / "Apps" / "Source" / "Reforged.Server" / "ServerStartupDiagnostics.cs": [
        "ServerDataRootResolver.ResolveDefault", "CoreStartupDiagnostics().Run(projectRoot, configuredDataRoot)"
    ],
}
for path, tokens in source_checks.items():
    if not path.exists():
        errors.append(f"Missing bootstrap source: {path.relative_to(root)}")
        continue
    text = path.read_text(encoding="utf-8")
    for token in tokens:
        if token not in text:
            errors.append(f"Bootstrap source {path.relative_to(root)} missing token: {token}")

print(json.dumps({"passed": not errors, "errors": errors}, indent=2))
sys.exit(0 if not errors else 1)
