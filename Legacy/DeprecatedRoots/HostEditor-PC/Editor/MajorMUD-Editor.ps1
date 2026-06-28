Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$configPath = Join-Path $root 'config.json'
$config = Get-Content $configPath -Raw | ConvertFrom-Json
$dataPath = Join-Path $root $config.dataPath
$form = New-Object System.Windows.Forms.Form
$form.Text = 'MajorMUD Reforged Editor - Redux Simple'
$form.Size = New-Object System.Drawing.Size(1000,700)
$form.StartPosition = 'CenterScreen'
$tabs = New-Object System.Windows.Forms.TabControl
$tabs.Dock = 'Fill'
$form.Controls.Add($tabs)
$entities = @('Rooms','Items','Monsters','Races','Classes','Skills','Spells','Messages')
function Load-JsonList($folder){
  $p = Join-Path $dataPath $folder
  if(!(Test-Path $p)){ New-Item -ItemType Directory -Path $p -Force | Out-Null }
  Get-ChildItem $p -Filter *.json -ErrorAction SilentlyContinue | ForEach-Object {
    try { [pscustomobject]@{ Name=$_.BaseName; Path=$_.FullName; Text=(Get-Content $_.FullName -Raw) } } catch {}
  }
}
foreach($entity in $entities){
  $tab = New-Object System.Windows.Forms.TabPage
  $tab.Text = $entity
  $list = New-Object System.Windows.Forms.ListBox
  $list.Width = 250
  $list.Dock = 'Left'
  $editor = New-Object System.Windows.Forms.TextBox
  $editor.Multiline = $true
  $editor.ScrollBars = 'Both'
  $editor.AcceptsReturn = $true
  $editor.AcceptsTab = $true
  $editor.Font = New-Object System.Drawing.Font('Consolas',10)
  $editor.Dock = 'Fill'
  $panel = New-Object System.Windows.Forms.Panel
  $panel.Height = 42
  $panel.Dock = 'Top'
  $btnReload = New-Object System.Windows.Forms.Button
  $btnReload.Text='Reload'; $btnReload.Left=5; $btnReload.Width=75
  $btnSave = New-Object System.Windows.Forms.Button
  $btnSave.Text='Save'; $btnSave.Left=85; $btnSave.Width=75
  $btnNew = New-Object System.Windows.Forms.Button
  $btnNew.Text='New'; $btnNew.Left=165; $btnNew.Width=75
  $btnDelete = New-Object System.Windows.Forms.Button
  $btnDelete.Text='Delete'; $btnDelete.Left=245; $btnDelete.Width=75
  $panel.Controls.AddRange(@($btnReload,$btnSave,$btnNew,$btnDelete))
  $right = New-Object System.Windows.Forms.Panel
  $right.Dock='Fill'
  $right.Controls.Add($editor)
  $right.Controls.Add($panel)
  $tab.Controls.Add($right)
  $tab.Controls.Add($list)
  $tabs.TabPages.Add($tab)
  $script:currentItems = @{}
  $reload = {
    $list.Items.Clear()
    $items = Load-JsonList $entity
    foreach($it in $items){ [void]$list.Items.Add($it.Name) }
  }
  $list.Add_SelectedIndexChanged({
    if($list.SelectedItem){
      $file = Join-Path (Join-Path $dataPath $entity) ($list.SelectedItem.ToString()+'.json')
      if(Test-Path $file){ $editor.Text = Get-Content $file -Raw }
    }
  })
  $btnReload.Add_Click($reload)
  $btnSave.Add_Click({
    if(!$list.SelectedItem){ [System.Windows.Forms.MessageBox]::Show('Select or create an entry first.'); return }
    $file = Join-Path (Join-Path $dataPath $entity) ($list.SelectedItem.ToString()+'.json')
    $editor.Text | Set-Content -Path $file -Encoding UTF8
    [System.Windows.Forms.MessageBox]::Show('Saved '+$list.SelectedItem)
  })
  $btnNew.Add_Click({
    $name = [Microsoft.VisualBasic.Interaction]::InputBox('New '+$entity+' id/name:', 'New '+$entity, 'new_'+$entity.ToLower())
    if([string]::IsNullOrWhiteSpace($name)){ return }
    $folder = Join-Path $dataPath $entity
    if(!(Test-Path $folder)){ New-Item -ItemType Directory -Path $folder -Force | Out-Null }
    $file = Join-Path $folder ($name+'.json')
    if(Test-Path $file){ [System.Windows.Forms.MessageBox]::Show('Already exists.'); return }
    '{ "id": "'+$name+'", "name": "'+$name+'" }' | Set-Content -Path $file -Encoding UTF8
    & $reload
  })
  $btnDelete.Add_Click({
    if(!$list.SelectedItem){ return }
    $file = Join-Path (Join-Path $dataPath $entity) ($list.SelectedItem.ToString()+'.json')
    if([System.Windows.Forms.MessageBox]::Show('Delete '+$list.SelectedItem+'?','Confirm','YesNo') -eq 'Yes'){
      Remove-Item $file -Force
      $editor.Clear()
      & $reload
    }
  })
  & $reload
}
[void]$form.ShowDialog()
