[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$SigningThumbprint = $env:REALMGATE_SIGNING_THUMBPRINT,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [switch]$RequireTrustedSignature
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$project = Join-Path $repoRoot "Apps\Source\RealmGate.App\RealmGate.App.csproj"
$publishDirectory = Join-Path $repoRoot "Release\RealmGate"

if ($RequireTrustedSignature -and [string]::IsNullOrWhiteSpace($SigningThumbprint)) {
    throw "A trusted signature is required. Set REALMGATE_SIGNING_THUMBPRINT or pass -SigningThumbprint."
}

function Find-SignTool {
    $fromPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $fromPath) {
        return $fromPath.Source
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path -LiteralPath $kitsRoot)) {
        return $null
    }

    return Get-ChildItem -LiteralPath $kitsRoot -Filter signtool.exe -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

function Resolve-SigningCertificate {
    param([Parameter(Mandatory)][string]$Thumbprint)

    $normalized = ($Thumbprint -replace "\s", "").ToUpperInvariant()
    foreach ($candidate in @(
        @{ Store = "CurrentUser"; Path = "Cert:\CurrentUser\My\$normalized" },
        @{ Store = "LocalMachine"; Path = "Cert:\LocalMachine\My\$normalized" }
    )) {
        if (Test-Path -LiteralPath $candidate.Path) {
            return [pscustomobject]@{
                Thumbprint = $normalized
                Store = $candidate.Store
                Certificate = Get-Item -LiteralPath $candidate.Path
            }
        }
    }

    throw "The signing certificate '$normalized' was not found in CurrentUser\\My or LocalMachine\\My."
}

Write-Host "Publishing RealmGate ($Configuration, $RuntimeIdentifier, framework-dependent)..."
$publishArguments = @(
    "publish",
    $project,
    "--configuration", $Configuration,
    "--runtime", $RuntimeIdentifier,
    "--self-contained", "false",
    "-p:NuGetAudit=false",
    "-p:PublishSingleFile=false",
    "-p:PublicRelease=false",
    "-p:PublishDir=$publishDirectory\"
)
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "RealmGate publish failed with exit code $LASTEXITCODE."
}

$executable = Join-Path $publishDirectory "RealmGate.exe"
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Published RealmGate.exe was not found at '$executable'."
}

if ([string]::IsNullOrWhiteSpace($SigningThumbprint)) {
    Write-Warning "RealmGate was published unsigned. Windows SmartScreen may warn until a trusted publisher signature is applied."
    Write-Host "Published: $executable"
    return
}

$signTool = Find-SignTool
if ([string]::IsNullOrWhiteSpace($signTool)) {
    throw "SignTool was not found. Install the Windows SDK Signing Tools component."
}

$certificate = Resolve-SigningCertificate -Thumbprint $SigningThumbprint
if (-not $certificate.Certificate.HasPrivateKey) {
    throw "The selected signing certificate does not expose a private key."
}
if ($certificate.Certificate.NotAfter -le (Get-Date)) {
    throw "The selected signing certificate expired on $($certificate.Certificate.NotAfter.ToString('u'))."
}

$signingTargets = @(
    Get-ChildItem -LiteralPath $publishDirectory -Filter "RealmGate*.dll" -File
    Get-Item -LiteralPath $executable
) | Sort-Object { $_.Extension -eq ".exe" }, Name

foreach ($target in $signingTargets) {
    $arguments = @(
        "sign",
        "/sha1", $certificate.Thumbprint,
        "/fd", "SHA256",
        "/tr", $TimestampUrl,
        "/td", "SHA256"
    )
    if ($certificate.Store -eq "LocalMachine") {
        $arguments += "/sm"
    }
    $arguments += $target.FullName

    Write-Host "Signing $($target.Name)..."
    & $signTool @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool failed for '$($target.FullName)' with exit code $LASTEXITCODE."
    }
}

foreach ($target in $signingTargets) {
    & $signTool verify /pa /all $target.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "Signature verification failed for '$($target.FullName)' with exit code $LASTEXITCODE."
    }
}

Write-Host "Published and signature-verified: $executable"
