# Data Generation Policy - CodeUpdate 0116

`/Data` is generated. Do not manually edit it as source.

To regenerate Classic runtime data:

```bash
python Tools/Librarium/export_librarium_to_data.py --world classic --clean
```

The generated folder may be used locally for runtime tests, but the authoritative content remains in the Librarium.
