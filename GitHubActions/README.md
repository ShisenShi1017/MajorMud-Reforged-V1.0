# GitHub Actions Manual Repair

If GitHub still shows `MajorMud.Core` or `MajorMud.Server` paths after applying this update, replace the repository file:

`.github/workflows/dotnet-build.yml`

with the contents of:

`GitHubActions/dotnet-build.yml`

The correct workflow name is `Reforged Cloud Build` and it must build `Reforged.*` project files.
