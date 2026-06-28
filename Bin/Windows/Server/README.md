# Server Runtime Folder

Future compiled Windows runtime artifacts for the Server app belong here.

Expected future executable:

- `MajorMud.Server.exe`

No executable is required for Milestone 1 source-framework completion. Build verification should be performed by GitHub Actions using `.github/workflows/dotnet-build.yml`, then future release packaging can place compiled artifacts here so normal users do not need the .NET SDK locally.
