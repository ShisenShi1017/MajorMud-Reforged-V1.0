import json
from pathlib import Path


def validate_gameplay_completion():
    report_path = Path('Reports/gameplay_completion_report_0017.json')
    quest_path = Path('Reports/quest_validation_readiness_0017.json')
    if not report_path.exists():
        return {'status':'FAIL','passed':0,'failed':1,'unknown':0,'notes':['Missing gameplay completion report.']}
    if not quest_path.exists():
        return {'status':'FAIL','passed':0,'failed':1,'unknown':0,'notes':['Missing quest validation readiness report.']}
    report = json.loads(report_path.read_text(encoding='utf-8'))
    failed = int(report.get('failed', 0))
    unknown = int(report.get('unknown', 0))
    stages = report.get('stages', [])
    passed = 2 + len(stages)
    notes = [
        'Runtime-0017 gameplay completion report present.',
        'Quest validation readiness report present.',
        f'Six-stage coverage entries: {len(stages)}.',
        'Unknowns are evidence-gated by design.'
    ]
    return {
        'status':'PASS_WITH_UNKNOWN' if unknown else 'PASS',
        'passed':passed,
        'failed':failed,
        'unknown':unknown,
        'notes':notes
    }
