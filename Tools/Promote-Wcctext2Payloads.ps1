param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$sourcePath = Join-Path $ProjectRoot 'ImportedDatabases/WCCDefaultInstall/wcc default/wcctext2.dat'
$outputPath = Join-Path $ProjectRoot 'Librarium/Objects/native_textblock_payloads/native_textblock_payload_records.json'
$pageSize = 0x800
$recordMarker = 0x5600
$payloadOffset = 0x10

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Required WCC default-install evidence is missing: $sourcePath"
}

$raw = [IO.File]::ReadAllBytes($sourcePath)
if (($raw.Length % $pageSize) -ne 0) {
    throw 'WCCTEXT2.DAT size is not page aligned.'
}

$hasher = [Security.Cryptography.SHA256]::Create()
try {
    $sha = ([BitConverter]::ToString($hasher.ComputeHash($raw)) -replace '-', '').ToLowerInvariant()
}
finally {
    $hasher.Dispose()
}
$records = [Collections.Generic.List[object]]::new()
$seen = [Collections.Generic.HashSet[int]]::new()
$encoding = [Text.Encoding]::GetEncoding(437)

for ($offset = 0; $offset -lt $raw.Length; $offset += $pageSize) {
    $marker = [BitConverter]::ToUInt16($raw, $offset)
    if ($marker -ne $recordMarker) {
        continue
    }

    $textblockId = [BitConverter]::ToUInt16($raw, $offset + 2)
    if ($textblockId -le 0 -or -not $seen.Add($textblockId)) {
        throw "Invalid or duplicate WCCTEXT2 textblock ID $textblockId."
    }

    $decoded = [byte[]]::new($pageSize - $payloadOffset)
    for ($index = 0; $index -lt $decoded.Length; $index++) {
        $decoded[$index] = [byte](($raw[$offset + $payloadOffset + $index] + 0xE0) -band 0xFF)
    }
    $decodedLength = $decoded.Length
    while ($decodedLength -gt 0 -and $decoded[$decodedLength - 1] -eq 0) {
        $decodedLength--
    }

    $records.Add([ordered]@{
        id = "native_textblock_payload.$textblockId"
        object_type = 'native_textblock_payload'
        world_scope = @('classic')
        textblock_id = $textblockId
        payload = $encoding.GetString($decoded, 0, $decodedLength)
        page_offset = $offset
        page_index = [int]($offset / $pageSize)
        record_marker = '0x5600'
        payload_offset = $payloadOffset
        decode = 'byte_plus_0xE0_mod_256'
        evidence_state = 'verified_native_runtime'
        classic_lock = 'evidence_backed_immutable'
    })
}

$orderedRecords = @($records | Sort-Object textblock_id)
if ($orderedRecords.Count -ne 3467) {
    throw "Expected 3467 WCCTEXT2 payload records, found $($orderedRecords.Count)."
}

$document = [ordered]@{
    schema = 'majormud.librarium.native-textblock-payloads.v1'
    source = [ordered]@{
        file = 'ImportedDatabases/WCCDefaultInstall/wcc default/wcctext2.dat'
        size = $raw.Length
        sha256 = $sha
        page_size = $pageSize
    }
    record_count = $orderedRecords.Count
    min_textblock_id = $orderedRecords[0].textblock_id
    max_textblock_id = $orderedRecords[-1].textblock_id
    records = $orderedRecords
}

$outputDirectory = Split-Path -Parent $outputPath
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
[IO.File]::WriteAllText(
    $outputPath,
    (($document | ConvertTo-Json -Depth 8) + [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))

Write-Host "Promoted $($orderedRecords.Count) verified WCCTEXT2 payloads to $outputPath"
