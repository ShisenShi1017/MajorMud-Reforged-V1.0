$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root
New-Item -ItemType Directory -Force -Path "$Root\Logs", "$Root\Saves", "$Root\Data" | Out-Null
$LogPath = "$Root\Logs\server.log"
$ESC = [char]27

function Log($msg) { $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $msg"; Add-Content -Path $LogPath -Value $line; Write-Host $line }
function C($code, $text, $ansi=$true) { if ($ansi) { return "$ESC[$code`m$text$ESC[0m" } return $text }
function Send-Line($writer, $text="") { $writer.WriteLine($text); $writer.Flush(); $clean = ($text -replace "\x1b\[[0-9;]*m", ""); Log "SEND: $clean" }
function Prompt($writer, $ansi=$true) { $writer.Write((C "92" "> " $ansi)); $writer.Flush(); Log "SEND: >" }
function Load-World { $path = "$Root\Data\world.json"; if (!(Test-Path $path)) { throw "Missing Data\world.json" }; return Get-Content $path -Raw | ConvertFrom-Json }
function Load-Json($name,$fallback=$null) { $path = "$Root\Data\$name"; if (Test-Path $path) { return Get-Content $path -Raw | ConvertFrom-Json }; return $fallback }
function Load-Leveling { return Load-Json 'leveling.json' ([pscustomobject]@{startLevel=1;pointsPerLevel=5;statCosts=[pscustomobject]@{str=1;agi=1;int=1;wil=1;cha=1;con=1}}) }
function Load-DerivedStats { return Load-Json 'derived_stats.json' ([pscustomobject]@{}) }
function Load-Skills { return Load-Json 'skills.json' ([pscustomobject]@{skills=@()}) }
function Load-Spells { return Load-Json 'spells.json' ([pscustomobject]@{spells=@()}) }
function Get-PropVal($obj,$name,$default=0) { if($null -eq $obj){return $default}; $p=$obj.PSObject.Properties[$name]; if($null -eq $p){return $default}; return $p.Value }
function Add-Bonus($world,$char,$key) {
  $total=0
  $race=$world.races.($char.race); $class=$world.classes.($char.class)
  $rb=Get-PropVal $race 'secondaryMods' $null; $cb=Get-PropVal $class 'secondaryMods' $null
  $total += [int](Get-PropVal $rb $key 0); $total += [int](Get-PropVal $cb $key 0)
  foreach($slot in $char.equipment.PSObject.Properties){ if($slot.Value -and $world.items.PSObject.Properties[$slot.Value]){ $it=$world.items.($slot.Value); $db=Get-PropVal $it 'derivedMods' $null; $total += [int](Get-PropVal $db $key 0) } }
  return $total
}
function Calc-Derived($world,$char) {
  $st=$char.stats
  $str=[int]$st.str; $agi=[int]$st.agi; $int=[int]$st.int; $wil=[int]$st.wil; $cha=[int]$st.cha; $con=[int]$st.con
  $d=[ordered]@{}
  $d.stealth    = [Math]::Floor($agi*2 + $int/3 + (Add-Bonus $world $char 'stealth'))
  $d.perception = [Math]::Floor($agi + $int + $wil/2 + (Add-Bonus $world $char 'perception'))
  $d.search     = [Math]::Floor($int + $agi/2 + $wil/2 + (Add-Bonus $world $char 'search'))
  $d.dodge      = [Math]::Floor($agi*2 + $con/3 + (Add-Bonus $world $char 'dodge'))
  $d.hitBonus   = [Math]::Floor($agi/2 + $str/3 + (Add-Bonus $world $char 'hitBonus'))
  $d.damageBonus= [Math]::Floor(($str-10)/3 + (Add-Bonus $world $char 'damageBonus'))
  $d.magic      = [Math]::Floor($int*2 + $wil/2 + (Add-Bonus $world $char 'magic'))
  $d.faith      = [Math]::Floor($wil*2 + $cha/2 + (Add-Bonus $world $char 'faith'))
  $d.charm      = [Math]::Floor($cha*2 + $wil/3 + (Add-Bonus $world $char 'charm'))
  $d.lockpick   = [Math]::Floor($agi + $int + (Add-Bonus $world $char 'lockpick'))
  $d.hide       = [Math]::Floor($agi*2 + (Add-Bonus $world $char 'hide'))
  $d.tracking   = [Math]::Floor($wil + $int + (Add-Bonus $world $char 'tracking'))
  $d.parry      = [Math]::Floor($agi + $str/2 + (Add-Bonus $world $char 'parry'))
  $d.critical   = [Math]::Floor($agi + $int/2 + (Add-Bonus $world $char 'critical'))
  return [pscustomobject]$d
}
function Get-TrainingCost($leveling,$stat,$current) {
  $cur=[int]$current
  if($leveling.PSObject.Properties['costCurves']){
    foreach($band in @($leveling.costCurves)){ if($cur -ge [int]$band.min -and $cur -le [int]$band.max){ return [int]$band.cost } }
  }
  return [int](Get-PropVal $leveling.statCosts $stat 1)
}

function Get-PointsForLevel($leveling,$level){
  $base=[int](Get-PropVal $leveling 'pointsPerLevelBase' (Get-PropVal $leveling 'pointsPerLevel' 5))
  $every=[int](Get-PropVal $leveling 'pointsPerLevelBonusEvery' 10)
  $bonus=[int](Get-PropVal $leveling 'pointsPerLevelBonusAmount' 1)
  if($every -le 0){ return $base }
  return $base + ([Math]::Floor(([int]$level-1)/$every) * $bonus)
}
function Get-StartingPoints($leveling,$startLevel){
  $total=0
  for($lvl=2; $lvl -le [int]$startLevel; $lvl++){ $total += Get-PointsForLevel $leveling $lvl }
  return $total
}
function Copy-Stats($stats){ return [pscustomobject]@{str=[int]$stats.str;agi=[int]$stats.agi;int=[int]$stats.int;wil=[int]$stats.wil;cha=[int]$stats.cha;con=[int]$stats.con} }
$script:TrainActive=$false
$script:TrainDraftStats=$null
$script:TrainDraftPoints=0
$script:TrainOriginalStats=$null
$script:TrainOriginalPoints=0
function To-Array($x) { if ($null -eq $x) { return @() }; return @($x) }
function Save-Char($c) { $c | ConvertTo-Json -Depth 20 | Set-Content -Path "$Root\Saves\$($c.name).json" }
function New-Equipment { return [ordered]@{ head=$null; face=$null; "left ear"=$null; "right ear"=$null; neck=$null; body=$null; back=$null; arms=$null; hands=$null; "left finger"=$null; "right finger"=$null; waist=$null; "belt slot 1"=$null; "belt slot 2"=$null; legs=$null; feet=$null; "main hand"="training_sword"; "off hand"=$null; ranged=$null } }
function Ensure-CharShape($c) {
  if (-not ($c.PSObject.Properties.Name -contains 'inventory')) { Add-Member -InputObject $c -NotePropertyName inventory -NotePropertyValue @('training_sword') }
  if (-not ($c.PSObject.Properties.Name -contains 'equipment')) { Add-Member -InputObject $c -NotePropertyName equipment -NotePropertyValue ([pscustomobject](New-Equipment)) }
  if (-not ($c.PSObject.Properties.Name -contains 'ansi')) { Add-Member -InputObject $c -NotePropertyName ansi -NotePropertyValue $true }
  if (-not ($c.PSObject.Properties.Name -contains 'race')) { Add-Member -InputObject $c -NotePropertyName race -NotePropertyValue 'human' }
  if (-not ($c.PSObject.Properties.Name -contains 'class')) { Add-Member -InputObject $c -NotePropertyName class -NotePropertyValue 'warrior' }
  if (-not ($c.PSObject.Properties.Name -contains 'level')) { Add-Member -InputObject $c -NotePropertyName level -NotePropertyValue 1 }
  if (-not ($c.PSObject.Properties.Name -contains 'stats')) { Add-Member -InputObject $c -NotePropertyName stats -NotePropertyValue ([pscustomobject]@{str=10;agi=10;int=10;wil=10;cha=10;con=10}) }
  if (-not ($c.PSObject.Properties.Name -contains 'ac')) { Add-Member -InputObject $c -NotePropertyName ac -NotePropertyValue 0 }
  if (-not ($c.PSObject.Properties.Name -contains 'trainingPoints')) { Add-Member -InputObject $c -NotePropertyName trainingPoints -NotePropertyValue 0 }
  return $c
}
function Load-Char($name, $world) {
  $safe = ($name -replace '[^A-Za-z0-9_ -]', '').Trim(); if ([string]::IsNullOrWhiteSpace($safe)) { $safe = "Player" }
  $path = "$Root\Saves\$safe.json"
  if (Test-Path $path) { return Ensure-CharShape (Get-Content $path -Raw | ConvertFrom-Json) }
  return $null
}

function Find-RaceId($world, $text) {
  $t=(Normalize $text)
  $props=@($world.races.PSObject.Properties)
  $num=0
  if([int]::TryParse($t, [ref]$num)){ if($num -ge 1 -and $num -le $props.Count){ return $props[$num-1].Name } }
  $pairs=@(); foreach($p in $props){ $pairs += [pscustomobject]@{id=$p.Name; name=$p.Value.name} }
  $match=Best-Match $pairs $text
  if($null -eq $match){ $key=(Normalize $text); if($world.races.PSObject.Properties[$key]){return $key} }
  return $match
}
function Find-ClassId($world, $text) {
  $t=(Normalize $text)
  $props=@($world.classes.PSObject.Properties)
  $num=0
  if([int]::TryParse($t, [ref]$num)){ if($num -ge 1 -and $num -le $props.Count){ return $props[$num-1].Name } }
  $pairs=@(); foreach($p in $props){ $pairs += [pscustomobject]@{id=$p.Name; name=$p.Value.name} }
  $match=Best-Match $pairs $text
  if($null -eq $match){ $key=(Normalize $text); if($world.classes.PSObject.Properties[$key]){return $key} }
  return $match
}
function New-Stats($race, $class) {
  $base=[ordered]@{str=10;agi=10;int=10;wil=10;cha=10;con=10}
  foreach($p in $race.statMods.PSObject.Properties){ $base[$p.Name] = [int]$base[$p.Name] + [int]$p.Value }
  foreach($p in $class.statMods.PSObject.Properties){ $base[$p.Name] = [int]$base[$p.Name] + [int]$p.Value }
  return [pscustomobject]$base
}
function Next-Xp($world,$level){ $next=[string]([int]$level+1); if($world.levels.PSObject.Properties[$next]){ return [int]$world.levels.$next }; return $null }
function New-Char-WithRaceClass($name, $raceId, $classId, $world) {
  $safe = ($name -replace '[^A-Za-z0-9_ -]', '').Trim(); if ([string]::IsNullOrWhiteSpace($safe)) { $safe = "Player" }
  $race=$world.races.$raceId; $class=$world.classes.$classId; $leveling=Load-Leveling
  $startLevel=[int]$leveling.startLevel; if($startLevel -lt 1){$startLevel=1}
  $points=Get-StartingPoints $leveling $startLevel
  $max=[int]$race.maxHp + [int]$class.hpBonus + (([int]$startLevel-1) * [int]$class.levelHp)
  $gold=[int]$race.gold + [int]$class.gold
  $c = [pscustomobject]@{ name=$safe; race=$raceId; class=$classId; level=$startLevel; trainingPoints=$points; room=$world.startRoom; hp=$max; maxHp=$max; xp=0; gold=$gold; stats=(New-Stats $race $class); inventory=@('training_sword'); equipment=[pscustomobject](New-Equipment); ansi=$true; ac=0 }
  Save-Char $c; return $c
}

function Slot-Value($eq, $slot) { $p = $eq.PSObject.Properties[$slot]; if ($null -eq $p) { return $null }; return $p.Value }
function Set-Slot($eq, $slot, $value) { $p = $eq.PSObject.Properties[$slot]; if ($null -eq $p) { Add-Member -InputObject $eq -NotePropertyName $slot -NotePropertyValue $value } else { $p.Value = $value } }
function Normalize($s) { return (($s -as [string]).ToLowerInvariant() -replace '[^a-z0-9 ]',' ').Trim() }
function Score-Name($name, $needle) {
  $n=Normalize $needle; if($n -eq ''){return 0}; $nm=Normalize $name
  if($nm -eq $n){return 100}
  if($nm.StartsWith($n)){return 90}
  foreach($word in $nm.Split(' ',[System.StringSplitOptions]::RemoveEmptyEntries)){ if($word -eq $n){return 85}; if($word.StartsWith($n)){return 80} }
  if($nm.Contains($n)){return 50}
  return 0
}
function Best-Match($pairs, $needle) {
  $bestScore=0; $best=@()
  foreach($pair in $pairs){ $score=Score-Name $pair.name $needle; if($score -gt $bestScore){$bestScore=$score; $best=@($pair)} elseif($score -eq $bestScore -and $score -gt 0){$best += $pair} }
  if($bestScore -eq 0){return $null}
  if($best.Count -eq 1){return $best[0].id}
  return $null
}
function Find-RoomItemId($world, $room, $text) { $pairs=@(); foreach($iid in (To-Array $room.items)){ if($world.items.PSObject.Properties[$iid]){ $pairs += [pscustomobject]@{id=$iid; name=$world.items.$iid.name} } }; return Best-Match $pairs $text }
function Find-InventoryItemId($world, $char, $text) { $pairs=@(); foreach($iid in (To-Array $char.inventory)){ if($world.items.PSObject.Properties[$iid]){ $pairs += [pscustomobject]@{id=$iid; name=$world.items.$iid.name} } }; return Best-Match $pairs $text }
function Find-MonsterId($world, $room, $text) { $mids=To-Array $room.monsters; if($mids.Count -eq 0){return $null}; $needle=$text.Trim(); if($needle -eq '' -and $mids.Count -eq 1){return $mids[0]}; $pairs=@(); foreach($mid in $mids){ if($world.monsters.PSObject.Properties[$mid]){ $pairs += [pscustomobject]@{id=$mid; name=$world.monsters.$mid.name} } }; return Best-Match $pairs $needle }
function Get-Ac($world, $char) { $ac=0; foreach($p in $char.equipment.PSObject.Properties){ if($p.Value -and $world.items.PSObject.Properties[$p.Value]){ $it=$world.items.($p.Value); if($it.PSObject.Properties.Name -contains 'ac'){$ac += [int]$it.ac} } }; return $ac }
function Has-BeltSlots($world, $char) { $beltId=Slot-Value $char.equipment 'waist'; if($null -eq $beltId){return 0}; $belt=$world.items.$beltId; if($belt.PSObject.Properties.Name -contains 'beltSlots'){return [int]$belt.beltSlots}; return 0 }
function Show-Room($writer, $world, $char, $tag='') {
  $room=$world.rooms.($char.room); $ansi=[bool]$char.ansi
  Send-Line $writer ""
  if($tag){ Send-Line $writer (C "90" $tag $ansi) }
  Send-Line $writer (C "96;1" $room.name $ansi)
  Send-Line $writer $room.description
  $exits=@($room.exits.PSObject.Properties.Name) -join ", "
  if($exits){Send-Line $writer ((C "93" "Exits:" $ansi)+" $exits")}else{Send-Line $writer "Exits: none"}
  foreach($iid in (To-Array $room.items)){ if($world.items.PSObject.Properties[$iid]){Send-Line $writer ((C "92" "You see" $ansi)+" a $($world.items.$iid.name) here.")} }
  foreach($mid in (To-Array $room.monsters)){ if($world.monsters.PSObject.Properties[$mid]){ $mob=$world.monsters.$mid; if($mob.PSObject.Properties.Name -contains 'presence'){ Send-Line $writer (C "91" $mob.presence $ansi) } else { Send-Line $writer ((C "91" "You see" $ansi)+" a $($mob.name) here.") } } }
  foreach($nid in (To-Array $room.npcs)){ if($world.npcs.PSObject.Properties[$nid]){ $npc=$world.npcs.$nid; if($npc.PSObject.Properties.Name -contains 'presence'){ Send-Line $writer (C "96" $npc.presence $ansi) } } }
  Send-Line $writer ((C "95" "HP:" $ansi)+" $($char.hp)/$($char.maxHp)  LVL: $($char.level)  AC: $(Get-Ac $world $char)  Gold: $($char.gold)  XP: $($char.xp)")
}
function Show-Inventory($writer, $world, $char) { Send-Line $writer (C "96;1" "Inventory" $char.ansi); $inv=To-Array $char.inventory; if($inv.Count -eq 0){Send-Line $writer "  empty";return}; foreach($iid in $inv){$mark=" "; foreach($p in $char.equipment.PSObject.Properties){if($p.Value -eq $iid){$mark='*'}}; $name=if($world.items.PSObject.Properties[$iid]){$world.items.$iid.name}else{$iid}; Send-Line $writer " $mark $name"} }
function Show-Equipment($writer,$world,$char){ Send-Line $writer (C "96;1" "Equipment" $char.ansi); $order=@('head','face','left ear','right ear','neck','body','back','arms','hands','left finger','right finger','waist','belt slot 1','belt slot 2','legs','feet','main hand','off hand','ranged'); $beltSlots=Has-BeltSlots $world $char; foreach($slot in $order){ if(($slot -eq 'belt slot 1' -and $beltSlots -lt 1) -or ($slot -eq 'belt slot 2' -and $beltSlots -lt 2)){continue}; $iid=Slot-Value $char.equipment $slot; $name=if($iid -and $world.items.PSObject.Properties[$iid]){$world.items.$iid.name}else{'--'}; Send-Line $writer (("{0,-13}: {1}" -f $slot,$name)) } }
function Equip-Item($writer,$world,$char,$target){ $iid=Find-InventoryItemId $world $char $target; if($null -eq $iid){Send-Line $writer "You do not have that item.";return}; $item=$world.items.$iid; $slot=$item.slot; if(($slot -like 'belt slot*') -and (Has-BeltSlots $world $char) -lt 1){Send-Line $writer "You need to wear a belt with item slots first.";return}; if($slot -eq 'left ear' -and (Slot-Value $char.equipment 'left ear')){if(-not (Slot-Value $char.equipment 'right ear')){$slot='right ear'}}; if($slot -eq 'left finger' -and (Slot-Value $char.equipment 'left finger')){if(-not (Slot-Value $char.equipment 'right finger')){$slot='right finger'}}; Set-Slot $char.equipment $slot $iid; Save-Char $char; Send-Line $writer ((C "92" "Equipped" $char.ansi)+" $($item.name) on $slot.") }
function Remove-Item($writer,$world,$char,$target){ $needle=$target.Trim().ToLowerInvariant(); if($needle -eq 'all'){foreach($p in $char.equipment.PSObject.Properties){$p.Value=$null};Save-Char $char;Send-Line $writer "Removed all equipment.";return}; foreach($p in $char.equipment.PSObject.Properties){ if($p.Value){$nm=$world.items.($p.Value).name.ToLowerInvariant(); if($nm.StartsWith($needle) -or $nm.Contains($needle) -or $p.Name -eq $needle){$old=$p.Value;$p.Value=$null;Save-Char $char;Send-Line $writer "Removed $($world.items.$old.name).";return}}}; Send-Line $writer "You are not wearing that." }
function Monster-Attack($writer,$world,$char,$reason=''){
  $room=$world.rooms.($char.room); $mids=To-Array $room.monsters; if($mids.Count -eq 0){return}
  foreach($mid in $mids){ if(-not $world.monsters.PSObject.Properties[$mid]){continue}; $mob=$world.monsters.$mid; $raw=Get-Random -Minimum ([int]$mob.attackMin) -Maximum ([int]$mob.attackMax+1); $dmg=[Math]::Max(1, $raw - [Math]::Floor((Get-Ac $world $char)/3)); $char.hp=[int]$char.hp - $dmg; $msg = if($mob.PSObject.Properties.Name -contains 'attackMessage'){$mob.attackMessage}else{"The $($mob.name) attacks you"}; Send-Line $writer ((C "91" $msg $char.ansi)+" for $dmg damage."); if($char.hp -le 0){ $char.hp=[int]$char.maxHp; $char.room=$world.startRoom; Send-Line $writer (C "91;1" "You are knocked out and wake up back in town." $char.ansi); Show-Room $writer $world $char; break } }
  Save-Char $char
}
function Search-Room($writer,$world,$char){
  $room=$world.rooms.($char.room)
  if($room.PSObject.Properties.Name -contains 'hiddenItems'){
    $hidden=To-Array $room.hiddenItems
    if($hidden.Count -gt 0){ foreach($iid in $hidden){ $room.items=@(To-Array $room.items)+$iid; Send-Line $writer ((C "93" "You found" $char.ansi)+" a $($world.items.$iid.name)!") }; $room.hiddenItems=@(); return }
  }
  if((To-Array $room.monsters).Count -gt 0){ Send-Line $writer "You search carefully, but danger keeps your attention." } else { Send-Line $writer "You search the area but find nothing hidden." }
}
function Show-TrainingMenu($writer,$world,$char){
  $leveling=Load-Leveling
  if(-not $script:TrainActive){
    $script:TrainActive=$true
    $script:TrainDraftStats=Copy-Stats $char.stats
    $script:TrainOriginalStats=Copy-Stats $char.stats
    $script:TrainDraftPoints=[int]$char.trainingPoints
    $script:TrainOriginalPoints=[int]$char.trainingPoints
  }
  Send-Line $writer (C "96;1" $world.messages.trainingHeader $char.ansi)
  Send-Line $writer "Level: $($char.level) / $([int](Get-PropVal $leveling 'maxLevel' 100))   Available Points: $script:TrainDraftPoints"
  Send-Line $writer $world.messages.trainingHelp
  Send-Line $writer ""
  Send-Line $writer ("1) STR Strength      {0,3}  Next Cost: {1}" -f $script:TrainDraftStats.str,(Get-TrainingCost $leveling 'str' $script:TrainDraftStats.str))
  Send-Line $writer ("2) AGI Agility       {0,3}  Next Cost: {1}" -f $script:TrainDraftStats.agi,(Get-TrainingCost $leveling 'agi' $script:TrainDraftStats.agi))
  Send-Line $writer ("3) INT Intellect     {0,3}  Next Cost: {1}" -f $script:TrainDraftStats.int,(Get-TrainingCost $leveling 'int' $script:TrainDraftStats.int))
  Send-Line $writer ("4) WIL Willpower     {0,3}  Next Cost: {1}" -f $script:TrainDraftStats.wil,(Get-TrainingCost $leveling 'wil' $script:TrainDraftStats.wil))
  Send-Line $writer ("5) CHA Charm         {0,3}  Next Cost: {1}" -f $script:TrainDraftStats.cha,(Get-TrainingCost $leveling 'cha' $script:TrainDraftStats.cha))
  Send-Line $writer ("6) CON Constitution  {0,3}  Next Cost: {1}" -f $script:TrainDraftStats.con,(Get-TrainingCost $leveling 'con' $script:TrainDraftStats.con))
  Send-Line $writer ""
  Send-Line $writer "Examples: 2 raises AGI, -2 lowers AGI, save commits, cancel exits, reset starts over."
}
function Adjust-TrainingDraft($writer,$world,$char,$choice){
  if(-not $script:TrainActive){ Show-TrainingMenu $writer $world $char; return }
  $leveling=Load-Leveling
  $map=@{'1'='str';'2'='agi';'3'='int';'4'='wil';'5'='cha';'6'='con'}
  $raw=(Normalize $choice)
  if($raw -eq 'save' -or $raw -eq 's'){
    $oldCon=[int]$char.stats.con
    $char.stats=Copy-Stats $script:TrainDraftStats
    $char.trainingPoints=[int]$script:TrainDraftPoints
    $conGain=[int]$char.stats.con - $oldCon
    if($conGain -ne 0){ $char.maxHp=[int]$char.maxHp + ($conGain*2); $char.hp=[Math]::Min([int]$char.maxHp,[int]$char.hp + ($conGain*2)) }
    Save-Char $char
    $script:TrainActive=$false
    Send-Line $writer (C '92;1' 'Training saved.' $char.ansi)
    Show-Stats $writer $world $char
    return
  }
  if($raw -eq 'cancel' -or $raw -eq 'c' -or $raw -eq 'done' -or $raw -eq 'exit'){
    $script:TrainActive=$false
    Send-Line $writer 'Training canceled. No changes saved.'
    return
  }
  if($raw -eq 'reset' -or $raw -eq 'r'){
    $script:TrainDraftStats=Copy-Stats $script:TrainOriginalStats
    $script:TrainDraftPoints=[int]$script:TrainOriginalPoints
    Show-TrainingMenu $writer $world $char
    return
  }
  $lower=$false
  if($raw.StartsWith('-')){ $lower=$true; $raw=$raw.Substring(1) }
  if(-not $map.ContainsKey($raw)){ Send-Line $writer 'Pick 1-6 to raise, -1 to -6 to lower, save, cancel, or reset.'; return }
  $stat=$map[$raw]
  if($lower){
    if([int]$script:TrainDraftStats.$stat -le [int]$script:TrainOriginalStats.$stat){ Send-Line $writer 'You cannot lower below your saved value.'; return }
    $after=[int]$script:TrainDraftStats.$stat - 1
    $refund=Get-TrainingCost $leveling $stat $after
    $script:TrainDraftStats.$stat=$after
    $script:TrainDraftPoints=[int]$script:TrainDraftPoints + [int]$refund
    Show-TrainingMenu $writer $world $char
    return
  }
  $cost=Get-TrainingCost $leveling $stat $script:TrainDraftStats.$stat
  if([int]$script:TrainDraftPoints -lt [int]$cost){ Send-Line $writer "You need $cost points to raise $($stat.ToUpper())."; return }
  $script:TrainDraftPoints=[int]$script:TrainDraftPoints - [int]$cost
  $script:TrainDraftStats.$stat=[int]$script:TrainDraftStats.$stat + 1
  Show-TrainingMenu $writer $world $char
}
function Train-Player($writer,$world,$char,$arg=''){
  if($char.room -ne 'training_yard'){ Send-Line $writer 'You need to find a trainer first.'; return }
  if([string]::IsNullOrWhiteSpace($arg) -or $arg -eq 'menu' -or $arg -eq 'stats'){ Show-TrainingMenu $writer $world $char; return }
  if($arg -eq 'level' -or $arg -eq 'lvl'){
    $leveling=Load-Leveling
    $maxLevel=[int](Get-PropVal $leveling 'maxLevel' 100)
    if([int]$char.level -ge $maxLevel){ Send-Line $writer "The trainer says, 'You have reached the maximum level.'"; return }
    $next=Next-Xp $world $char.level
    if($null -eq $next){ Send-Line $writer 'The trainer says you are already beyond my lessons.'; return }
    if([int]$char.xp -lt [int]$next){ Send-Line $writer "The trainer says, 'You need $next XP for your next level.'"; return }
    $class=$world.classes.($char.class)
    $char.level=[int]$char.level+1
    $gainPts=Get-PointsForLevel $leveling $char.level
    $char.trainingPoints=[int]$char.trainingPoints + [int]$gainPts
    $gain=[int]$class.levelHp
    $char.maxHp=[int]$char.maxHp+$gain
    $char.hp=[int]$char.maxHp
    Save-Char $char
    Send-Line $writer ((C '93;1' 'LEVEL UP!' $char.ansi)+" You are now level $($char.level). You gained $gainPts training points.")
    Show-TrainingMenu $writer $world $char
    return
  }
  Adjust-TrainingDraft $writer $world $char $arg
}
function Show-Skills($writer,$world,$char){
  $d=Calc-Derived $world $char; $skills=Load-Skills
  Send-Line $writer (C "96;1" "Skills" $char.ansi)
  foreach($sk in @($skills.skills)){ $key=$sk.key; $base=[int](Get-PropVal $d $key 0); Send-Line $writer ("{0,-18} {1,3}%  {2}" -f $sk.name,$base,$sk.description) }
}
function Show-Spells($writer,$world,$char){
  $spells=Load-Spells; $classId=$char.class
  Send-Line $writer (C "96;1" "Spells" $char.ansi)
  $shown=0
  foreach($sp in @($spells.spells)){ if(@($sp.classes) -contains $classId){ Send-Line $writer ("{0,-18} Mana:{1,3}  {2}" -f $sp.name,$sp.mana,$sp.description); $shown++ } }
  if($shown -eq 0){ Send-Line $writer "You do not know any spells yet." }
}
function Show-Stats($writer,$world,$char){
  $next=Next-Xp $world $char.level; $nextText=if($null -eq $next){'max'}else{$next}; $d=Calc-Derived $world $char
  Send-Line $writer (C "96;1" "Character Stats" $char.ansi)
  Send-Line $writer "$($char.name)  $($world.races.($char.race).name) $($world.classes.($char.class).name)"
  Send-Line $writer "Level: $($char.level)   Experience: $($char.xp) / $nextText"
  Send-Line $writer "HP: $($char.hp)/$($char.maxHp)   Mana: 0/0   Moves: 100/100"
  Send-Line $writer "Gold: $($char.gold)   Training Points: $($char.trainingPoints)"
  Send-Line $writer "STR: $($char.stats.str)  AGI: $($char.stats.agi)  INT: $($char.stats.int)  WIL: $($char.stats.wil)  CHA: $($char.stats.cha)  CON: $($char.stats.con)"
  Send-Line $writer "Armor Class: $(Get-Ac $world $char)   Hit Bonus: +$($d.hitBonus)   Damage Bonus: +$($d.damageBonus)"
  Send-Line $writer "Stealth: $($d.stealth)   Perception: $($d.perception)   Search: $($d.search)   Dodge: $($d.dodge)"
  Send-Line $writer "Hide: $($d.hide)   Lockpick: $($d.lockpick)   Tracking: $($d.tracking)   Parry: $($d.parry)   Critical: $($d.critical)"
  Send-Line $writer "Magic: $($d.magic)   Faith: $($d.faith)   Charm: $($d.charm)"
}
function Show-EditorHelp($writer,$world,$arg){
  Send-Line $writer (C "96;1" "Editor Helper Commands" $true)
  Send-Line $writer "edit races    - shows race data file and count"
  Send-Line $writer "edit classes  - shows class data file and count"
  Send-Line $writer "edit skills   - shows skills data file"
  Send-Line $writer "edit spells   - shows spells data file"
  Send-Line $writer "edit rooms    - shows room count"
  if($arg -eq 'races'){ Send-Line $writer ("Race entries: {0}  File: Data\\world.json" -f @($world.races.PSObject.Properties).Count) }
  elseif($arg -eq 'classes'){ Send-Line $writer ("Class entries: {0}  File: Data\\world.json" -f @($world.classes.PSObject.Properties).Count) }
  elseif($arg -eq 'skills'){ Send-Line $writer "Skill data file: Data\\skills.json" }
  elseif($arg -eq 'spells'){ Send-Line $writer "Spell data file: Data\\spells.json" }
  elseif($arg -eq 'rooms'){ Send-Line $writer ("Room entries: {0}  File: Data\\world.json" -f @($world.rooms.PSObject.Properties).Count) }
}

function Set-AdminLevel($writer,$world,$char,$newLevel){
  $leveling=Load-Leveling
  $maxLevel=[int](Get-PropVal $leveling 'maxLevel' 100)
  $lvl=[int]$newLevel
  if($lvl -lt 1){$lvl=1}
  if($lvl -gt $maxLevel){$lvl=$maxLevel}
  $class=$world.classes.($char.class)
  $race=$world.races.($char.race)
  $char.level=$lvl
  $char.trainingPoints=Get-StartingPoints $leveling $lvl
  $newMax=[int]$race.maxHp + [int]$class.hpBonus + (($lvl-1) * [int]$class.levelHp)
  if($newMax -lt 1){$newMax=1}
  $char.maxHp=$newMax
  if([int]$char.hp -gt $newMax -or [int]$char.hp -le 0){$char.hp=$newMax}
  Save-Char $char
  Send-Line $writer "ADMIN: level set to $lvl. Training Points recalculated to $($char.trainingPoints). Max HP is $($char.maxHp)."
}
function Process-Admin($writer,$world,$char,$arg){
  $parts=@($arg.Trim().Split(' ',[System.StringSplitOptions]::RemoveEmptyEntries))
  if($parts.Count -eq 0 -or $parts[0] -eq 'help'){
    Send-Line $writer (C "96;1" "Admin Commands" $char.ansi)
    Send-Line $writer "admin setlevel <1-100>     - set your level and recalc level training points"
    Send-Line $writer "admin addlevel <amount>    - raise/lower level by amount"
    Send-Line $writer "admin xp <amount>          - set XP"
    Send-Line $writer "admin addxp <amount>       - add XP"
    Send-Line $writer "admin points <amount>      - set training points"
    Send-Line $writer "admin addpoints <amount>   - add training points"
    Send-Line $writer "admin gold <amount>        - set gold"
    Send-Line $writer "admin hp <amount>          - set current HP"
    Send-Line $writer "admin maxhp <amount>       - set max HP"
    Send-Line $writer "admin stat <str|agi|int|wil|cha|con> <amount> - set a core stat"
    return
  }
  $cmd=$parts[0].ToLowerInvariant()
  $val=0
  if($cmd -eq 'setlevel' -or $cmd -eq 'level'){
    if($parts.Count -lt 2 -or -not [int]::TryParse($parts[1],[ref]$val)){ Send-Line $writer "Usage: admin setlevel <1-100>"; return }
    Set-AdminLevel $writer $world $char $val
    return
  }
  if($cmd -eq 'addlevel'){
    if($parts.Count -lt 2 -or -not [int]::TryParse($parts[1],[ref]$val)){ Send-Line $writer "Usage: admin addlevel <amount>"; return }
    Set-AdminLevel $writer $world $char ([int]$char.level + $val)
    return
  }
  if($cmd -eq 'xp' -or $cmd -eq 'setxp'){
    if($parts.Count -lt 2 -or -not [int]::TryParse($parts[1],[ref]$val)){ Send-Line $writer "Usage: admin xp <amount>"; return }
    if($val -lt 0){$val=0}; $char.xp=$val; Save-Char $char; Send-Line $writer "ADMIN: XP set to $val."; return
  }
  if($cmd -eq 'addxp'){
    if($parts.Count -lt 2 -or -not [int]::TryParse($parts[1],[ref]$val)){ Send-Line $writer "Usage: admin addxp <amount>"; return }
    $char.xp=[Math]::Max(0,[int]$char.xp+$val); Save-Char $char; Send-Line $writer "ADMIN: XP is now $($char.xp)."; return
  }
  if($cmd -eq 'points' -or $cmd -eq 'trainingpoints'){
    if($parts.Count -lt 2 -or -not [int]::TryParse($parts[1],[ref]$val)){ Send-Line $writer "Usage: admin points <amount>"; return }
    if($val -lt 0){$val=0}; $char.trainingPoints=$val; Save-Char $char; Send-Line $writer "ADMIN: Training Points set to $val."; return
  }
  if($cmd -eq 'addpoints'){
    if($parts.Count -lt 2 -or -not [int]::TryParse($parts[1],[ref]$val)){ Send-Line $writer "Usage: admin addpoints <amount>"; return }
    $char.trainingPoints=[Math]::Max(0,[int]$char.trainingPoints+$val); Save-Char $char; Send-Line $writer "ADMIN: Training Points are now $($char.trainingPoints)."; return
  }
  if($cmd -eq 'gold'){
    if($parts.Count -lt 2 -or -not [int]::TryParse($parts[1],[ref]$val)){ Send-Line $writer "Usage: admin gold <amount>"; return }
    if($val -lt 0){$val=0}; $char.gold=$val; Save-Char $char; Send-Line $writer "ADMIN: Gold set to $val."; return
  }
  if($cmd -eq 'hp'){
    if($parts.Count -lt 2 -or -not [int]::TryParse($parts[1],[ref]$val)){ Send-Line $writer "Usage: admin hp <amount>"; return }
    if($val -lt 1){$val=1}; if($val -gt [int]$char.maxHp){$val=[int]$char.maxHp}; $char.hp=$val; Save-Char $char; Send-Line $writer "ADMIN: HP set to $val/$($char.maxHp)."; return
  }
  if($cmd -eq 'maxhp'){
    if($parts.Count -lt 2 -or -not [int]::TryParse($parts[1],[ref]$val)){ Send-Line $writer "Usage: admin maxhp <amount>"; return }
    if($val -lt 1){$val=1}; $char.maxHp=$val; if([int]$char.hp -gt $val){$char.hp=$val}; Save-Char $char; Send-Line $writer "ADMIN: Max HP set to $val."; return
  }
  if($cmd -eq 'stat'){
    if($parts.Count -lt 3 -or -not [int]::TryParse($parts[2],[ref]$val)){ Send-Line $writer "Usage: admin stat <str|agi|int|wil|cha|con> <amount>"; return }
    $stat=$parts[1].ToLowerInvariant()
    if(@('str','agi','int','wil','cha','con') -notcontains $stat){ Send-Line $writer "Stat must be str, agi, int, wil, cha, or con."; return }
    if($val -lt 1){$val=1}; $char.stats.$stat=$val; Save-Char $char; Send-Line $writer "ADMIN: $($stat.ToUpper()) set to $val."; return
  }
  Send-Line $writer "Unknown admin command. Type admin help."
}

function Handle-Command($writer,$world,$char,$cmd){
  $lc=$cmd.ToLowerInvariant(); $parts=$lc.Split(' ',2); $verb=$parts[0]; $arg=if($parts.Count -gt 1){$parts[1].Trim()}else{''}
  $dirMap=@{n='north';s='south';e='east';w='west';ne='northeast';nw='northwest';se='southeast';sw='southwest';u='up';d='down'}
  if($verb -eq 'quit'){ Send-Line $writer "Goodbye."; return 'quit' }
  if(@('ansi','color','colour') -contains $verb){ $char.ansi=-not [bool]$char.ansi; Save-Char $char; Send-Line $writer "ANSI color is now $($char.ansi)."; return 'ok' }
  if(@('help','h','?') -contains $verb){ Send-Line $writer "Commands: look, search, n/s/e/w/ne/nw/se/sw/u/d, stat/stats/sta/st/sc, skills, spells, i/inv, eq/equipment, get/g, drop, wear/equip/wield/hold, remove/unequip, attack/a/atk/k/kill, use/u, train/menu, level/lvl, exp/xp, say, admin, edit, ansi, quit"; Send-Line $writer "Enter on a blank line refreshes the room. The l shortcut is disabled."; return 'ok' }
  if(@('look','refresh') -contains $verb){ Show-Room $writer $world $char; return 'ok' }
  if($verb -eq 'l'){ Send-Line $writer "The l shortcut is disabled. Type look, or press Enter to refresh the room."; return 'ok' }
  if(@('search','sea','srch') -contains $verb){ Search-Room $writer $world $char; return 'ok' }
  if(@('train','trainer','menu') -contains $verb){ Train-Player $writer $world $char $arg; return 'ok' }
  if(@('level','lvl') -contains $verb){ Train-Player $writer $world $char 'level'; return 'ok' }
  if($script:TrainActive -and (@('1','2','3','4','5','6','-1','-2','-3','-4','-5','-6','save','s','cancel','c','reset','r','done','exit') -contains $verb)){ Adjust-TrainingDraft $writer $world $char $verb; return 'ok' }
  if(@('stat','stats','sta','st','score','sc') -contains $verb){ Show-Stats $writer $world $char; return 'ok' }
  if(@('skill','skills','sk') -contains $verb){ Show-Skills $writer $world $char; return 'ok' }
  if(@('spell','spells','sp') -contains $verb){ Show-Spells $writer $world $char; return 'ok' }
  if($verb -eq 'admin'){ Process-Admin $writer $world $char $arg; return 'ok' }
  if(@('edit','editor') -contains $verb){ Show-EditorHelp $writer $world $arg; return 'ok' }
  if(@('exp','xp') -contains $verb){ $next=Next-Xp $world $char.level; $nextText=if($null -eq $next){'max'}else{$next}; Send-Line $writer "Level: $($char.level)  XP: $($char.xp)  Next: $nextText  Training Points: $($char.trainingPoints)"; return 'ok' }
  if(@('inventory','inv','i') -contains $verb){ Show-Inventory $writer $world $char; return 'ok' }
  if(@('equipment','equip','eq') -contains $verb -and $arg -eq ''){ Show-Equipment $writer $world $char; return 'ok' }
  if($verb -eq 'who'){ Send-Line $writer "Online: $($char.name)"; return 'ok' }
  if($verb -eq 'say' -and $arg){ Send-Line $writer "You say, '$arg'"; return 'ok' }
  if($verb -eq 'go' -and $arg){ $verb=$arg }
  if(($dirMap.ContainsKey($verb)) -or @('north','south','east','west','northeast','northwest','southeast','southwest','up','down','in','out') -contains $verb){ $dir=if($dirMap.ContainsKey($verb)){$dirMap[$verb]}else{$verb}; $room=$world.rooms.($char.room); $exitProp=$room.exits.PSObject.Properties[$dir]; if($null -eq $exitProp){Send-Line $writer "You cannot go that way."}else{$char.room=$exitProp.Value;Save-Char $char;Show-Room $writer $world $char; Monster-Attack $writer $world $char 'enter'}; return 'move' }
  if(@('attack','atk','a','k','kill') -contains $verb){ $room=$world.rooms.($char.room); $mid=Find-MonsterId $world $room $arg; if($null -eq $mid){Send-Line $writer "There is nothing here by that name to attack."}else{$mob=$world.monsters.$mid; $weapon=Slot-Value $char.equipment 'main hand'; $wmin=2; $wmax=6; if($weapon -and $world.items.PSObject.Properties[$weapon]){ $wi=$world.items.$weapon; if($wi.PSObject.Properties.Name -contains 'damageMin'){$wmin=[int]$wi.damageMin}; if($wi.PSObject.Properties.Name -contains 'damageMax'){$wmax=[int]$wi.damageMax} }; $dmg=(Get-Random -Minimum $wmin -Maximum ($wmax+1)) + [Math]::Floor(([int]$char.stats.str-10)/3); if($dmg -lt 1){$dmg=1}; $ret=Get-Random -Minimum ([int]$mob.attackMin) -Maximum ([int]$mob.attackMax+1); $char.hp=[Math]::Max(1,[int]$char.hp-$ret); $char.xp=[int]$char.xp+[int]$mob.xp; $char.gold=[int]$char.gold+[int]$mob.gold; $room.monsters=@((To-Array $room.monsters)|Where-Object{$_ -ne $mid}); Send-Line $writer ((C "91" "You attack" $char.ansi)+" the $($mob.name) for $dmg damage and defeat it."); Send-Line $writer "The $($mob.name) hits you for $ret damage before falling."; Send-Line $writer ((C "93" "You gain" $char.ansi)+" $($mob.xp) XP and $($mob.gold) gold."); Save-Char $char}; return 'combat' }
  if(@('get','g','take') -contains $verb){ $room=$world.rooms.($char.room); if($arg -eq 'all'){$ids=To-Array $room.items}else{$iid=Find-RoomItemId $world $room $arg; $ids=@(); if($iid){$ids=@($iid)}}; if($ids.Count -eq 0){Send-Line $writer "You do not see that here."}else{foreach($iid in $ids){$char.inventory=@(To-Array $char.inventory)+$iid;$room.items=@((To-Array $room.items)|Where-Object{$_ -ne $iid});Send-Line $writer "You pick up $($world.items.$iid.name)."};Save-Char $char}; return 'ok' }
  if(@('wear','wield','wi','hold') -contains $verb -or (($verb -eq 'equip' -or $verb -eq 'eq') -and $arg -ne '')){ Equip-Item $writer $world $char $arg; return 'ok' }
  if(@('remove','unequip','rem','unwear') -contains $verb){ Remove-Item $writer $world $char $arg; return 'ok' }

  if(@('use','u') -contains $verb){ $iid=Find-InventoryItemId $world $char $arg; if($null -eq $iid){Send-Line $writer "You do not have that item."}else{$item=$world.items.$iid; if($item.PSObject.Properties.Name -contains 'heal'){ $old=[int]$char.hp; $char.hp=[Math]::Min([int]$char.maxHp, [int]$char.hp + [int]$item.heal); $char.inventory=@((To-Array $char.inventory)|Where-Object{$_ -ne $iid}); Save-Char $char; Send-Line $writer ((C "92" "You use" $char.ansi)+" $($item.name) and recover $([int]$char.hp-$old) HP.") } else { Send-Line $writer "You cannot use that right now." }}; return 'ok' }

  if(@('drop','dr') -contains $verb){ $iid=Find-InventoryItemId $world $char $arg; if($iid){$char.inventory=@((To-Array $char.inventory)|Where-Object{$_ -ne $iid});$room=$world.rooms.($char.room);$room.items=@(To-Array $room.items)+$iid;Save-Char $char;Send-Line $writer "Dropped $($world.items.$iid.name)."}else{Send-Line $writer "You do not have that."}; return 'ok' }
  Send-Line $writer "Unknown command. Type help."; return 'ok'
}
function Handle-Client($client,$world){
  $remote=$client.Client.RemoteEndPoint.ToString(); Log "CONNECT $remote"
  $stream=$client.GetStream(); $stream.ReadTimeout=30000
  $reader=New-Object System.IO.StreamReader($stream,[Text.Encoding]::ASCII); $writer=New-Object System.IO.StreamWriter($stream,[Text.Encoding]::ASCII); $writer.AutoFlush=$true
  Send-Line $writer (C "92;1" "==========================================" $true); Send-Line $writer (C "96;1" "        MajorMUD Reforged v2.2" $true); Send-Line $writer (C "92;1" "==========================================" $true)
  Send-Line $writer "Enter your character name:"; Prompt $writer $true
  try{$name=$reader.ReadLine()}catch{Log "CLIENT_DISCONNECT_BEFORE_NAME $remote";$client.Close();return}
  Log "RECV name: $name"; if($null -eq $name){$client.Close();return}
  $char=Load-Char $name $world
  if($null -eq $char){
    Send-Line $writer (C "96;1" $world.messages.chooseRace $true)
    $i=1; foreach($p in $world.races.PSObject.Properties){ Send-Line $writer ("  {0}) {1} - {2}" -f $i,$p.Value.name,$p.Value.description); $i++ }
    Prompt $writer $true
    try{$raceLine=$reader.ReadLine()}catch{Log "CLIENT_DISCONNECT_BEFORE_RACE $remote";$client.Close();return}
    Log "RECV race: $raceLine"
    $raceId=Find-RaceId $world $raceLine
    if($null -eq $raceId){ $raceId='human'; Send-Line $writer "Invalid race. Starting as Human for now." }
    Send-Line $writer (C "96;1" $world.messages.chooseClass $true)
    $i=1; foreach($p in $world.classes.PSObject.Properties){ Send-Line $writer ("  {0}) {1} - {2}" -f $i,$p.Value.name,$p.Value.description); $i++ }
    Prompt $writer $true
    try{$classLine=$reader.ReadLine()}catch{Log "CLIENT_DISCONNECT_BEFORE_CLASS $remote";$client.Close();return}
    Log "RECV class: $classLine"
    $classId=Find-ClassId $world $classLine
    if($null -eq $classId){ $classId='warrior'; Send-Line $writer "Invalid class. Starting as Warrior for now." }
    $char=New-Char-WithRaceClass $name $raceId $classId $world
  }
  Send-Line $writer ((C "92" "Welcome," $char.ansi)+" $($char.name) the $($world.races.($char.race).name) $($world.classes.($char.class).name)! Type help for commands.")
  Show-Room $writer $world $char; Prompt $writer $char.ansi
  while($true){
    try{ $line=$reader.ReadLine() }
    catch [System.IO.IOException] { Log "IDLE_REFRESH $remote"; Show-Room $writer $world $char $world.messages.roomRefresh; Monster-Attack $writer $world $char 'idle'; Prompt $writer $char.ansi; continue }
    if($null -eq $line){Log "CLIENT_CLOSED $remote";break}
    $cmd=$line.Trim(); Log "RECV $($char.name): $cmd"
    if($cmd -eq ''){ Show-Room $writer $world $char $world.messages.roomRefresh; Monster-Attack $writer $world $char 'blank'; Prompt $writer $char.ansi; continue }
    $result=Handle-Command $writer $world $char $cmd
    if($result -eq 'quit'){break}
    if($result -ne 'combat' -and $result -ne 'move'){ Monster-Attack $writer $world $char 'turn' }
    Prompt $writer $char.ansi
  }
  try{$client.Close()}catch{}; Log "CLIENT_CLOSED $remote"
}
$world=Load-World
$listener=[System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any,2323)
$listener.Start(); Log "SERVER_READY port 2323"
while($true){Log "WAITING_FOR_CLIENT";$client=$listener.AcceptTcpClient();try{Handle-Client $client $world}catch{Log "CLIENT_ERROR $($_.Exception.Message)";try{$client.Close()}catch{}}}
