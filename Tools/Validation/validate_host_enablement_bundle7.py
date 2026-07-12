from pathlib import Path
root=Path(__file__).resolve().parents[2]
required=[
'Config/server.host.json','Config/server.worlds.json',
'Apps/Source/Reforged.Server/RuntimeHostEnablementSmokeReport.cs',
'Apps/Source/Reforged.Server/ServerRuntimeHost.cs',
'Apps/Source/Reforged.Server/ServerNetworkHost.cs',
'Apps/Source/Reforged.Server/ServerTickScheduler.cs']
missing=[p for p in required if not (root/p).exists()]
if missing: raise SystemExit('FAIL: missing '+', '.join(missing))
text=(root/'Config/server.host.json').read_text()
for token in ('"gameplayEnabled": true','"networkingEnabled": true','"worldSimulationEnabled": true'):
    if token not in text: raise SystemExit('FAIL: host setting missing '+token)
world=(root/'Config/server.worlds.json').read_text()
if '"enabled": true' not in world: raise SystemExit('FAIL: network runtime is not enabled')
print('PASS: Bundle 7 host, networking, scheduler, world simulation, and smoke contracts are present.')
