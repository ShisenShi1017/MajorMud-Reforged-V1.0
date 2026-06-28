param(
    [string]$NativeRuntimePath = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

function Get-Sha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

if ([string]::IsNullOrWhiteSpace($NativeRuntimePath)) {
    $NativeRuntimePath = Join-Path (Get-Location) "ImportedDatabases\NativeRuntime"
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Get-Location) "Data\NativeRuntimeCatalog"
}

if (!(Test-Path $NativeRuntimePath)) {
    throw "Native runtime path not found: $NativeRuntimePath"
}
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

$files = Get-ChildItem -Path $NativeRuntimePath -Filter *.DAT | Sort-Object Name
$catalog = @()
foreach ($file in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $first = ($bytes | Select-Object -First 16 | ForEach-Object { $_.ToString("X2") }) -join " "
    $catalog += [PSCustomObject]@{
        file = $file.Name
        size_bytes = $file.Length
        sha256 = Get-Sha256 $file.FullName
        first_16_hex = $first
        status = "archived_native_source_not_decoded_yet"
    }
}

$result = [PSCustomObject]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("s") + "Z"
    native_runtime_path = $NativeRuntimePath
    source_file_count = $catalog.Count
    source_files = $catalog
}

$result | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 (Join-Path $OutputPath "SourceFileCatalog.generated.json")
Write-Host "Catalog written to $OutputPath\SourceFileCatalog.generated.json"
