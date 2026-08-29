param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$sourceRoots = @(
    'ImportedDatabases/WCCDefaultInstall',
    'ImportedDatabases/NativeRuntime',
    'ImportedDatabases/MegaMudMP',
    'ImportedDatabases/OmniVerseReference',
    'DataDecodeCheckpoints'
)
$runtimeRoot = Join-Path $ProjectRoot 'Data/Classic'
$records = [Collections.Generic.List[object]]::new()

function Get-Role([IO.FileInfo]$file) {
    $name = $file.Name.ToUpperInvariant()
    $extension = $file.Extension.ToUpperInvariant()
    if ($name -match 'MMPLS') { return 'majormud_plus' }
    if ($name -match 'TWINK') { return 'code_of_twinks' }
    if ($name -match 'SYSOP') { return 'sysop_board_host' }
    if ($extension -in '.DAT','.NU1') { return 'native_data' }
    if ($extension -in '.DLL','.EXE') { return 'native_executable' }
    if ($extension -in '.MCV','.MSG','.H','.MDF') { return 'module_contract_presentation' }
    if ($extension -in '.HSE','.STO') { return 'board_house_state' }
    if ($extension -eq '.SYS') { return 'addon_host_configuration' }
    if ($extension -in '.BIN','.ANS','.UIC','.BAN','.EMB') { return 'board_ui_presentation' }
    if ($extension -in '.REL','.RLN','.NOT','.DOC','.LIC','.AD','.STA','.TXT','.LOG') { return 'documentation_configuration_release' }
    return 'unclassified'
}

foreach ($relativeRoot in $sourceRoots) {
    $absoluteRoot = Join-Path $ProjectRoot $relativeRoot
    if (-not (Test-Path -LiteralPath $absoluteRoot)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File | Sort-Object FullName) {
        $relative = $file.FullName.Substring($ProjectRoot.Length + 1).Replace('\','/')
        $role = if ($relativeRoot -eq 'ImportedDatabases/MegaMudMP') {
            'megamud_client_automation_reference'
        } elseif ($relativeRoot -eq 'ImportedDatabases/OmniVerseReference') {
            'omniverse_public_reference'
        } elseif ($relativeRoot -eq 'DataDecodeCheckpoints') {
            'decoded_checkpoint_evidence'
        } else {
            Get-Role $file
        }
        $records.Add([pscustomobject][ordered]@{
            path = $relative
            file_name = $file.Name
            extension = $file.Extension.ToLowerInvariant()
            bytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            source_role = $role
            retirement_state = 'requires_field_and_consumer_proof'
        })
    }
}

$runtimeFiles = @()
if (Test-Path -LiteralPath $runtimeRoot) {
    $runtimeFiles = @(Get-ChildItem -LiteralPath $runtimeRoot -File | Sort-Object Name | ForEach-Object {
        [pscustomobject][ordered]@{ name=$_.Name; bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
}

$roles = @($records | Group-Object source_role | Sort-Object Name | ForEach-Object {
    [ordered]@{ role=$_.Name; file_count=$_.Count; bytes=($_.Group | Measure-Object bytes -Sum).Sum }
})
$twinksEvidence = @(
    'Evidence/DataSimVer0013-UserRequestedCustomContent.md',
    'Reports/DataDecode/decoded_data_evidence_summary.md',
    'Simulator/DataSimVer/0013/custom_content_registration.json'
)
$decodeAuditPath = Join-Path $ProjectRoot 'Reports/RuntimeVerification/classic_decode_exhaustion_report.json'
$decodeAudit = $null
if (Test-Path -LiteralPath $decodeAuditPath) {
    $rawDecodeAudit = Get-Content -LiteralPath $decodeAuditPath -Raw | ConvertFrom-Json
    $decodeAudit = [ordered]@{
        generated_utc = $rawDecodeAudit.GeneratedAtUtc
        family_count = $rawDecodeAudit.FamilyCount
        generated_record_count = $rawDecodeAudit.TotalClassicRecords
        scalar_field_count = $rawDecodeAudit.TotalFields
        disconnected_field_count = $rawDecodeAudit.DisconnectedFieldCount
        pending_family_count = $rawDecodeAudit.PendingFamilyCount
        disconnected_family_count = $rawDecodeAudit.DisconnectedFamilyCount
        status_counts = @($rawDecodeAudit.Fields | Group-Object Status | Sort-Object Name | ForEach-Object {
            [ordered]@{ status=$_.Name; count=$_.Count }
        })
        generated_unconsumed = @($rawDecodeAudit.Fields | Where-Object Status -eq 'generated-unconsumed' | ForEach-Object {
            [ordered]@{ family=$_.Family; json_path=$_.JsonPath; occurrences=$_.OccurrenceCount }
        })
        report_path = 'Reports/RuntimeVerification/classic_decode_exhaustion_report.json'
    }
}

$document = [ordered]@{
    schema = 'majormud.classic-parity-retirement-ledger.v2'
    generated_utc = [DateTime]::UtcNow.ToString('O')
    completion_standard = 'Every valid decoded field must trace through Librarium, generated runtime data, and an executable gameplay, sysop, builder, or board-host consumer.'
    source_file_count = $records.Count
    source_bytes = ($records | Measure-Object bytes -Sum).Sum
    source_roles = $roles
    source_files = @($records)
    generated_runtime = [ordered]@{
        file_count = $runtimeFiles.Count
        bytes = ($runtimeFiles | Measure-Object bytes -Sum).Sum
        files = $runtimeFiles
        consolidation_state = 'not_compact_enough'
    }
    generated_decode_consumption_audit = $decodeAudit
    wccmmud_sysop_unlock = [ordered]@{
        configured_setting_name = 'CODEC_OF_TWINKS'
        configured_setting_value = 78
        user_confirmed_semantics = 'Observed WCCMMUD configuration setting; exact native numeric semantics remain evidence-bounded'
        request_registration_files = @($twinksEvidence | Where-Object { Test-Path -LiteralPath (Join-Path $ProjectRoot $_) })
        native_contract_files = @(
            'ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMUD.msg',
            'ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMUD.RLN',
            'ImportedDatabases/WCCDefaultInstall/wcc default/WCCMMSYS.NOT'
        )
        runtime_authority = 'authenticated_account_role_and_explicit_capabilities'
        plaintext_key_runtime_command_allowed = $false
        state = 'correctly_classified_sysop_command_inventory_and_capability_mapping_required'
    }
    majormud_plus = [ordered]@{
        native_source_files = @($records | Where-Object source_role -eq 'majormud_plus' | Select-Object -ExpandProperty path)
        native_source_present = @($records | Where-Object source_role -eq 'majormud_plus').Count -gt 0
        decoded_feature_scope = @(
            'character_descriptions',
            'character_registries',
            'purchase_additional_life',
            'offline_action_editor',
            'global_mudreg_lookup',
            'sysop_description_approval_and_management'
        )
        state = 'online_runtime_connected_action_editor_connected_contest_rune_placement_evidence_pending'
    }
    retirement_gate = [ordered]@{
        imported_databases_removable = $false
        decoded_sources_removable = $false
        unexplained_valid_records_required = 0
        decoded_unconsumed_fields_required = 0
        board_host_and_sysop_parity_required = $true
        majormud_plus_parity_required = $true
        wccmmud_sysop_command_parity_required = $true
        external_immutable_evidence_archive_required = $true
    }
}

$reportDirectory = Join-Path $ProjectRoot 'Reports/RuntimeVerification'
[IO.Directory]::CreateDirectory($reportDirectory) | Out-Null
$jsonPath = Join-Path $reportDirectory 'classic_parity_retirement_ledger.json'
[IO.File]::WriteAllText($jsonPath, ($document | ConvertTo-Json -Depth 8) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

$markdown = @"
# Classic Parity and Source Retirement Baseline - CodeUpdate0139

- Original/source files inventoried: $($records.Count)
- Original/source bytes: $(($records | Measure-Object bytes -Sum).Sum)
- Generated Classic runtime files: $($runtimeFiles.Count)
- Generated Classic runtime bytes: $(($runtimeFiles | Measure-Object bytes -Sum).Sum)
- Imported/decoded source retirement ready: **NO**
- WCCMMUD SYSOP unlock state: **correctly reclassified; command/capability parity mapping required**
- Generated records checked: $($decodeAudit.generated_record_count)
- Generated scalar fields checked: $($decodeAudit.scalar_field_count)
- Fields not yet fully connected: $($decodeAudit.disconnected_field_count)

The older `AVAILABLE_WCC_ARTIFACTS_EXHAUSTED` result proves static file-family classification. It does not prove that every valid decoded field is promoted and consumed. Under the user-required standard, retirement remains blocked until every valid field has a Librarium projection, compact generated-data projection, executable gameplay/sysop/builder/board-host consumer, and regression proof.

No source evidence was modified or deleted by this audit.
"@
$markdownPath = Join-Path $reportDirectory 'classic_parity_retirement_baseline.md'
[IO.File]::WriteAllText($markdownPath, $markdown + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Host "Inventoried $($records.Count) source files; runtime has $($runtimeFiles.Count) files. Retirement ready: NO."

