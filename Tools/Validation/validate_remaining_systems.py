#!/usr/bin/env python3
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CATALOG_DIR = ROOT / "Librarium/RemainingSystems"
REQUIRED = [
    CATALOG_DIR / "remaining_systems_registry.json",
    CATALOG_DIR / "party_catalog.json",
    CATALOG_DIR / "communication_catalog.json",
    CATALOG_DIR / "organization_catalog.json",
    CATALOG_DIR / "door_lock_access_catalog.json",
    CATALOG_DIR / "trap_catalog.json",
    CATALOG_DIR / "environmental_hazard_catalog.json",
    CATALOG_DIR / "world_container_catalog.json",
    CATALOG_DIR / "clock_scheduling_catalog.json",
    CATALOG_DIR / "world_reset_cleanup_catalog.json",
    CATALOG_DIR / "world_event_catalog.json",
    CATALOG_DIR / "pvp_policy_catalog.json",
    CATALOG_DIR / "law_policy_catalog.json",
    CATALOG_DIR / "rest_recovery_policy_catalog.json",
    CATALOG_DIR / "transport_service_catalog.json",
    CATALOG_DIR / "player_transaction_catalog.json",
    CATALOG_DIR / "mail_offline_messaging_catalog.json",
    CATALOG_DIR / "account_session_ownership_catalog.json",
    CATALOG_DIR / "world_rules_configuration_catalog.json",
    CATALOG_DIR / "administrative_policy_catalog.json",
    CATALOG_DIR / "security_permission_catalog.json",
    CATALOG_DIR / "logging_diagnostics_catalog.json",
    CATALOG_DIR / "save_coordination_catalog.json",
    CATALOG_DIR / "startup_shutdown_catalog.json",
    CATALOG_DIR / "worldgroup_integration_catalog.json",
    CATALOG_DIR / "global_random_catalog.json",
    CATALOG_DIR / "global_world_state_catalog.json",
    CATALOG_DIR / "weather_day_night_catalog.json",
    CATALOG_DIR / "import_export_compatibility_catalog.json",
    CATALOG_DIR / "help_reference_catalog.json",
    CATALOG_DIR / "rankings_statistics_catalog.json",
    CATALOG_DIR / "character_identity_policy_catalog.json",
    CATALOG_DIR / "capacity_resource_limits_catalog.json",
    CATALOG_DIR / "error_handling_recovery_catalog.json",
    CATALOG_DIR / "builder_content_editing_catalog.json",
    CATALOG_DIR / "repository_inventory_closure.json",
    ROOT / "Reports/RemainingSystems/native_dependencies.json",
]


def load(path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def require_identity(rows, label):
    ids = [entry.get("id") for entry in rows]
    if not rows or any(not isinstance(value, str) or not value.strip() for value in ids):
        raise SystemExit(f"invalid-{label}-id")
    if len(ids) != len(set(ids)):
        raise SystemExit(f"duplicate-{label}-id")
    return ids


def main():
    missing = [str(path.relative_to(ROOT)) for path in REQUIRED if not path.is_file()]
    if missing:
        raise SystemExit("missing:" + ",".join(missing))

    registry = load(CATALOG_DIR / "remaining_systems_registry.json")
    systems = registry.get("systems", [])
    require_identity(systems, "subsystem")
    for entry in systems:
        system_id = entry["id"]
        if not isinstance(entry.get("runtimeOwner"), str) or not entry["runtimeOwner"].strip():
            raise SystemExit("missing-runtime-owner:" + system_id)
        if not isinstance(entry.get("status"), str) or not entry["status"].strip():
            raise SystemExit("missing-subsystem-status:" + system_id)

    closure = load(CATALOG_DIR / "repository_inventory_closure.json")
    accepted_closure_states = {
        "repository-visible-inventory-closed-native-evidence-required",
        "evidence-backed-strong-native-owner-inventory-closure",
    }
    if closure.get("closureStatus") not in accepted_closure_states:
        raise SystemExit("inventory-closure-status-invalid")

    inventory_systems = closure.get("systems", [])
    inventory_ids = require_identity(inventory_systems, "inventory-system")
    for entry in inventory_systems:
        if not isinstance(entry.get("runtimeOwner"), str) or not entry["runtimeOwner"].strip():
            raise SystemExit("missing-inventory-runtime-owner:" + entry["id"])
    for collection in ("recordFamilies", "commandFamilies", "timers", "stateObjects", "persistenceOwners"):
        for row in closure.get(collection, []):
            owner = row.get("ownerSubsystem") or row.get("subsystem")
            if owner not in inventory_ids:
                raise SystemExit("inventory-unknown-owner:" + str(owner))
    if not closure.get("nativeDependencies"):
        raise SystemExit("inventory-native-dependencies-empty")

    dependencies = load(ROOT / "Reports/RemainingSystems/native_dependencies.json").get("dependencies", [])
    if not dependencies:
        raise SystemExit("native-dependency-inventory-empty")

    print(json.dumps({
        "passed": True,
        "systems": len(systems),
        "inventorySystems": len(inventory_systems),
        "nativeDependencies": len(dependencies),
        "retainedFiles": len(REQUIRED),
    }, indent=2))


if __name__ == "__main__":
    main()
