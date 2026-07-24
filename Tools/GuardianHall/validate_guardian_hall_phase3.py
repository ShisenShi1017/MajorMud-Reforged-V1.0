from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
files = {
    "models": root / "Apps/Source/Reforged.App/Guardians/GuardianModels.cs",
    "services": root / "Apps/Source/Reforged.App/Guardians/GuardianServices.cs",
    "vm": root / "Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs",
    "xaml": root / "Apps/Source/Reforged.App/Views/GuardianHallView.xaml",
}
for name, path in files.items():
    if not path.exists():
        raise SystemExit(f"FAIL missing {name}: {path}")

ET.parse(files["xaml"])
models = files["models"].read_text(encoding="utf-8")
services = files["services"].read_text(encoding="utf-8")
vm = files["vm"].read_text(encoding="utf-8")
xaml = files["xaml"].read_text(encoding="utf-8")

for token in [
    "RealmKeeperTelemetrySnapshot", "TelemetryStatus", "LastRefresh",
    "ConnectedHosts", "CoveredWorlds", "GeneratedRecords", "ActiveSaveFiles"
]:
    assert token in models, f"FAIL missing telemetry model token {token}"

for token in [
    "IRealmKeeperTelemetryService", "RealmKeeperTelemetryService", "CaptureAsync",
    "CountGeneratedRecords", "FindRuntimeLogs", "FindCrashEvidence", "ReadHealthMarker"
]:
    assert token in services, f"FAIL missing telemetry service token {token}"

for token in [
    "RefreshRealmKeeperCommand", "RefreshRealmKeeperAsync", "DispatcherTimer",
    "TimeSpan.FromSeconds(30)", "_telemetryTimer.Start()"
]:
    assert token in vm, f"FAIL missing telemetry view-model token {token}"

for token in [
    "Refresh Telemetry", "Connected Hosts", "World Folders", "Generated Records",
    "Save Files", "RealmKeeper.TelemetryStatus", "RealmKeeper.LastRefresh"
]:
    assert token in xaml, f"FAIL missing Realm Keeper telemetry UI token {token}"

assert "Character" not in xaml[xaml.index("Realm Keeper War Room"):xaml.index("Wanderer Expedition Lodge")], "FAIL Realm Keeper exposes character assignment"
assert "SelectedItem=\"{Binding RealmKeeper" not in xaml, "FAIL Realm Keeper exposes a world/server selector"
print("Guardian Hall Phase 3 Realm Keeper telemetry validator: PASS")
