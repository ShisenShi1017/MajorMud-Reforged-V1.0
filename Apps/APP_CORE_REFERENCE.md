# Application Core Reference Policy

`RealmGate.Server` and `RealmGate.Server.Runtime` reference the active Core project at
`Core/Source/RealmGate.Core/RealmGate.Core.csproj`. `RealmGate.App` consumes those shared
contracts through `RealmGate.Server.Runtime`; the compiler remains an independent data
build tool.

Application code must not define duplicate versions of shared Core models such as game
records, validation results, repository contracts, logging events, or configuration
providers. Retired Milestone 1 Client, Editor, Importer, and Launcher shells are not
active product projects; their features live in `RealmGate.App` and the compiler pipeline.
