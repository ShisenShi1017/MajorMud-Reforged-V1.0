param(
    [Parameter(Mandatory=$true)][string]$MdbPath,
    [Parameter(Mandatory=$true)][string]$OutputJson
)

$ErrorActionPreference = 'Stop'

function Open-MdbConnection([string]$Path) {
    $providers = @('Microsoft.Jet.OLEDB.4.0','Microsoft.ACE.OLEDB.16.0','Microsoft.ACE.OLEDB.12.0')
    $errors = @()
    foreach ($provider in $providers) {
        try {
            $connection = New-Object System.Data.OleDb.OleDbConnection("Provider=$provider;Data Source=$Path;Persist Security Info=False;")
            $connection.Open()
            return [pscustomobject]@{ Connection=$connection; Provider=$provider }
        } catch {
            $errors += "${provider}: $($_.Exception.Message)"
        }
    }
    throw "No compatible Jet/ACE provider could open '$Path'. Tried: $($errors -join ' | ')"
}

function Convert-Cell($Value) {
    if ($null -eq $Value -or $Value -is [System.DBNull]) { return $null }
    if ($Value -is [byte[]]) { return [Convert]::ToBase64String($Value) }
    if ($Value -is [DateTime]) { return $Value.ToString('o') }
    if ($Value -is [Guid]) { return $Value.ToString() }
    return $Value
}

$resolved = [IO.Path]::GetFullPath($MdbPath)
if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) { throw "MDB not found: $resolved" }
$opened = Open-MdbConnection $resolved
$conn = $opened.Connection
try {
    $schema = $conn.GetSchema('Tables')
    $tables = @()
    foreach ($schemaRow in $schema.Rows) {
        $name = [string]$schemaRow['TABLE_NAME']
        $type = [string]$schemaRow['TABLE_TYPE']
        if ($type -ne 'TABLE') { continue }
        if ($name.StartsWith('MSys', [StringComparison]::OrdinalIgnoreCase)) { continue }

        $escaped = $name.Replace(']', ']]')
        $adapter = New-Object System.Data.OleDb.OleDbDataAdapter("SELECT * FROM [$escaped]", $conn)
        $dt = New-Object System.Data.DataTable
        [void]$adapter.Fill($dt)
        $columns = @($dt.Columns | ForEach-Object { $_.ColumnName })
        $rows = @()
        foreach ($dataRow in $dt.Rows) {
            $obj = [ordered]@{}
            foreach ($column in $dt.Columns) {
                $obj[$column.ColumnName] = Convert-Cell $dataRow[$column]
            }
            $rows += [pscustomobject]$obj
        }
        $tables += [pscustomobject][ordered]@{
            name = $name
            columns = $columns
            rows = $rows
        }
    }

    $result = [pscustomobject][ordered]@{
        schema = 'nightmare_mdb_dump_v1'
        provider = $opened.Provider
        source = $resolved
        tables = $tables
    }
    $json = $result | ConvertTo-Json -Depth 12
    $directory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputJson))
    if ($directory -and -not (Test-Path -LiteralPath $directory)) { [void](New-Item -ItemType Directory -Path $directory -Force) }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputJson), $json, (New-Object System.Text.UTF8Encoding($false)))
} finally {
    if ($conn) { $conn.Close(); $conn.Dispose() }
}
