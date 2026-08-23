param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$source = Join-Path $Root 'ImportedDatabases/WCCDefaultInstall/wcc default/wccacts2.dat'
$target = Join-Path $Root 'Librarium/Objects/majormud_plus/majormud_plus_records_0139.json'
$bytes = [IO.File]::ReadAllBytes($source)
$keys = @(
  'single_actor_message','single_room_message','user_actor_message','user_target_message','user_room_message',
  'monster_actor_message','monster_room_message','inventory_item_actor_message','inventory_item_room_message',
  'floor_item_actor_message','floor_item_room_message')
$arguments = @(
  @(), @('%i','%p'), @('%t'), @('%i'), @('%i','%t'), @('%t'), @('%i','%t'), @('%t'), @('%i','%p','%t'), @('%t'), @('%i','%t'))
function Read-Field([int]$Offset,[int]$Length) {
  ([Text.Encoding]::ASCII.GetString($bytes,$Offset,$Length).Trim([char]0,' '))
}
function Normalize-Template([string]$Value,[string[]]$Values) {
  $result=$Value
  $occurrences=([regex]::Matches($Value,'%s')).Count
  $effective=$Values
  if($Values.Count -eq 3 -and $occurrences -eq 2){$effective=@($Values[0],$Values[2])}
  foreach($replacement in $effective) {
    $position=$result.IndexOf('%s',[StringComparison]::Ordinal)
    if($position -lt 0){break}
    $result=$result.Substring(0,$position)+$replacement+$result.Substring($position+2)
  }
  $result
}
$actions=@()
for($page=6;$page -le 21;$page++) {
  for($slot=0;$slot -lt 4;$slot++) {
    $offset=$page*4096+8+$slot*1012
    $name=(Read-Field $offset 30).ToLowerInvariant()
    if($name -notmatch '^[a-z][a-z0-9_-]*$'){throw "Invalid WCC action record at page $page slot $slot"}
    $messages=[ordered]@{}
    for($index=0;$index -lt 11;$index++) {
      $value=Read-Field ($offset+30+$index*80) 80
      if($value.Length -gt 0){$messages[$keys[$index]]=Normalize-Template $value $arguments[$index]}
    }
    $actions += [ordered]@{name=$name;board_specific=$false;wcc_origin=$true;messages=$messages;native_page=$page;native_slot=$slot}
  }
}
$document=Get-Content -LiteralPath $target -Raw | ConvertFrom-Json -Depth 100
$document.records[0] | Add-Member -Force NoteProperty default_actions $actions
$document.records[0] | Add-Member -Force NoteProperty default_action_evidence ([ordered]@{
  source='ImportedDatabases/WCCDefaultInstall/wcc default/wccacts2.dat'
  record_count=64
  page_range_inclusive=@(6,21)
  page_size=4096
  records_per_page=4
  record_stride=1012
  action_name_bytes=30
  message_slot_bytes=80
  message_slot_count=11
  classification='native_fixed_record_exact'
})
$document | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $target -Encoding utf8
Write-Output "Promoted $($actions.Count) native WCC actions."
