from pathlib import Path
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
checks=[]
def ck(name, cond):
 checks.append((name,bool(cond))); print(('PASS' if cond else 'FAIL')+': '+name)
models=(root/'Apps/Source/Reforged.App/Guardians/GuardianModels.cs').read_text()
services=(root/'Apps/Source/Reforged.App/Guardians/GuardianServices.cs').read_text()
vm=(root/'Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs').read_text()
xaml=root/'Apps/Source/Reforged.App/Views/GuardianHallView.xaml'
ck('schema v6 or newer', any(f'SchemaVersion = {v}' in services for v in range(6, 20)) and any(f'Math.Max({v}' in services for v in range(6, 20)))
ck('persisted history','analysis-history.json' in services and 'Take(365)' in services)
ck('atomic history write','File.Move(temp, _historyPath, true)' in services)
ck('historical comparison','BuildComparison' in services and 'ComparisonStatus' in models)
ck('chart series model','AnalystChartSeries' in models and 'ChartSeries' in models)
ck('chart rendering','ProgressBar' in xaml.read_text() and 'Analyst.ChartSeries' in xaml.read_text())
ck('markdown export','Analyst-{stamp}.md' in vm)
ck('json export','Analyst-{stamp}.json' in vm)
ck('csv export','Analyst-{stamp}-series.csv' in vm)
ck('safe relative output','must remain inside GuardianHall' in vm)
ck('read only source','no character control or runtime mutation' in services)
try: ET.parse(xaml); ok=True
except Exception: ok=False
ck('xaml parse',ok)
print(f"RESULT: {sum(v for _,v in checks)}/{len(checks)}")
raise SystemExit(0 if all(v for _,v in checks) else 1)
