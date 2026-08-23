param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))

$source = Join-Path $ProjectRoot 'ImportedDatabases/WCCDefaultInstall/wcc default/wccupda2.dat'
$output = Join-Path $ProjectRoot 'Librarium/Objects/invasion_scripts/wcc_update_invasion_script_records_0139.json'
$raw = [IO.File]::ReadAllBytes($source)
$encoding = [Text.Encoding]::GetEncoding(437)
$specs = @(
    @(4368,6004,38),@(4369,6005,10),@(4370,6006,38),@(4371,6007,10),@(4372,6008,38),
    @(4646,6286,38),@(4647,6287,10),@(4648,6288,38),@(4649,6289,10),@(4650,6290,38),
    @(4651,6291,10),@(4652,6292,38),@(4653,6293,10),@(4654,6294,38),@(4655,6295,10),
    @(4656,6296,38),@(4658,6298,38),@(4659,6299,10),@(4660,6300,38),@(4661,6301,10),
    @(4662,6302,38),@(4663,6303,10),@(4664,6304,38),@(4665,6305,10),@(4666,6306,38),
    @(4667,6307,10),@(4668,6308,38),@(4669,6309,10),@(4670,6310,38),@(4671,6311,10),@(4672,6312,38)
)
$records = foreach($spec in $specs) {
    $id=[int]$spec[0]; $page=[int]$spec[1]; $payloadOffset=[int]$spec[2]; $start=$page*0x800+$payloadOffset
    $decoded=[byte[]]::new(0x800-$payloadOffset)
    for($i=0;$i-lt$decoded.Length;$i++){$decoded[$i]=[byte](($raw[$start+$i]+0xE0)-band 0xff)}
    $script=$encoding.GetString($decoded)
    $end=$script.IndexOf("`n`n")
    if($end-lt0){throw "Invasion script $id has no decoded terminator."}
    $script=$script.Substring(0,$end).Trim()
    if($script.Length-eq0){throw "Invasion script $id decoded empty."}
    [ordered]@{id="invasion-script.$id";object_type='invasion_script';world_scope=@('classic');textblock_id=$id;script=$script;source_page=$page;payload_offset=$payloadOffset;decode='byte_plus_0xE0_mod_256';evidence_state='verified_wcc_update_database'}
}
$document=[ordered]@{schema='majormud.reforged.invasion-scripts.v1';source_file='ImportedDatabases/WCCDefaultInstall/wcc default/wccupda2.dat';record_count=$records.Count;records=$records}
[IO.Directory]::CreateDirectory((Split-Path -Parent $output))|Out-Null
[IO.File]::WriteAllText($output,(($document|ConvertTo-Json -Depth 8)+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
Write-Host "Promoted $($records.Count) WCC invasion script records."
