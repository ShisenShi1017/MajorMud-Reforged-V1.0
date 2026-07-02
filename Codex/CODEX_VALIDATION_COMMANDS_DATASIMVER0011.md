# Validation Commands — DataSimVer0011

```bash
python3 Tools/Simulator/datasimver0011_action_resolution.py
python3 -m json.tool Simulator/DataSimVer/0011/DataSimVer0011Summary.json
python3 -m json.tool Simulator/DataSimVer/0011/FailureReasonCatalog.json
```

Expected results:
- `/Data` unchanged.
- `room_endpoints_promoted == 0`.
- `special_exit_semantics_promoted == 0`.
