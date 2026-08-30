param(
    [Parameter(Mandatory=$true)][string]$ManifestJson,
    [Parameter(Mandatory=$true)][string]$MdbPath
)

$ErrorActionPreference = 'Stop'

function Escape-Name([string]$Name) { return '[' + $Name.Replace(']', ']]') + ']' }

function Jet-Type($Column) {
    $type = ([string]$Column.type).ToUpperInvariant()
    switch ($type) {
        'LONG' { return 'LONG' }
        'DOUBLE' { return 'DOUBLE' }
        'YESNO' { return 'YESNO' }
        'MEMO' { return 'MEMO' }
        default {
            $size = 255
            if ($Column.PSObject.Properties.Name -contains 'size' -and [int]$Column.size -gt 0) { $size = [Math]::Min(255, [Math]::Max(1, [int]$Column.size)) }
            return "TEXT($size)"
        }
    }
}

function OleDb-Type($Column) {
    $type = ([string]$Column.type).ToUpperInvariant()
    switch ($type) {
        'LONG' { return [System.Data.OleDb.OleDbType]::Integer }
        'DOUBLE' { return [System.Data.OleDb.OleDbType]::Double }
        'YESNO' { return [System.Data.OleDb.OleDbType]::Boolean }
        'MEMO' { return [System.Data.OleDb.OleDbType]::LongVarWChar }
        default { return [System.Data.OleDb.OleDbType]::VarWChar }
    }
}

function Convert-Value($Column, $Value) {
    if ($null -eq $Value) { return [DBNull]::Value }
    $type = ([string]$Column.type).ToUpperInvariant()
    switch ($type) {
        'LONG' { try { return [int]$Value } catch { return 0 } }
        'DOUBLE' { try { return [double]$Value } catch { return 0.0 } }
        'YESNO' {
            if ($Value -is [bool]) { return $Value }
            $text = [string]$Value
            return $text -eq '1' -or $text.Equals('true',[StringComparison]::OrdinalIgnoreCase) -or $text.Equals('yes',[StringComparison]::OrdinalIgnoreCase)
        }
        default { return [string]$Value }
    }
}

$manifestPath = [IO.Path]::GetFullPath($ManifestJson)
$outputPath = [IO.Path]::GetFullPath($MdbPath)
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Manifest not found: $manifestPath" }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schema -ne 'reforged_nightmare_mdb_export_v1') { throw "Unexpected export manifest schema: $($manifest.schema)" }

$directory = Split-Path -Parent $outputPath
if ($directory -and -not (Test-Path -LiteralPath $directory)) { [void](New-Item -ItemType Directory -Path $directory -Force) }
if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force }

try {
    $catalog = New-Object -ComObject ADOX.Catalog
    $catalog.Create("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=$outputPath;Jet OLEDB:Engine Type=5;")
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($catalog)
} catch {
    throw "Could not create Jet MDB '$outputPath'. The 32-bit Microsoft Jet provider/ADOX components are required. $($_.Exception.Message)"
}

$conn = New-Object System.Data.OleDb.OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=$outputPath;Persist Security Info=False;")
$conn.Open()
try {
    foreach ($table in $manifest.tables) {
        $tableName = [string]$table.name
        if ([string]::IsNullOrWhiteSpace($tableName)) { continue }
        $columns = @($table.columns)
        if ($columns.Count -eq 0) { continue }
        if ($columns.Count -gt 255) { throw "Table '$tableName' has $($columns.Count) columns; Jet supports at most 255." }

        $defs = @()
        foreach ($column in $columns) {
            $defs += "$(Escape-Name ([string]$column.name)) $(Jet-Type $column)"
        }
        $create = $conn.CreateCommand()
        $create.CommandText = "CREATE TABLE $(Escape-Name $tableName) ($($defs -join ', '))"
        [void]$create.ExecuteNonQuery()
        $create.Dispose()

        foreach ($row in @($table.rows)) {
            $names = @($columns | ForEach-Object { Escape-Name ([string]$_.name) })
            $placeholders = @($columns | ForEach-Object { '?' })
            $insert = $conn.CreateCommand()
            $insert.CommandText = "INSERT INTO $(Escape-Name $tableName) ($($names -join ', ')) VALUES ($($placeholders -join ', '))"
            foreach ($column in $columns) {
                $columnName = [string]$column.name
                $value = $null
                if ($row.PSObject.Properties.Name -contains $columnName) { $value = $row.PSObject.Properties[$columnName].Value }
                $parameter = $insert.Parameters.Add('@p', (OleDb-Type $column))
                $parameter.Value = Convert-Value $column $value
            }
            [void]$insert.ExecuteNonQuery()
            $insert.Dispose()
        }
    }
} finally {
    $conn.Close(); $conn.Dispose()
}
