# Pass 34 Validation — Classic Worldgroup PRNG

- Focused Pass 34 static/evidence/runtime-contract validation: **106/106 PASS**.
- Retained Pass regressions: Pass 15 **60/60**, Pass 24 **53/53**, Pass 26 **70/70**, Pass 27 **68/68**, Pass 28 **51/51**, Pass 29 **78/78**, Pass 30 **56/56**, Pass 31 **57/57**, Pass 32 **109/109**, Pass 33 **93/93**.
- Exact Borland/CW3220MT vectors are checked independently from the C# implementation.
- The proposed MSVC `214013/2531011` LCG vector is explicitly rejected.
- Startup packed-time seed vectors and two-second resolution are checked.
- `_genrdn`/`_lngrnd` exclusive-maximum/shared-host boundary is retained without inventing the unresolved high-level pull-order mapping.
- Prior StrengthAlternative, `enabled` shadowing, WPF App/MainWindow, Borin, and Custom1 boundaries remain protected.
- A .NET compile must be performed when an SDK is available; static validation is not represented as compilation.
