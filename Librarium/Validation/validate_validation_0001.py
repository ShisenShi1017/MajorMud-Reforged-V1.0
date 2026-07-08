#!/usr/bin/env python3
import json, sys, pathlib, zipfile
root = pathlib.Path(__file__).resolve().parents[2]
report = root/'Librarium'/'Reports'/'validation_0001_complete_knowledge_verification_report.json'
if not report.exists():
    print('FAIL: missing validation report')
    sys.exit(1)
data=json.loads(report.read_text(encoding='utf-8'))
required=['files_scanned','knowledge_assertions','entities_extracted','integrity','canonical_counts']
missing=[k for k in required if k not in data]
if missing:
    print('FAIL: report missing keys', missing)
    sys.exit(1)
if data['files_scanned'] <= 0 or data['knowledge_assertions'] <= 0:
    print('FAIL: no files/assertions scanned')
    sys.exit(1)
print('PASS: Validation-0001 report present')
print(json.dumps({'status':data['status'],'files_scanned':data['files_scanned'],'entities':data['entities_extracted'],'assertions':data['knowledge_assertions'],'promotion_candidates':data['promotion_candidates'],'integrity':data['integrity']}, indent=2))
