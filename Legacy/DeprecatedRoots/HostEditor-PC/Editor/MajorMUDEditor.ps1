
# MajorMUD Reforged Host Editor v2.6
# Simple Redux-style console editor. Edits shared JSON data so server/editor/client use same files.
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$DataDir = Join-Path $Root 'Data'
function Pause-Key { Write-Host ''; Read-Host 'Press Enter' | Out-Null }
function Load-Json($name, $fallback) {
  $p = Join-Path $DataDir $name
  if(Test-Path $p){ return (Get-Content $p -Raw | ConvertFrom-Json -Depth 50) }
  return ($fallback | ConvertTo-Json -Depth 50 | ConvertFrom-Json)
}
function Save-Json($name, $obj) {
  $p = Join-Path $DataDir $name
  $obj | ConvertTo-Json -Depth 80 | Set-Content -Path $p -Encoding UTF8
  Write-Host "Saved $name" -ForegroundColor Green
}
function Backup-Data {
  $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
  $dest = Join-Path $Root "Backups\Data-$stamp"
  New-Item -ItemType Directory -Force -Path $dest | Out-Null
  Copy-Item -Path (Join-Path $DataDir '*') -Destination $dest -Recurse -Force
  Write-Host "Backup created: $dest" -ForegroundColor Green
}
function Show-Header($title){ Clear-Host; Write-Host '==========================================' -ForegroundColor Cyan; Write-Host "  $title" -ForegroundColor Cyan; Write-Host '==========================================' -ForegroundColor Cyan }
function Edit-KeyValue($obj, $label){
  while($true){
    Show-Header "Edit $label"
    $props = @($obj.PSObject.Properties.Name)
    for($i=0;$i -lt $props.Count;$i++){ $k=$props[$i]; $v=$obj.$k; if($v -is [System.Management.Automation.PSCustomObject]){ $v='{...}' }; Write-Host ("{0}) {1}: {2}" -f ($i+1),$k,$v) }
    Write-Host 'A) Add field   S) Save/Back   Q) Back without more edits'
    $choice = Read-Host 'Select field'
    if($choice -match '^(s|q)$'){ return $obj }
    if($choice -match '^a$'){
      $nk=Read-Host 'New field name'; $nv=Read-Host 'Value'; Add-Member -InputObject $obj -NotePropertyName $nk -NotePropertyValue $nv -Force; continue
    }
    if($choice -as [int]){ $idx=[int]$choice-1; if($idx -ge 0 -and $idx -lt $props.Count){ $k=$props[$idx]; $nv=Read-Host "New value for $k"; $obj.$k=$nv } }
  }
}
function Edit-Collection($file,$title,$nameField){
  $data = Load-Json $file @()
  if($data -isnot [System.Array]){ $data=@($data) }
  while($true){
    Show-Header $title
    for($i=0;$i -lt $data.Count;$i++){ $n=$data[$i].$nameField; if(-not $n){$n=$data[$i].id}; Write-Host ("{0}) {1}" -f ($i+1),$n) }
    Write-Host ''
    Write-Host 'A) Add new   S) Save   Q) Back'
    $c=Read-Host 'Choice'
    if($c -match '^q$'){ return }
    if($c -match '^s$'){ Save-Json $file $data; continue }
    if($c -match '^a$'){
      $id=Read-Host 'id'; $nm=Read-Host 'name'; $desc=Read-Host 'description'
      $new=[pscustomobject]@{id=$id; name=$nm; description=$desc}
      $data += $new; continue
    }
    if($c -as [int]){ $idx=[int]$c-1; if($idx -ge 0 -and $idx -lt $data.Count){ $data[$idx]=Edit-KeyValue $data[$idx] ($data[$idx].$nameField) } }
  }
}
function Edit-WorldRooms {
  $world = Load-Json 'world.json' @{}
  if(-not $world.rooms){ Write-Host 'world.json has no rooms collection.'; Pause-Key; return }
  $rooms = @($world.rooms)
  while($true){
    Show-Header 'Room Editor'
    for($i=0;$i -lt $rooms.Count;$i++){ Write-Host ("{0}) {1} [{2}]" -f ($i+1),$rooms[$i].name,$rooms[$i].id) }
    Write-Host 'A) Add room   S) Save world.json   Q) Back'
    $c=Read-Host 'Choice'
    if($c -match '^q$'){ return }
    if($c -match '^s$'){ $world.rooms=$rooms; Save-Json 'world.json' $world; continue }
    if($c -match '^a$'){
      $id=Read-Host 'room id'; $name=Read-Host 'room name'; $desc=Read-Host 'description'
      $rooms += [pscustomobject]@{id=$id;name=$name;description=$desc;exits=[pscustomobject]@{};items=@();monsters=@();npcs=@()}; continue
    }
    if($c -as [int]){ $idx=[int]$c-1; if($idx -ge 0 -and $idx -lt $rooms.Count){ $rooms[$idx]=Edit-KeyValue $rooms[$idx] $rooms[$idx].name } }
  }
}
function Edit-Messages {
  $msg=Load-Json 'messages.json' @{}
  while($true){
    Show-Header 'Messages Editor'
    Write-Host '1) System messages'
    Write-Host '2) Combat messages'
    Write-Host '3) Monster messages'
    Write-Host 'S) Save   Q) Back'
    $c=Read-Host 'Choice'
    if($c -match '^q$'){ return }
    if($c -match '^s$'){ Save-Json 'messages.json' $msg; continue }
    if($c -eq '1'){ $msg.system=Edit-KeyValue $msg.system 'System Messages' }
    elseif($c -eq '2'){ $msg.combat=Edit-KeyValue $msg.combat 'Combat Messages' }
    elseif($c -eq '3'){ $msg.monsters=Edit-KeyValue $msg.monsters 'Monster Messages' }
  }
}
while($true){
  Show-Header 'MajorMUD Reforged Host Editor v2.6'
  Write-Host 'Simple Redux-style editor: choose a database, edit fields, save. Shared Data folder is used by server and clients.'
  Write-Host ''
  Write-Host '1) Rooms / World'
  Write-Host '2) Items'
  Write-Host '3) Monsters'
  Write-Host '4) Races'
  Write-Host '5) Classes'
  Write-Host '6) Skills'
  Write-Host '7) Spells'
  Write-Host '8) Messages / Text'
  Write-Host '9) Backup Data'
  Write-Host 'Q) Quit'
  $c=Read-Host 'Choice'
  switch -Regex ($c) {
    '^1$' { Edit-WorldRooms }
    '^2$' { Edit-Collection 'items.json' 'Item Editor' 'name' }
    '^3$' { Edit-Collection 'monsters.json' 'Monster Editor' 'name' }
    '^4$' { Edit-Collection 'races.json' 'Race Editor' 'name' }
    '^5$' { Edit-Collection 'classes.json' 'Class Editor' 'name' }
    '^6$' { Edit-Collection 'skills.json' 'Skill Editor' 'name' }
    '^7$' { Edit-Collection 'spells.json' 'Spell Editor' 'name' }
    '^8$' { Edit-Messages }
    '^9$' { Backup-Data; Pause-Key }
    '^(q|quit|exit)$' { break }
  }
}
