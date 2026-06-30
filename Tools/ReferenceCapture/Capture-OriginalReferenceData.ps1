param(
    [Parameter(Mandatory = $true)]
    [string[]]$SourcePath,

    [string]$CaptureName = "",

    [string]$ProjectRoot = "",

    [int]$MinimumStringLength = 4,

    [int]$MaximumStringsPerFile = 250,

    [switch]$CopyDirectorySources
)

$ErrorActionPreference = "Stop"

function Find-ProjectRoot {
    param([string]$Start)

    $current = Resolve-Path -LiteralPath $Start
    while ($null -ne $current) {
        if ((Test-Path -LiteralPath (Join-Path $current "ProjectManifest.json")) -and
            (Test-Path -LiteralPath (Join-Path $current "Data"))) {
            return $current.Path
        }

        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }

        $current = $parent
    }

    throw "Could not locate Reforged project root from '$Start'."
}

function Get-SafeName {
    param([string]$Value)

    $name = [System.IO.Path]::GetFileNameWithoutExtension($Value)
    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = [System.IO.Path]::GetFileName($Value)
    }

    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    foreach ($char in $invalid) {
        $name = $name.Replace($char, "_")
    }

    $name = $name.Trim()
    if ([string]::IsNullOrWhiteSpace($name)) {
        return "ReferenceCapture"
    }

    return $name
}

function Get-Sha256 {
    param([string]$Path)

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Get-SevenZipPath {
    $candidates = @(
        "7z.exe",
        "7za.exe",
        "C:\Program Files\7-Zip\7z.exe",
        "C:\Program Files (x86)\7-Zip\7z.exe"
    )

    foreach ($candidate in $candidates) {
        $command = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }

        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

function Expand-ZipArchive {
    param([string]$ArchivePath, [string]$Destination)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($ArchivePath, $Destination)
}

function Expand-WithSevenZip {
    param([string]$SevenZipPath, [string]$ArchivePath, [string]$Destination)

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $arguments = @("x", "-y", "-o$Destination", $ArchivePath)
    $process = Start-Process -FilePath $SevenZipPath -ArgumentList $arguments -NoNewWindow -PassThru -Wait
    if ($process.ExitCode -ne 0) {
        throw "7-Zip extraction failed for '$ArchivePath' with exit code $($process.ExitCode)."
    }
}

function Get-CategoryHints {
    param([string]$Path)

    $name = [System.IO.Path]::GetFileNameWithoutExtension($Path).ToLowerInvariant()
    $extension = [System.IO.Path]::GetExtension($Path).ToLowerInvariant()
    $relative = $Path.Replace("\", "/").ToLowerInvariant()
    $hints = New-Object System.Collections.Generic.List[string]

    $rules = @(
        @{ Category = "rooms"; Tokens = @("room", "rooms", "wccmp", "map", "maps"); Extensions = @(".mp", ".map") },
        @{ Category = "items"; Tokens = @("item", "items", "weapon", "armor", "armour", "object"); Extensions = @(".itm", ".item", ".obj") },
        @{ Category = "monsters"; Tokens = @("monster", "monsters", "mob", "mobs", "npc", "wccknmsr"); Extensions = @(".mon", ".mob", ".npc") },
        @{ Category = "shops"; Tokens = @("shop", "shops", "store", "vendor", "wccshops"); Extensions = @(".sto", ".shop") },
        @{ Category = "spells"; Tokens = @("spell", "spells", "skill", "skills", "ability", "wccspels"); Extensions = @(".spl", ".spell") },
        @{ Category = "classes"; Tokens = @("class", "classes", "wccclass"); Extensions = @(".cls") },
        @{ Category = "races"; Tokens = @("race", "races", "wccrace"); Extensions = @(".race") },
        @{ Category = "quests"; Tokens = @("quest", "quests", "mission"); Extensions = @(".qst", ".quest") },
        @{ Category = "trainers"; Tokens = @("trainer", "trainers", "training"); Extensions = @(".trainer") },
        @{ Category = "messages"; Tokens = @("message", "messages", "msg", "text", "wcctext", "wccmsg"); Extensions = @(".msg", ".txt", ".ans") },
        @{ Category = "ganghouses"; Tokens = @("gang", "gangs", "house", "houses", "guild"); Extensions = @(".hse", ".house") },
        @{ Category = "raw-legacy-database"; Tokens = @("wcc", "wbb", "wsg", "major", "mud", "dat"); Extensions = @(".dat", ".mdb", ".db") },
        @{ Category = "configuration"; Tokens = @("config", "settings", "setup"); Extensions = @(".ini", ".cfg", ".conf") },
        @{ Category = "executable-runtime"; Tokens = @("exe", "dll", "runtime"); Extensions = @(".exe", ".dll", ".com", ".sys") }
    )

    foreach ($rule in $rules) {
        $matchedToken = $false
        foreach ($token in $rule.Tokens) {
            if ($name.Contains($token) -or $relative.Contains("/$token/")) {
                $matchedToken = $true
                break
            }
        }

        if ($matchedToken -or ($rule.Extensions -contains $extension)) {
            $hints.Add($rule.Category)
        }
    }

    if ($hints.Count -eq 0) {
        $hints.Add("unknown")
    }

    return ($hints | Sort-Object -Unique) -join ";"
}

function Get-PrintableStrings {
    param([string]$Path, [int]$MinimumLength, [int]$MaximumCount)

    $strings = New-Object System.Collections.Generic.List[string]
    $builder = New-Object System.Text.StringBuilder
    $bytes = [System.IO.File]::ReadAllBytes($Path)

    foreach ($byte in $bytes) {
        if ($byte -ge 32 -and $byte -le 126) {
            [void]$builder.Append([char]$byte)
        } else {
            if ($builder.Length -ge $MinimumLength) {
                $value = $builder.ToString().Trim()
                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    $strings.Add($value)
                    if ($strings.Count -ge $MaximumCount) {
                        return $strings
                    }
                }
            }

            [void]$builder.Clear()
        }
    }

    if ($builder.Length -ge $MinimumLength -and $strings.Count -lt $MaximumCount) {
        $value = $builder.ToString().Trim()
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $strings.Add($value)
        }
    }

    return $strings
}

function Get-RelativePath {
    param([string]$Root, [string]$Path)

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\') + "\"
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if ($pathFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $pathFull.Substring($rootFull.Length)
    }

    return $pathFull
}

function Add-Count {
    param(
        [hashtable]$Table,
        [string]$Key
    )

    if ($Table.ContainsKey($Key)) {
        $Table[$Key] = [int]$Table[$Key] + 1
    } else {
        $Table[$Key] = 1
    }
}

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Find-ProjectRoot -Start (Get-Location).Path
} else {
    $ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
}

if ([string]::IsNullOrWhiteSpace($CaptureName)) {
    $CaptureName = "Capture-" + (Get-Date -Format "yyyyMMdd-HHmmss")
}

$safeCaptureName = Get-SafeName -Value $CaptureName
$captureRoot = Join-Path $ProjectRoot "ImportedDatabases\ReferenceCaptures\$safeCaptureName"
$sourceArchiveRoot = Join-Path $captureRoot "SourceArchives"
$sourceDirectoryRoot = Join-Path $captureRoot "SourceDirectories"
$extractedRoot = Join-Path $captureRoot "Extracted"
$reportRoot = Join-Path $ProjectRoot "Reports\ReferenceCaptures\$safeCaptureName"
$stringRoot = Join-Path $reportRoot "Strings"

foreach ($path in @($captureRoot, $sourceArchiveRoot, $sourceDirectoryRoot, $extractedRoot, $reportRoot, $stringRoot)) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

$sevenZip = Get-SevenZipPath
$sourceSummaries = New-Object System.Collections.Generic.List[object]
$scanRoots = New-Object System.Collections.Generic.List[string]
$unsupportedArchives = New-Object System.Collections.Generic.List[object]

foreach ($source in $SourcePath) {
    if (!(Test-Path -LiteralPath $source)) {
        throw "Source path was not found: $source"
    }

    $resolved = (Resolve-Path -LiteralPath $source).Path
    $item = Get-Item -LiteralPath $resolved
    $safeName = Get-SafeName -Value $item.Name

    if ($item.PSIsContainer) {
        $scanRoots.Add($item.FullName)
        $sourceSummaries.Add([pscustomobject]@{
            SourcePath = $item.FullName
            Type = "directory"
            Copied = [bool]$CopyDirectorySources
            Extracted = $false
            Note = "Directory scanned in place. Use -CopyDirectorySources to preserve a copy under the capture folder."
        })

        if ($CopyDirectorySources) {
            $target = Join-Path $sourceDirectoryRoot $safeName
            Copy-Item -LiteralPath $item.FullName -Destination $target -Recurse -Force
        }

        continue
    }

    $extension = $item.Extension.ToLowerInvariant()
    $archiveCopy = Join-Path $sourceArchiveRoot $item.Name
    Copy-Item -LiteralPath $item.FullName -Destination $archiveCopy -Force

    $sourceSummaries.Add([pscustomobject]@{
        SourcePath = $item.FullName
        Type = if ($extension -in @(".zip", ".rar", ".7z")) { "archive" } else { "file" }
        Copied = $true
        Extracted = $false
        Note = "Source file preserved under SourceArchives."
    })

    if ($extension -eq ".zip") {
        $destination = Join-Path $extractedRoot $safeName
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        Expand-ZipArchive -ArchivePath $archiveCopy -Destination $destination
        $scanRoots.Add($destination)
        $sourceSummaries[$sourceSummaries.Count - 1].Extracted = $true
        $sourceSummaries[$sourceSummaries.Count - 1].Note = "ZIP extracted safely without executing contents."
    } elseif ($extension -in @(".rar", ".7z")) {
        if ($sevenZip) {
            $destination = Join-Path $extractedRoot $safeName
            Expand-WithSevenZip -SevenZipPath $sevenZip -ArchivePath $archiveCopy -Destination $destination
            $scanRoots.Add($destination)
            $sourceSummaries[$sourceSummaries.Count - 1].Extracted = $true
            $sourceSummaries[$sourceSummaries.Count - 1].Note = "Archive extracted with 7-Zip without executing contents."
        } else {
            $unsupportedArchives.Add([pscustomobject]@{
                Archive = $archiveCopy
                Reason = "7-Zip was not found. Install 7-Zip or manually extract this archive into a folder and rerun the capture tool on that folder."
            })
        }
    } else {
        $scanRoots.Add($archiveCopy)
    }
}

$fileRows = New-Object System.Collections.Generic.List[object]
$stringRows = New-Object System.Collections.Generic.List[object]
$extensionCounts = @{}
$categoryCounts = @{}

foreach ($root in $scanRoots) {
    $files = @()
    if (Test-Path -LiteralPath $root -PathType Leaf) {
        $files = @(Get-Item -LiteralPath $root)
        $rootForRelative = Split-Path -Parent $root
    } else {
        $files = @(Get-ChildItem -LiteralPath $root -Recurse -File -Force)
        $rootForRelative = $root
    }

    foreach ($file in $files) {
        $relative = Get-RelativePath -Root $rootForRelative -Path $file.FullName
        $extension = if ([string]::IsNullOrWhiteSpace($file.Extension)) { "<none>" } else { $file.Extension.ToLowerInvariant() }
        $categoryHints = Get-CategoryHints -Path $file.FullName
        $hash = Get-Sha256 -Path $file.FullName
        $stringFileName = ($hash + ".strings.txt")
        $stringPath = Join-Path $stringRoot $stringFileName
        $strings = Get-PrintableStrings -Path $file.FullName -MinimumLength $MinimumStringLength -MaximumCount $MaximumStringsPerFile
        if ($strings.Count -gt 0) {
            Set-Content -LiteralPath $stringPath -Value $strings -Encoding UTF8
        }

        $fileRows.Add([pscustomobject]@{
            SourceRoot = $root
            RelativePath = $relative
            FullPath = $file.FullName
            FileName = $file.Name
            Extension = $extension
            SizeBytes = $file.Length
            LastWriteTimeUtc = $file.LastWriteTimeUtc.ToString("o")
            Sha256 = $hash
            CategoryHints = $categoryHints
            StringCount = $strings.Count
            StringsReport = if ($strings.Count -gt 0) { "Strings/$stringFileName" } else { "" }
        })

        Add-Count -Table $extensionCounts -Key $extension
        foreach ($hint in $categoryHints.Split(";")) {
            Add-Count -Table $categoryCounts -Key $hint
        }

        foreach ($value in $strings) {
            $stringRows.Add([pscustomobject]@{
                Sha256 = $hash
                RelativePath = $relative
                CategoryHints = $categoryHints
                Value = $value
            })
        }
    }
}

$fileInventoryPath = Join-Path $reportRoot "FileInventory.csv"
$stringInventoryPath = Join-Path $reportRoot "StringInventory.csv"
$manifestPath = Join-Path $reportRoot "ReferenceCaptureManifest.json"
$summaryPath = Join-Path $reportRoot "ReferenceCaptureSummary.md"

$fileRows | Sort-Object RelativePath | Export-Csv -LiteralPath $fileInventoryPath -NoTypeInformation -Encoding UTF8
$stringRows | Export-Csv -LiteralPath $stringInventoryPath -NoTypeInformation -Encoding UTF8

$manifest = [pscustomobject]@{
    captureName = $safeCaptureName
    createdUtc = (Get-Date).ToUniversalTime().ToString("o")
    projectRoot = $ProjectRoot
    captureRoot = $captureRoot
    reportRoot = $reportRoot
    sourceSummaries = $sourceSummaries
    sourceCount = $SourcePath.Count
    fileCount = $fileRows.Count
    stringCount = $stringRows.Count
    minimumStringLength = $MinimumStringLength
    maximumStringsPerFile = $MaximumStringsPerFile
    sevenZipAvailable = [bool]$sevenZip
    unsupportedArchives = $unsupportedArchives
    extensionCounts = $extensionCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { [pscustomobject]@{ extension = $_.Name; count = $_.Value } }
    categoryCounts = $categoryCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { [pscustomobject]@{ category = $_.Name; count = $_.Value } }
    policy = "Reference-only capture. Does not execute original programs, does not use keygens, and does not modify Reforged Data."
}

$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Reference Capture Summary")
$lines.Add("")
$lines.Add("- Capture: " + $safeCaptureName)
$lines.Add("- Created UTC: " + $manifest.createdUtc)
$lines.Add("- Files inventoried: " + $fileRows.Count)
$lines.Add("- Printable strings captured: " + $stringRows.Count)
$lines.Add("- 7-Zip available: " + [bool]$sevenZip)
$lines.Add("- Policy: reference-only; no original executable was run; no keygen was used; no Reforged Data file was modified.")
$lines.Add("")
$lines.Add("## Source Inputs")
$lines.Add("")
foreach ($sourceSummary in $sourceSummaries) {
    $lines.Add("- " + $sourceSummary.SourcePath + " - " + $sourceSummary.Type + "; copied=" + $sourceSummary.Copied + "; extracted=" + $sourceSummary.Extracted + "; " + $sourceSummary.Note)
}

if ($unsupportedArchives.Count -gt 0) {
    $lines.Add("")
    $lines.Add("## Unsupported Archives")
    $lines.Add("")
    foreach ($archive in $unsupportedArchives) {
        $lines.Add("- " + $archive.Archive + ": " + $archive.Reason)
    }
}

$lines.Add("")
$lines.Add("## Category Counts")
$lines.Add("")
foreach ($entry in ($manifest.categoryCounts | Sort-Object category)) {
    $lines.Add("- " + $entry.category + ": " + $entry.count)
}

$lines.Add("")
$lines.Add("## Report Files")
$lines.Add("")
$lines.Add("- FileInventory.csv")
$lines.Add("- StringInventory.csv")
$lines.Add("- ReferenceCaptureManifest.json")
$lines.Add("- Strings/")

Set-Content -LiteralPath $summaryPath -Value $lines -Encoding UTF8

Write-Host "Reference capture complete."
Write-Host "Capture root: $captureRoot"
Write-Host "Report root: $reportRoot"
Write-Host "Files inventoried: $($fileRows.Count)"
Write-Host "Strings captured: $($stringRows.Count)"
if ($unsupportedArchives.Count -gt 0) {
    Write-Warning "Some archives were not extracted. See $summaryPath"
}
