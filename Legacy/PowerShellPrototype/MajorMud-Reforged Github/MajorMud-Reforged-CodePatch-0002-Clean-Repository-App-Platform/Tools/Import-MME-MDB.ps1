
param([string]$ProjectRoot = (Get-Location).Path)
$ErrorActionPreference = 'Stop'
function Write-Info($m){ Write-Host "[IMPORT] $m" }
function Safe-Name([string]$s){ return (($s -replace '[^A-Za-z0-9_\- ]','_') -replace '\s+','_') }
function To-Array($x){ if($null -eq $x){ return @() }; if($x -is [System.Array]){ return $x }; return @($x) }
function Convert-DataTableToObjects($dt, [string]$tableName){
  $out = New-Object System.Collections.Generic.List[object]
  foreach($row in $dt.Rows){
    $o = [ordered]@{ source='data-v1.11n.mdb'; source_table=$tableName }
    foreach($col in $dt.Columns){
      $v = $row[$col.ColumnName]
      if($v -is [System.DBNull]){ $v = $null }
      $o[$col.ColumnName] = $v
    }
    $out.Add([pscustomobject]$o) | Out-Null
  }
  return @($out)
}
function Get-Key($obj){
  $props = @($obj.PSObject.Properties.Name)
  foreach($k in @('ID','Id','id','Number','Num','VNum','VNUM','RecordID','Index')){ if($props -contains $k -and $null -ne $obj.$k -and "$($obj.$k)" -ne ''){ return "$k=$($obj.$k)" } }
  foreach($k in @('Name','name','ItemName','MobName','RoomName','SpellName','Class','Race','Title')){ if($props -contains $k -and $null -ne $obj.$k -and "$($obj.$k)" -ne ''){ return "$k=$($obj.$k)".ToLowerInvariant() } }
  return ($obj | ConvertTo-Json -Compress -Depth 4)
}
function Merge-JsonArray($path, $incoming){
  $existing = @()
  if(Test-Path $path){ try { $existing = @(Get-Content $path -Raw | ConvertFrom-Json) } catch { $existing=@() } }
  $map = @{}
  foreach($e in $existing){ $map[(Get-Key $e)] = $e }
  $added=0; $merged=0
  foreach($i in $incoming){
    $key = Get-Key $i
    if($map.ContainsKey($key)){
      # Preserve existing record but fill missing fields from imported data.
      foreach($p in $i.PSObject.Properties){ if(-not ($map[$key].PSObject.Properties.Name -contains $p.Name)){ Add-Member -InputObject $map[$key] -MemberType NoteProperty -Name $p.Name -Value $p.Value -Force } }
      $merged++
    } else { $map[$key] = $i; $added++ }
  }
  $arr = @($map.Values | Sort-Object { Get-Key $_ })
  $arr | ConvertTo-Json -Depth 50 | Set-Content $path -Encoding UTF8
  return @{added=$added; merged=$merged; total=$arr.Count}
}
function Write-FieldDictionary($dataRoot, $tables){
  $metaDir = Join-Path $dataRoot 'metadata'; New-Item -ItemType Directory -Force $metaDir | Out-Null
  $common = [ordered]@{
    ID='Unique record number from the source database.'; Id='Unique record number from the source database.'; Name='Display name shown in the editor/game.'; Description='Long description shown to players or editors.'; Desc='Description text.'; Type='Record type. Numeric values are preserved from the source; see *_value_summary fields for discovered values.'; Flags='Bitmask/flag field from the source database. The raw value is preserved and should later be decoded with a source-specific lookup.'; Slot='Equipment slot or location field when used by item records.'; AC='Armor class / protection value when used by gear.'; Damage='Damage value or dice expression when used by weapons/monsters.'; HP='Hit points.'; MP='Mana points.'; Gold='Currency amount.'; XP='Experience value/reward.'; Level='Level requirement or monster/player level.'; Race='Race name or race restriction.'; Class='Class name or class restriction.'; Spell='Spell reference.'; Price='Shop or item price.'; Weight='Item weight.'; Room='Room reference.'; Exit='Room exit reference.'
  }
  $schema = [ordered]@{ generated=(Get-Date).ToString('s'); note='This dictionary explains common fields and also includes table/column summaries generated from the MDB. Unknown numeric codes are preserved raw and flagged for later decoding as we map the original database.'; common_field_meanings=$common; tables=$tables }
  $schema | ConvertTo-Json -Depth 50 | Set-Content (Join-Path $metaDir 'field_dictionary.json') -Encoding UTF8
  @'
MajorMUD Reforged Field Meaning Notes

The MDB import preserves original fields exactly, then adds readable metadata where possible.
Common meanings:
- ID/Number/VNum: source database record number.
- Name/Title: editor/player display name.
- Description/Desc: text shown in game or editor.
- Type/Flags/Bitmask-style numbers: original database codes. These are kept raw and summarized in field_dictionary.json. As we map each code, the editor will show labels instead of only numbers.
- Slot/Wear/Equip fields: item equipment slot or wearable location.
- AC/Damage/HP/XP/Gold/Level/Price/Weight: direct gameplay values.

Use Data\ImportedMDB for the raw imported records and Data\metadata\field_dictionary.json for discovered field meanings/value summaries.
'@ | Set-Content (Join-Path $metaDir 'README_FIELD_MEANINGS.txt') -Encoding UTF8
}
$dataRoot = Join-Path $ProjectRoot 'Data'
New-Item -ItemType Directory -Force $dataRoot | Out-Null
$impRoot = Join-Path $dataRoot 'ImportedMDB'; New-Item -ItemType Directory -Force $impRoot | Out-Null
$rawSrc = Join-Path $ProjectRoot 'ImportedDatabases'
$mdb = Get-ChildItem $rawSrc -Filter '*.mdb' -Recurse | Select-Object -First 1
if(-not $mdb){ throw "No MDB found under $rawSrc" }
Write-Info "Using MDB: $($mdb.FullName)"
$providers = @('Microsoft.ACE.OLEDB.16.0','Microsoft.ACE.OLEDB.12.0','Microsoft.Jet.OLEDB.4.0')
$conn=$null; $used=$null
foreach($p in $providers){
  try{
    $cs = "Provider=$p;Data Source=$($mdb.FullName);Persist Security Info=False;"
    $conn = New-Object System.Data.OleDb.OleDbConnection($cs); $conn.Open(); $used=$p; break
  } catch { $conn=$null }
}
if(-not $conn){ throw "Could not open MDB. Install Microsoft Access Database Engine/ACE provider, then rerun this patch." }
Write-Info "Opened MDB with $used"
$schema = $conn.GetSchema('Tables')
$tables = @($schema | Where-Object { $_.TABLE_TYPE -eq 'TABLE' -and $_.TABLE_NAME -notlike 'MSys*' } | Select-Object -ExpandProperty TABLE_NAME | Sort-Object)
$report = New-Object System.Collections.Generic.List[string]
$dictTables = [ordered]@{}
$totalRecords=0
foreach($t in $tables){
  try{
    $cmd = $conn.CreateCommand(); $cmd.CommandText = "SELECT * FROM [$t]"
    $ad = New-Object System.Data.OleDb.OleDbDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    [void]$ad.Fill($dt)
    $objects = Convert-DataTableToObjects $dt $t
    $safe = Safe-Name $t
    $path = Join-Path $impRoot "$safe.json"
    $m = Merge-JsonArray $path $objects
    $totalRecords += $objects.Count
    $cols = [ordered]@{}
    foreach($col in $dt.Columns){
      $vals = @($dt.Rows | ForEach-Object { $_[$col.ColumnName] } | Where-Object { $_ -ne [DBNull]::Value -and $null -ne $_ } | Select-Object -First 30)
      $cols[$col.ColumnName] = [ordered]@{ type=$col.DataType.Name; sample_values=@($vals | Select-Object -First 10); unique_sample_count=(@($vals | Select-Object -Unique)).Count }
    }
    $dictTables[$t] = [ordered]@{ record_count=$dt.Rows.Count; columns=$cols; imported_file="Data/ImportedMDB/$safe.json" }
    $report.Add("$t : imported $($objects.Count), added $($m.added), merged $($m.merged), final $($m.total)") | Out-Null
  } catch { $report.Add("$t : ERROR $($_.Exception.Message)") | Out-Null }
}
$conn.Close()
Write-FieldDictionary $dataRoot $dictTables
$repDir = Join-Path $ProjectRoot 'Reports'; New-Item -ItemType Directory -Force $repDir | Out-Null
$report.Insert(0, "MajorMUD Reforged v3.7 MDB Import Report")
$report.Insert(1, "MDB: $($mdb.Name)")
$report.Insert(2, "Tables: $($tables.Count)  Raw records processed: $totalRecords")
$report | Set-Content (Join-Path $repDir 'ImportReport-v3.7.txt') -Encoding UTF8
Write-Info "Done. Tables: $($tables.Count); Records: $totalRecords"
Write-Info "Report: Reports\ImportReport-v3.7.txt"
