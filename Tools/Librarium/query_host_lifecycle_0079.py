#!/usr/bin/env python3
import json, sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
model=json.loads((root/'Librarium/HostLifecycleVerification/host_lifecycle_model_0079.json').read_text())
evidence=json.loads((root/'Evidence/HostLifecycle0079/host_lifecycle_evidence_sources_0079.json').read_text())
gaps=json.loads((root/'Librarium/GapRegistry/host_lifecycle_gaps_0079.json').read_text())
q=' '.join(sys.argv[1:]).lower()
if not q or q in ('summary','status'):
    print(json.dumps({'status':model['status'],'timing_findings':model['timing_findings'],'gaps':len(gaps),'evidence_entries':len(evidence)}, indent=2))
elif q=='order':
    print(json.dumps(model['host_lifecycle_order'], indent=2))
elif q=='gaps':
    print(json.dumps(gaps, indent=2))
else:
    hits=[e for e in evidence if q in e['title'].lower() or q in e['finding'].lower() or q in e['file'].lower()]
    print(json.dumps(hits, indent=2))
