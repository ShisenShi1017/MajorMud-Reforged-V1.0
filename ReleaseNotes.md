# CodeUpdate-0023 - Fresh GitHub Actions Workflow with BAT Installer

This update repeats the fresh GitHub Actions workflow setup and adds the missing Windows batch installer.

## Added

- `.github/workflows/dotnet-build.yml`
- `GitHubActions/dotnet-build.yml`
- `Install-GitHub-Actions-Workflow.bat`

## How to use

1. Extract this update over your repository.
2. Run `Install-GitHub-Actions-Workflow.bat` from the repository root.
3. Commit and push to GitHub.
4. Open the GitHub Actions tab and check `Reforged Cloud Build`.

## Notes

- The workflow uses only `Reforged.*` project paths.
- The Node.js warning from GitHub-hosted actions is not treated as the project build failure.
- No local .NET SDK is required for normal runtime use.
