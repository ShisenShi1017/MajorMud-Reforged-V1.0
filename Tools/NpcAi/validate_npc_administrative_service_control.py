#!/usr/bin/env python3
from pathlib import Path
import json,sys
r=Path(sys.argv[1] if len(sys.argv)>1 else ".")
for p in [r/"Evidence/NpcAI/npc_ai_administrative_service_control_evidence.json",r/"Evidence/NpcAI/npc_ai_administrative_service_control_provenance.json"]: json.loads(p.read_text())
s=(r/"Apps/Source/Reforged.Server/RuntimeNpcAdministrativeServiceControl.cs").read_text()
for token in ["CancelNpcTransientInteraction","authority-required","reason-required","no-active-transient-state","npc_admin_interaction_last_cleared_keys"]:
 assert token in s, token
print("npc administrative service control validation passed")
