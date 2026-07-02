# Codex Validation Commands — Patch 0081

```bash
# Confirm no nested zip files
find . -name '*.zip' -print

# Confirm required top-level folders
test -d Data && test -d Reports && test -d Validation && test -d Evidence && test -d Codex && test -d Documentation

# Inspect validation summary
cat Validation/patch0081_validation_summary.json
```
