#!/usr/bin/env python3
from pathlib import Path
import json
d=json.loads(Path("Librarium/Evidence/native_support_distribution_coverage_0026.json").read_text())
print(f"WCC_HSE={d['wcc']['house_description_count']} WBB_HSE={d['wbb']['house_description_count']} WBB_ONLY={d['house_descriptions']['wbb_only_count']}")
