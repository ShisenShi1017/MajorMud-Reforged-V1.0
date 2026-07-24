#!/usr/bin/env python3
from pathlib import Path
import json
d=json.loads(Path("Librarium/Evidence/native_support_distribution_coverage_0026.json").read_text())
h=d['house_descriptions']
assert d['wcc']['house_description_count']==47
assert d['wbb']['house_description_count']==133
assert h['shared_count']==47 and h['wbb_only_count']==86 and h['wcc_only_count']==0
assert h['shared_byte_identical_count']==47
assert d['gravestones']['shared_count']==d['gravestones']['shared_byte_identical_count']==7
assert len(d['binary_screens'])==3 and all(x['byte_identical'] for x in d['binary_screens'])
assert d['runtime_policy']['cross_distribution_substitution']=='forbidden'
print('native support distribution coverage: PASS')
