# Pass 27 WPF duplicate build cleanup

This package contains the canonical single definitions of:

- `Apps/Source/Reforged.App/App.xaml`
- `Apps/Source/Reforged.App/App.xaml.cs`
- `Apps/Source/Reforged.App/MainWindow.xaml`
- `Apps/Source/Reforged.App/MainWindow.xaml.cs`

The CS0102/CS0111/CS8646 duplicate-member errors occur when an existing local repository contains an extra copied App or MainWindow `.cs`/`.xaml` file, or stale WPF generated files remain under `obj`.

After extracting this cumulative ZIP over the repository, run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Fix-Pass27-WpfDuplicateBuild.ps1
```

The script:

1. Keeps the canonical App/MainWindow files.
2. Moves extra App/MainWindow source/XAML definitions into a timestamped `_Pass27_WpfDuplicateBackup_*` folder instead of deleting them.
3. Deletes only `Apps/Source/Reforged.App/bin` and `Apps/Source/Reforged.App/obj` generated output.
4. Verifies exactly one App and MainWindow source/XAML definition remains.
5. Runs `dotnet build Apps/Source/Reforged.App/Reforged.App.csproj -c Release`.

Use `-SkipBuild` to perform cleanup only.
