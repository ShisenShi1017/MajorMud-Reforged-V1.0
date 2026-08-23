param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Read-Json([string]$Path) {
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$textblockPath = Join-Path $ProjectRoot 'Librarium/SourceImports/MMExplorerSQLData/TextblocksDecoded0142/textblocks_decoded.json'
$roomPartsRoot = Join-Path $ProjectRoot 'Librarium/Objects/rooms/mmexplorer_room_records_0140.parts'
$exitPartsRoot = Join-Path $ProjectRoot 'Librarium/Objects/exits/mmexplorer_exit_records_0140.parts'
$nativeMapPath = Join-Path $ProjectRoot 'ImportedDatabases/WCCDefaultInstall/wcc default/wccmp002.dat'
$reportPath = Join-Path $ProjectRoot 'Reports/RuntimeVerification/full_remoteaction_correlation_0139.json'
$contractPath = Join-Path $ProjectRoot 'Librarium/Objects/room_interactions/decoded_remote_action_records_0139.json'

# These owner chains provide exact map context where the decoded textblock owner
# is not itself a room. Spell 700 ("cleanup 3") has the native room-effect range
# 1239..1278; those room records exist only on map 12. Textblock 2528 invokes
# textblock 2529, which contains the cleanup remote-action table. Textblock 1436
# is the Phoenix quest branch rooted at NPC 503 in map 9.
$textblockMapEvidence = @{
    1436 = [pscustomobject]@{
        map_number = 9
        evidence = 'Phoenix quest textblock 1436 is reached from NPC 503 and room.map9.1423.'
    }
    2529 = [pscustomobject]@{
        map_number = 12
        evidence = 'Spell 700 cleanup 3 invokes textblock 2529; its native effect room range 1239..1278 resolves only on map 12.'
    }
}

$invocations = @(
    foreach ($node in (Read-Json $textblockPath)) {
        foreach ($operation in @($node.operations | Where-Object opcode -eq 'remoteaction')) {
            [pscustomobject]@{
                textblock_id = [int]$node.textblock_id
                owner_kind = [string]$node.owner.kind
                owner_raw = [string]$node.owner_raw
                signature = ($operation.arguments -join '.')
                action_record_id = [int]$operation.arguments[0]
                optional_message_id = [int]$operation.arguments[1]
                selector = [int]$operation.arguments[2]
                action_slot = [int]$operation.arguments[3]
            }
        }
    }
)

$rooms = @(
    foreach ($file in Get-ChildItem -LiteralPath $roomPartsRoot -Filter '*.json' | Sort-Object Name) {
        foreach ($room in (Read-Json $file.FullName).records) {
            if ($null -eq $room.placed_definition.native_record_offset) { continue }
            [pscustomobject]@{
                id = [string]$room.id
                map_number = [int]$room.map_number
                room_number = [int]$room.room_number
                native_record_offset = [long]$room.placed_definition.native_record_offset
            }
        }
    }
)
$roomsByNumber = $rooms | Group-Object room_number -AsHashTable -AsString
$roomsByMapAndNumber = @{}
foreach ($room in $rooms) { $roomsByMapAndNumber["$($room.map_number)/$($room.room_number)"] = $room }

$exitLookup = @{}
foreach ($file in Get-ChildItem -LiteralPath $exitPartsRoot -Filter '*.json' | Sort-Object Name) {
    foreach ($exit in (Read-Json $file.FullName).records) {
        $from = [string]$exit.relationships.from_room
        $to = [string]$exit.relationships.to_room
        if ([string]::IsNullOrWhiteSpace($from) -or [string]::IsNullOrWhiteSpace($to)) { continue }
        $exitLookup["$from|$to"] = [string]$exit.id
    }
}

$stream = [System.IO.File]::OpenRead($nativeMapPath)
$reader = [System.IO.BinaryReader]::new($stream)
try {
    function Read-U16([long]$Offset) {
        $stream.Position = $Offset
        [BitConverter]::ToUInt16($reader.ReadBytes(2), 0)
    }
    function Read-I16([long]$Offset) {
        $stream.Position = $Offset
        [BitConverter]::ToInt16($reader.ReadBytes(2), 0)
    }
    function Read-I32([long]$Offset) {
        $stream.Position = $Offset
        [BitConverter]::ToInt32($reader.ReadBytes(4), 0)
    }
    function Read-Action($Room, [int]$Slot) {
        $offset = [long]$Room.native_record_offset
        [pscustomobject]@{
            room_id = $Room.id
            map_number = $Room.map_number
            room_number = $Room.room_number
            action_slot = $Slot
            action_type = Read-U16 ($offset + 0x360 + ($Slot * 2))
            state = Read-I32 ($offset + 0x374 + ($Slot * 4))
            parameter = Read-I16 ($offset + 0x39C + ($Slot * 2))
            repeat = Read-I32 ($offset + 0x3B0 + ($Slot * 4))
            linked_record_id = Read-I32 ($offset + 0x338 + ($Slot * 4))
        }
    }

    $records = @(
        foreach ($signatureGroup in ($invocations | Group-Object signature | Sort-Object Name)) {
            $invocation = $signatureGroup.Group[0]
            $ownerMaps = @(
                $signatureGroup.Group.owner_raw |
                    ForEach-Object {
                        if ($_ -match '^Room\s+(\d+)/') { [int]$Matches[1] }
                    } |
                    Sort-Object -Unique
            )
            $contextEvidence = @(
                foreach ($textblockId in @($signatureGroup.Group.textblock_id | Sort-Object -Unique)) {
                    $entry = $textblockMapEvidence[[int]$textblockId]
                    if ($null -ne $entry) {
                        $ownerMaps += [int]$entry.map_number
                        [string]$entry.evidence
                    }
                }
            )
            $ownerMaps = @($ownerMaps | Sort-Object -Unique)
            $candidates = @(
                foreach ($room in @($roomsByNumber[[string]$invocation.action_record_id])) {
                    $action = Read-Action $room $invocation.action_slot
                    if ($action.action_type -ge 6 -and $action.action_type -le 24) { $action }
                }
            )
            if ($ownerMaps.Count -eq 1) {
                $ownedCandidates = @($candidates | Where-Object map_number -eq $ownerMaps[0])
                if ($ownedCandidates.Count -gt 0) { $candidates = $ownedCandidates }
            }

            $resolved = if ($candidates.Count -eq 1) { $candidates[0] } else { $null }
            $paired = $null
            $sourceExitId = ''
            $reciprocalExitId = ''
            if ($null -ne $resolved -and $resolved.action_type -in 7, 11 -and $resolved.linked_record_id -gt 0) {
                $linkedRoom = $roomsByMapAndNumber["$($resolved.map_number)/$($resolved.linked_record_id)"]
                if ($null -ne $linkedRoom) {
                    $pairedCandidates = @(
                        foreach ($slot in 0..10) {
                            $action = Read-Action $linkedRoom $slot
                            if ($action.action_type -in 7, 11 -and $action.linked_record_id -eq $resolved.room_number) { $action }
                        }
                    )
                    if ($pairedCandidates.Count -eq 1) {
                        $paired = $pairedCandidates[0]
                        $sourceExitId = [string]$exitLookup["$($resolved.room_id)|$($paired.room_id)"]
                        $reciprocalExitId = [string]$exitLookup["$($paired.room_id)|$($resolved.room_id)"]
                    }
                }
            }

            $classification = if ($null -eq $resolved) {
                if ($candidates.Count -eq 0) { 'no_native_action_candidate' } else { 'ambiguous_native_action_candidate' }
            } elseif ($resolved.action_type -in 7, 11 -and $null -ne $paired -and $sourceExitId -and $reciprocalExitId) {
                'paired_toggle_resolved'
            } elseif ($resolved.action_type -eq 6) {
                'type6_state_bit_controller'
            } elseif ($resolved.action_type -in 9, 24) {
                'room_wide_dispatch'
            } else {
                'common_finalization_no_type_specific_mutation'
            }

            [pscustomobject]@{
                signature = $signatureGroup.Name
                invocation_count = $signatureGroup.Count
                textblock_ids = @($signatureGroup.Group.textblock_id | Sort-Object -Unique)
                owner_kinds = @($signatureGroup.Group.owner_kind | Sort-Object -Unique)
                owner_raw = @($signatureGroup.Group.owner_raw | Sort-Object -Unique)
                owner_map_candidates = $ownerMaps
                context_evidence = $contextEvidence
                candidate_count = $candidates.Count
                candidates = $candidates
                resolved_action = $resolved
                paired_action = $paired
                source_exit_id = $sourceExitId
                reciprocal_exit_id = $reciprocalExitId
                classification = $classification
            }
        }
    )
}
finally {
    $reader.Dispose()
}

$summary = [ordered]@{
    invocation_count = $invocations.Count
    distinct_signature_count = $records.Count
    resolved_signature_count = @($records | Where-Object candidate_count -eq 1).Count
    unresolved_signature_count = @($records | Where-Object candidate_count -ne 1).Count
    classification_counts = [ordered]@{}
}
foreach ($group in ($records | Group-Object classification | Sort-Object Name)) {
    $summary.classification_counts[$group.Name] = $group.Count
}

$report = [ordered]@{
    authority = 'Librarium and read-only WCCMP002 correlation'
    code_update = '0139'
    generated_utc = [DateTimeOffset]::UtcNow.ToString('O')
    summary = $summary
    records = $records
}

$reportDirectory = Split-Path -Parent $reportPath
if (-not (Test-Path -LiteralPath $reportDirectory)) { New-Item -ItemType Directory -Path $reportDirectory | Out-Null }
$report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $reportPath -Encoding utf8

$contracts = @(
    foreach ($record in $records) {
        $action = $record.resolved_action
        $stateEffect = switch ($record.classification) {
            'paired_toggle_resolved' { 'toggle_paired_door_state' }
            'type6_state_bit_controller' { 'clear_native_state_bit' }
            default { 'native_common_finalization' }
        }
        [ordered]@{
            id = "interaction.decoded.remoteaction.$($record.signature)"
            object_type = 'room_interaction'
            world_scope = @('classic')
            interaction_kind = 'remote_action_mutation'
            remote_action_signature = $record.signature
            runtime_disposition = 'active_decoded_native_dispatch'
            from_map_number = $action.map_number
            from_room_number = $action.room_number
            from_room_id = $action.room_id
            source_exit_id = $record.source_exit_id
            reciprocal_exit_id = $record.reciprocal_exit_id
            native_state_key = "$($action.room_id).action.$($action.action_slot)"
            initial_native_state = $action.state
            command_semantics = [ordered]@{
                state_effect = $stateEffect
                native_action_type = $action.action_type
                selector = $record.signature.Split('.')[2] -as [int]
            }
            evidence = [ordered]@{
                correlation_report = 'Reports/RuntimeVerification/full_remoteaction_correlation_0139.json'
                native_source = 'ImportedDatabases/WCCDefaultInstall/wcc default/wccmp002.dat'
                context = $record.context_evidence
            }
            confidence = 'proven_binary_layout_and_decoded_script'
            fallback_behavior = 'fail closed when the generated native mutation contract cannot be applied'
        }
    }
)
$contract = [ordered]@{
    authority = 'Librarium'
    update = '0139'
    scope = 'complete_decoded_remoteaction_corpus'
    records = $contracts
}
$contract | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $contractPath -Encoding utf8
$summary | ConvertTo-Json -Depth 10
