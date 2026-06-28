
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
function Get-ProjectRoot {
  $p = Split-Path -Parent $MyInvocation.MyCommand.Path
  # Editor folder: Root\HostEditor-PC\Editor
  return (Resolve-Path (Join-Path $p '..\..')).Path
}
$ProjectRoot = Get-ProjectRoot
$DataRoot = Join-Path $ProjectRoot 'Data'
if(-not (Test-Path $DataRoot)){ New-Item -ItemType Directory -Force $DataRoot | Out-Null }
$script:Records = @(); $script:CurrentFile = $null; $script:CurrentIndex = -1; $script:JsonFiles = @()
function Load-JsonFiles {
  $script:JsonFiles = @(Get-ChildItem $DataRoot -Recurse -Filter '*.json' | Where-Object { $_.FullName -notmatch '\\Backups\\' } | Sort-Object FullName)
  $catList.Items.Clear()
  foreach($f in $script:JsonFiles){ $rel=$f.FullName.Substring($DataRoot.Length).TrimStart('\'); [void]$catList.Items.Add($rel) }
}
function Display-Name($o){
  if($null -eq $o){ return '<null>' }
  foreach($k in @('Name','name','Title','title','ItemName','MobName','RoomName','SpellName','Class','Race','ID','Id','id','Number','VNum')){
    if($o.PSObject.Properties.Name -contains $k -and $null -ne $o.$k -and "$($o.$k)" -ne ''){ return "$($o.$k)" }
  }
  return ($o | ConvertTo-Json -Compress -Depth 3).Substring(0,[Math]::Min(80,($o | ConvertTo-Json -Compress -Depth 3).Length))
}
function Load-Category($idx){
  if($idx -lt 0 -or $idx -ge $script:JsonFiles.Count){ return }
  $script:CurrentFile = $script:JsonFiles[$idx].FullName
  try { $raw = Get-Content $script:CurrentFile -Raw; $obj = $raw | ConvertFrom-Json; $script:Records = @($obj) } catch { $script:Records=@(); [System.Windows.Forms.MessageBox]::Show("Could not load JSON: $($_.Exception.Message)") }
  $recordList.Items.Clear()
  for($i=0;$i -lt $script:Records.Count;$i++){ [void]$recordList.Items.Add(("{0}: {1}" -f ($i+1),(Display-Name $script:Records[$i]))) }
  $fileLabel.Text = "File: " + $script:CurrentFile.Replace($ProjectRoot,'.') + "   Records: " + $script:Records.Count
  if($recordList.Items.Count -gt 0){ $recordList.SelectedIndex = 0 } else { $jsonBox.Text='' }
}
function Show-Record($idx){
  $script:CurrentIndex=$idx
  if($idx -ge 0 -and $idx -lt $script:Records.Count){ $jsonBox.Text = ($script:Records[$idx] | ConvertTo-Json -Depth 80) } else { $jsonBox.Text='' }
}
function Save-CurrentRecord {
  if($script:CurrentIndex -lt 0){ return }
  try { $new = $jsonBox.Text | ConvertFrom-Json; $script:Records[$script:CurrentIndex] = $new; $recordList.Items[$script:CurrentIndex] = ("{0}: {1}" -f ($script:CurrentIndex+1),(Display-Name $new)); Save-File } catch { [System.Windows.Forms.MessageBox]::Show("Invalid JSON: $($_.Exception.Message)") }
}
function Save-File {
  if(-not $script:CurrentFile){ return }
  $script:Records | ConvertTo-Json -Depth 80 | Set-Content $script:CurrentFile -Encoding UTF8
  $status.Text = "Saved " + (Get-Date).ToString('T')
}
function Backup-Data {
  $broot = Join-Path $ProjectRoot 'Backups'; New-Item -ItemType Directory -Force $broot | Out-Null
  $dest = Join-Path $broot ('DataBackup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
  Copy-Item $DataRoot $dest -Recurse -Force
  [System.Windows.Forms.MessageBox]::Show("Backup created:`n$dest")
}
$form = New-Object System.Windows.Forms.Form
$form.Text = 'MajorMUD Reforged Editor - Redux Style Shared Data'
$form.Width = 1200; $form.Height=760; $form.StartPosition='CenterScreen'
$split = New-Object System.Windows.Forms.SplitContainer
$split.Dock='Fill'; $split.SplitterDistance=270
$form.Controls.Add($split)
$left = New-Object System.Windows.Forms.TableLayoutPanel
$left.Dock='Fill'; $left.RowCount=4; $left.ColumnCount=1
$left.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute,28)))
$left.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent,45)))
$left.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute,28)))
$left.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent,55)))
$split.Panel1.Controls.Add($left)
$lbl1=New-Object System.Windows.Forms.Label; $lbl1.Text='Database files (main Data folder)'; $lbl1.Dock='Fill'
$left.Controls.Add($lbl1,0,0)
$catList=New-Object System.Windows.Forms.ListBox; $catList.Dock='Fill'
$left.Controls.Add($catList,0,1)
$lbl2=New-Object System.Windows.Forms.Label; $lbl2.Text='Records'; $lbl2.Dock='Fill'
$left.Controls.Add($lbl2,0,2)
$recordList=New-Object System.Windows.Forms.ListBox; $recordList.Dock='Fill'
$left.Controls.Add($recordList,0,3)
$right=New-Object System.Windows.Forms.TableLayoutPanel
$right.Dock='Fill'; $right.RowCount=4; $right.ColumnCount=1
$right.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute,32)))
$right.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute,42)))
$right.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent,100)))
$right.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute,24)))
$split.Panel2.Controls.Add($right)
$fileLabel=New-Object System.Windows.Forms.Label; $fileLabel.Dock='Fill'; $fileLabel.Text='File:'
$right.Controls.Add($fileLabel,0,0)
$buttons=New-Object System.Windows.Forms.FlowLayoutPanel; $buttons.Dock='Fill'
$right.Controls.Add($buttons,0,1)
function Btn($t){ $b=New-Object System.Windows.Forms.Button; $b.Text=$t; $b.Width=92; $b.Height=28; [void]$buttons.Controls.Add($b); return $b }
$bNew=Btn 'Add New'; $bClone=Btn 'Clone'; $bDelete=Btn 'Delete'; $bSave=Btn 'Save Record'; $bReload=Btn 'Reload'; $bBackup=Btn 'Backup Data'; $bDict=Btn 'Field Help'
$jsonBox=New-Object System.Windows.Forms.TextBox
$jsonBox.Multiline=$true; $jsonBox.ScrollBars='Both'; $jsonBox.AcceptsReturn=$true; $jsonBox.AcceptsTab=$true; $jsonBox.WordWrap=$false; $jsonBox.Dock='Fill'; $jsonBox.Font=New-Object System.Drawing.Font('Consolas',10)
$right.Controls.Add($jsonBox,0,2)
$status=New-Object System.Windows.Forms.Label; $status.Text='Ready. Edits save to the main Data folder.'; $status.Dock='Fill'
$right.Controls.Add($status,0,3)
$catList.Add_SelectedIndexChanged({ Load-Category $catList.SelectedIndex })
$recordList.Add_SelectedIndexChanged({ Show-Record $recordList.SelectedIndex })
$bSave.Add_Click({ Save-CurrentRecord })
$bReload.Add_Click({ Load-JsonFiles; if($catList.Items.Count -gt 0){ $catList.SelectedIndex=0 } })
$bBackup.Add_Click({ Backup-Data })
$bNew.Add_Click({
  $new=[pscustomobject]@{ id=('new_' + [guid]::NewGuid().ToString('N').Substring(0,8)); name='New Record'; notes='Edit this JSON, then click Save Record.' }
  $script:Records += $new; [void]$recordList.Items.Add(("{0}: {1}" -f $script:Records.Count,(Display-Name $new))); $recordList.SelectedIndex=$script:Records.Count-1; Save-File
})
$bClone.Add_Click({
  if($script:CurrentIndex -ge 0){ $clone = ($script:Records[$script:CurrentIndex] | ConvertTo-Json -Depth 80 | ConvertFrom-Json); if($clone.PSObject.Properties.Name -contains 'id'){ $clone.id = "$($clone.id)_copy" }; if($clone.PSObject.Properties.Name -contains 'name'){ $clone.name = "$($clone.name) Copy" }; $script:Records += $clone; [void]$recordList.Items.Add(("{0}: {1}" -f $script:Records.Count,(Display-Name $clone))); $recordList.SelectedIndex=$script:Records.Count-1; Save-File }
})
$bDelete.Add_Click({
  if($script:CurrentIndex -ge 0 -and [System.Windows.Forms.MessageBox]::Show('Delete selected record?','Confirm','YesNo') -eq 'Yes'){
    $list = New-Object System.Collections.ArrayList; [void]$list.AddRange($script:Records); $list.RemoveAt($script:CurrentIndex); $script:Records=@($list); Load-Category $catList.SelectedIndex; Save-File
  }
})
$bDict.Add_Click({
  $p=Join-Path $DataRoot 'metadata\README_FIELD_MEANINGS.txt'; if(Test-Path $p){ Start-Process notepad.exe $p } else { [System.Windows.Forms.MessageBox]::Show('Run the v3.7 import patch first to generate field help.') }
})
Load-JsonFiles
if($catList.Items.Count -gt 0){ $catList.SelectedIndex=0 }
[void]$form.ShowDialog()
