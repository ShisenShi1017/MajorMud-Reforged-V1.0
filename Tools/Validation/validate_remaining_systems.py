#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REQUIRED = [
    ROOT / "Librarium/RemainingSystems/remaining_systems_registry.json",
    ROOT / "Librarium/RemainingSystems/party_catalog.json",
    ROOT / "Librarium/RemainingSystems/communication_catalog.json",
    ROOT / "Librarium/RemainingSystems/organization_catalog.json",
    ROOT / "Librarium/RemainingSystems/door_lock_access_catalog.json",
    ROOT / "Librarium/RemainingSystems/trap_catalog.json",
    ROOT / "Librarium/RemainingSystems/environmental_hazard_catalog.json",
    ROOT / "Librarium/RemainingSystems/world_container_catalog.json",
    ROOT / "Librarium/RemainingSystems/clock_scheduling_catalog.json",
    ROOT / "Librarium/RemainingSystems/world_reset_cleanup_catalog.json",
    ROOT / "Librarium/RemainingSystems/world_event_catalog.json",
    ROOT / "Librarium/RemainingSystems/pvp_policy_catalog.json",
    ROOT / "Librarium/RemainingSystems/law_policy_catalog.json",
    ROOT / "Librarium/RemainingSystems/rest_recovery_policy_catalog.json",
    ROOT / "Librarium/RemainingSystems/transport_service_catalog.json",
    ROOT / "Librarium/RemainingSystems/player_transaction_catalog.json",
    ROOT / "Librarium/RemainingSystems/mail_offline_messaging_catalog.json",
    ROOT / "Librarium/RemainingSystems/account_session_ownership_catalog.json",
    ROOT / "Librarium/RemainingSystems/world_rules_configuration_catalog.json",
    ROOT / "Librarium/RemainingSystems/administrative_policy_catalog.json",
    ROOT / "Librarium/RemainingSystems/security_permission_catalog.json",
    ROOT / "Librarium/RemainingSystems/logging_diagnostics_catalog.json",
    ROOT / "Librarium/RemainingSystems/save_coordination_catalog.json",
    ROOT / "Librarium/RemainingSystems/startup_shutdown_catalog.json",
    ROOT / "Librarium/RemainingSystems/worldgroup_integration_catalog.json",
    ROOT / "Librarium/RemainingSystems/global_random_catalog.json",
    ROOT / "Librarium/RemainingSystems/global_world_state_catalog.json",
    ROOT / "Librarium/RemainingSystems/weather_day_night_catalog.json",
    ROOT / "Librarium/RemainingSystems/import_export_compatibility_catalog.json",
    ROOT / "Librarium/RemainingSystems/help_reference_catalog.json",
    ROOT / "Librarium/RemainingSystems/rankings_statistics_catalog.json",
    ROOT / "Librarium/RemainingSystems/character_identity_policy_catalog.json",
    ROOT / "Librarium/RemainingSystems/capacity_resource_limits_catalog.json",
    ROOT / "Librarium/RemainingSystems/error_handling_recovery_catalog.json",
    ROOT / "Evidence/RemainingSystems/character_identity_policy_provenance.json",
    ROOT / "Evidence/RemainingSystems/capacity_resource_limits_provenance.json",
    ROOT / "Evidence/RemainingSystems/error_handling_recovery_provenance.json",
    ROOT / "Evidence/RemainingSystems/startup_shutdown_provenance.json",
    ROOT / "Evidence/RemainingSystems/worldgroup_integration_provenance.json",
    ROOT / "Evidence/RemainingSystems/global_random_provenance.json",
    ROOT / "Evidence/RemainingSystems/global_world_state_provenance.json",
    ROOT / "Evidence/RemainingSystems/weather_day_night_provenance.json",
    ROOT / "Evidence/RemainingSystems/import_export_compatibility_provenance.json",
    ROOT / "Evidence/RemainingSystems/help_reference_provenance.json",
    ROOT / "Evidence/RemainingSystems/rankings_statistics_provenance.json",
    ROOT / "Evidence/RemainingSystems/party_coordination_provenance.json",
    ROOT / "Evidence/RemainingSystems/communication_provenance.json",
    ROOT / "Evidence/RemainingSystems/organization_provenance.json",
    ROOT / "Evidence/RemainingSystems/door_lock_access_provenance.json",
    ROOT / "Evidence/RemainingSystems/trap_provenance.json",
    ROOT / "Evidence/RemainingSystems/environmental_hazard_provenance.json",
    ROOT / "Evidence/RemainingSystems/world_container_provenance.json",
    ROOT / "Evidence/RemainingSystems/clock_scheduling_provenance.json",
    ROOT / "Evidence/RemainingSystems/world_reset_cleanup_provenance.json",
    ROOT / "Evidence/RemainingSystems/world_event_provenance.json",
    ROOT / "Evidence/RemainingSystems/pvp_policy_provenance.json",
    ROOT / "Evidence/RemainingSystems/law_policy_provenance.json",
    ROOT / "Evidence/RemainingSystems/rest_recovery_policy_provenance.json",
    ROOT / "Evidence/RemainingSystems/transport_service_provenance.json",
    ROOT / "Evidence/RemainingSystems/player_transaction_provenance.json",
    ROOT / "Evidence/RemainingSystems/mail_offline_messaging_provenance.json",
    ROOT / "Evidence/RemainingSystems/account_session_ownership_provenance.json",
    ROOT / "Evidence/RemainingSystems/world_rules_configuration_provenance.json",
    ROOT / "Evidence/RemainingSystems/administrative_policy_provenance.json",
    ROOT / "Evidence/RemainingSystems/security_permission_provenance.json",
    ROOT / "Evidence/RemainingSystems/logging_diagnostics_provenance.json",
    ROOT / "Evidence/RemainingSystems/save_coordination_provenance.json",
    ROOT / "Reports/RemainingSystems/native_dependencies.json",
    ROOT / "Librarium/RemainingSystems/builder_content_editing_catalog.json",
    ROOT / "Evidence/RemainingSystems/builder_content_editing_provenance.json",
    ROOT / "Librarium/RemainingSystems/repository_inventory_closure.json",
    ROOT / "Evidence/RemainingSystems/repository_inventory_closure_provenance.json",
    ROOT / "Reports/RemainingSystems/repository_inventory_closure_0037.md",
]

def load(path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)

def main():
    missing = [str(path.relative_to(ROOT)) for path in REQUIRED if not path.is_file()]
    if missing:
        raise SystemExit("missing:" + ",".join(missing))
    registry = load(REQUIRED[0])
    systems = registry.get("systems", [])
    ids = [entry.get("id") for entry in systems]
    if len(ids) != len(set(ids)):
        raise SystemExit("duplicate-subsystem-id")
    party = next((entry for entry in systems if entry.get("id") == "party-coordination"), None)
    if not party or party.get("runtimeOwner") != "RuntimePartyCoordinationService":
        raise SystemExit("party-runtime-owner-invalid")

    rng = next((entry for entry in systems if entry.get("id") == "global-rng"), None)
    if not rng or rng.get("runtimeOwner") != "RuntimeGlobalRandomService":
        raise SystemExit("global-rng-runtime-owner-invalid")
    if rng.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("global-rng-status-invalid")
    world_state = next((entry for entry in systems if entry.get("id") == "global-world-state"), None)
    if not world_state or world_state.get("runtimeOwner") != "RuntimeGlobalWorldStateService":
        raise SystemExit("global-world-state-runtime-owner-invalid")
    if world_state.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("global-world-state-status-invalid")
    weather = next((entry for entry in systems if entry.get("id") == "weather-day-night"), None)
    if not weather or weather.get("runtimeOwner") != "RuntimeWeatherDayNightService":
        raise SystemExit("weather-day-night-runtime-owner-invalid")
    if weather.get("status") != "implemented-safe-disabled-classic-shell":
        raise SystemExit("weather-day-night-status-invalid")
    builder = next((entry for entry in systems if entry.get("id") == "builder-content-editing"), None)
    if not builder or builder.get("runtimeOwner") != "RuntimeBuilderContentPolicyService":
        raise SystemExit("builder-content-editing-runtime-owner-invalid")
    if builder.get("status") != "implemented-reforged-host-boundary-shell":
        raise SystemExit("builder-content-editing-status-invalid")
    compatibility = next((entry for entry in systems if entry.get("id") == "import-export-compatibility"), None)
    if not compatibility or compatibility.get("runtimeOwner") != "RuntimeImportExportCompatibilityService":
        raise SystemExit("import-export-compatibility-runtime-owner-invalid")
    if compatibility.get("status") != "implemented-safe-compatibility-shell":
        raise SystemExit("import-export-compatibility-status-invalid")

    closure = load(ROOT / "Librarium/RemainingSystems/repository_inventory_closure.json")
    if closure.get("closureStatus") != "repository-visible-inventory-closed-native-evidence-required":
        raise SystemExit("inventory-closure-status-invalid")
    if len(closure.get("systems", [])) != len(systems):
        raise SystemExit("inventory-system-count-mismatch")
    for collection in ("recordFamilies", "commandFamilies", "timers", "stateObjects", "persistenceOwners"):
        for row in closure.get(collection, []):
            owner = row.get("ownerSubsystem") or row.get("subsystem")
            if owner not in ids:
                raise SystemExit("inventory-unknown-owner:" + str(owner))
    if not closure.get("nativeDependencies"):
        raise SystemExit("inventory-native-dependencies-empty")

    provenance_files = [p for p in REQUIRED if "provenance" in p.name]
    promotion_count = 0
    organization = next((entry for entry in systems if entry.get("id") == "guild-faction-organization"), None)
    if not organization or organization.get("runtimeOwner") != "RuntimeOrganizationService":
        raise SystemExit("organization-runtime-owner-invalid")
    communication = next((entry for entry in systems if entry.get("id") == "communications"), None)
    if not communication or communication.get("runtimeOwner") != "RuntimeCommunicationService":
        raise SystemExit("communication-runtime-owner-invalid")
    doors = next((entry for entry in systems if entry.get("id") == "doors-locks-access"), None)
    if not doors or doors.get("runtimeOwner") != "RuntimeDoorAccessService":
        raise SystemExit("door-runtime-owner-invalid")
    if doors.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("door-status-invalid")
    traps = next((entry for entry in systems if entry.get("id") == "traps"), None)
    if not traps or traps.get("runtimeOwner") != "RuntimeTrapService":
        raise SystemExit("trap-runtime-owner-invalid")
    if traps.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("trap-status-invalid")
    hazards = next((entry for entry in systems if entry.get("id") == "environmental-hazards"), None)
    if not hazards or hazards.get("runtimeOwner") != "RuntimeEnvironmentalHazardService":
        raise SystemExit("environmental-hazard-runtime-owner-invalid")
    if hazards.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("environmental-hazard-status-invalid")
    containers = next((entry for entry in systems if entry.get("id") == "world-containers"), None)
    if not containers or containers.get("runtimeOwner") != "RuntimeWorldContainerService":
        raise SystemExit("world-container-runtime-owner-invalid")
    if containers.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("world-container-status-invalid")
    scheduling = next((entry for entry in systems if entry.get("id") == "clock-scheduling"), None)
    if not scheduling or scheduling.get("runtimeOwner") != "RuntimeClockSchedulingService":
        raise SystemExit("clock-scheduling-runtime-owner-invalid")
    if scheduling.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("clock-scheduling-status-invalid")
    reset = next((entry for entry in systems if entry.get("id") == "world-reset-cleanup"), None)
    if not reset or reset.get("runtimeOwner") != "RuntimeWorldResetCleanupService":
        raise SystemExit("world-reset-runtime-owner-invalid")
    if reset.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("world-reset-status-invalid")
    events = next((entry for entry in systems if entry.get("id") == "events"), None)
    if not events or events.get("runtimeOwner") != "RuntimeWorldEventService":
        raise SystemExit("world-event-runtime-owner-invalid")
    if events.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("world-event-status-invalid")
    pvp = next((entry for entry in systems if entry.get("id") == "pvp-policy"), None)
    if not pvp or pvp.get("runtimeOwner") != "RuntimePvpPolicyService":
        raise SystemExit("pvp-runtime-owner-invalid")
    if pvp.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("pvp-status-invalid")
    law = next((entry for entry in systems if entry.get("id") == "law-bounty-guards"), None)
    if not law or law.get("runtimeOwner") != "RuntimeLawPolicyService":
        raise SystemExit("law-runtime-owner-invalid")
    if law.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("law-status-invalid")
    rest = next((entry for entry in systems if entry.get("id") == "rest-inn-recovery-policy"), None)
    if not rest or rest.get("runtimeOwner") != "RuntimeRestRecoveryPolicyService":
        raise SystemExit("rest-recovery-runtime-owner-invalid")
    if rest.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("rest-recovery-status-invalid")
    transport = next((entry for entry in systems if entry.get("id") == "transport-services"), None)
    if not transport or transport.get("runtimeOwner") != "RuntimeTransportService":
        raise SystemExit("transport-runtime-owner-invalid")
    if transport.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("transport-status-invalid")
    admin = next((entry for entry in systems if entry.get("id") == "administrative-policy"), None)
    if not admin or admin.get("runtimeOwner") != "RuntimeAdministrativePolicyService":
        raise SystemExit("administrative-policy-runtime-owner-invalid")
    if admin.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("administrative-policy-status-invalid")
    security = next((entry for entry in systems if entry.get("id") == "security-permissions"), None)
    if not security or security.get("runtimeOwner") != "RuntimeSecurityPolicyService":
        raise SystemExit("security-permissions-runtime-owner-invalid")
    if security.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("security-permissions-status-invalid")
    logging = next((entry for entry in systems if entry.get("id") == "logging-diagnostics"), None)
    if not logging or logging.get("runtimeOwner") != "RuntimeLoggingDiagnosticsService":
        raise SystemExit("logging-diagnostics-runtime-owner-invalid")
    if logging.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("logging-diagnostics-status-invalid")
    save = next((entry for entry in systems if entry.get("id") == "save-coordination"), None)
    if not save or save.get("runtimeOwner") != "RuntimeSaveCoordinationService":
        raise SystemExit("save-coordination-runtime-owner-invalid")
    if save.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("save-coordination-status-invalid")
    lifecycle = next((entry for entry in systems if entry.get("id") == "startup-shutdown"), None)
    if not lifecycle or lifecycle.get("runtimeOwner") != "RuntimeStartupShutdownService":
        raise SystemExit("startup-shutdown-runtime-owner-invalid")
    if lifecycle.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("startup-shutdown-status-invalid")
    integration = next((entry for entry in systems if entry.get("id") == "worldgroup-integration"), None)
    if not integration or integration.get("runtimeOwner") != "RuntimeWorldgroupIntegrationService":
        raise SystemExit("worldgroup-integration-runtime-owner-invalid")
    if integration.get("status") != "implemented-safe-host-boundary-shell":
        raise SystemExit("worldgroup-integration-status-invalid")
    help_reference = next((entry for entry in systems if entry.get("id") == "help-reference"), None)
    if not help_reference or help_reference.get("runtimeOwner") != "RuntimeHelpReferenceService":
        raise SystemExit("help-reference-runtime-owner-invalid")
    if help_reference.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("help-reference-status-invalid")
    rankings = next((entry for entry in systems if entry.get("id") == "rankings-statistics"), None)
    if not rankings or rankings.get("runtimeOwner") != "RuntimeRankingsStatisticsService":
        raise SystemExit("rankings-statistics-runtime-owner-invalid")
    if rankings.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("rankings-statistics-status-invalid")
    identity = next((entry for entry in systems if entry.get("id") == "character-identity-policy"), None)
    if not identity or identity.get("runtimeOwner") != "RuntimeCharacterIdentityPolicyService":
        raise SystemExit("character-identity-policy-runtime-owner-invalid")
    if identity.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("character-identity-policy-status-invalid")
    capacity = next((entry for entry in systems if entry.get("id") == "capacity-resource-limits"), None)
    if not capacity or capacity.get("runtimeOwner") != "RuntimeCapacityLimitsService":
        raise SystemExit("capacity-resource-limits-runtime-owner-invalid")
    if capacity.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("capacity-resource-limits-status-invalid")
    recovery = next((entry for entry in systems if entry.get("id") == "error-handling-recovery"), None)
    if not recovery or recovery.get("runtimeOwner") != "RuntimeErrorRecoveryService":
        raise SystemExit("error-recovery-runtime-owner-invalid")
    if recovery.get("status") != "implemented-safe-policy-shell":
        raise SystemExit("error-recovery-status-invalid")
    optional_activities = next((entry for entry in systems if entry.get("id") == "games-arenas-optional-activities"), None)
    if not optional_activities or optional_activities.get("runtimeOwner") != "RuntimeOptionalActivitiesService":
        raise SystemExit("optional-activities-runtime-owner-invalid")
    if optional_activities.get("status") != "implemented-evidence-gated-policy-shell":
        raise SystemExit("optional-activities-status-invalid")
    extension = next((entry for entry in systems if entry.get("id") == "reforged-extension-boundaries"), None)
    if not extension or extension.get("runtimeOwner") != "RuntimeExtensionBoundaryService":
        raise SystemExit("extension-boundaries-runtime-owner-invalid")
    if extension.get("status") != "implemented-mode-isolation-shell":
        raise SystemExit("extension-boundaries-status-invalid")
    for provenance_path in provenance_files:
        provenance = load(provenance_path)
        promotion_count += len(provenance.get("promotions", []))
        for promotion in provenance.get("promotions", []):
            required = {"behavior", "classification", "sources", "confidence", "assumptions", "tests", "safeFallback", "runtimeImpact", "characterSaveImpact", "worldStateImpact"}
            if not required.issubset(promotion):
                raise SystemExit("incomplete-provenance:" + str(promotion.get("behavior")))
            for source in promotion["sources"]:
                if not isinstance(source, str) or not source.strip():
                    raise SystemExit("invalid-provenance-source")
                must_be_overlay = ("RemainingSystems/" in source or "RuntimeCommunication" in source or "RuntimePartyCoordination" in source or "RuntimeOrganization" in source or "RuntimeDoorAccess" in source or "RuntimeTrap" in source or "RuntimeEnvironmentalHazard" in source or "RuntimeWorldContainer" in source or "RuntimeClockScheduling" in source or "RuntimeWorldResetCleanup" in source or "RuntimeWorldEvent" in source or "RuntimePvpPolicy" in source or "RuntimeLawPolicy" in source or "RuntimeRestRecoveryPolicy" in source or "RuntimeTransport" in source or "RuntimePlayerTransaction" in source or "RuntimeOfflineMessaging" in source or "RuntimeSessionOwnership" in source or "RuntimeWorldRulesConfiguration" in source or "RuntimeAdministrativePolicy" in source or "RuntimeSecurityPolicy" in source or "RuntimeLoggingDiagnostics" in source or "RuntimeSaveCoordination" in source or "RuntimeStartupShutdown" in source or "RuntimeWorldgroupIntegration" in source or "RuntimeGlobalRandom" in source or "RuntimeGlobalWorldState" in source or "RuntimeWeatherDayNight" in source or "RuntimeBuilderContentPolicy" in source or "RuntimeImportExportCompatibility" in source or "RuntimeHelpReference" in source or "RuntimeRankingsStatistics" in source or "RuntimeCharacterIdentityPolicy" in source or "RuntimeCapacityLimits" in source or "RuntimeErrorRecovery" in source or "RuntimeOptionalActivities" in source or "RuntimeExtensionBoundaries" in source)
                if must_be_overlay and not (ROOT / source).exists():
                    raise SystemExit("missing-overlay-provenance-source:" + source)
    print(json.dumps({"passed": True, "systems": len(systems), "promotions": promotion_count}, indent=2))

if __name__ == "__main__":
    main()
