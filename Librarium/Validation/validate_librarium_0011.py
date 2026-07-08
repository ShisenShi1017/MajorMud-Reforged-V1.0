import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
ents=json.loads((root/'Librarium'/'Assertions'/'knowledge_entities_0011.json').read_text())
ass=json.loads((root/'Librarium'/'Assertions'/'knowledge_assertions_0011.json').read_text())
assert ents and ass
print(json.dumps({'status':'PASS','entities':len(ents),'assertions':len(ass)},indent=2))
