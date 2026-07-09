# Equipment stat/effect modifiers

**Mechanic ID:** `ITEM.EQUIP_MODIFIERS`  
**Bucket:** Items and Equipment  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
How equipped items alter stats, AC, DR, MR, damage, hit, regen, or abilities.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 4314
- Representative evidence entries: 54
- Source/path coverage: 5

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 1383 — `cmbField.AddItem "HP Bonus", 2`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 1877 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 2039 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 2147 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 2248 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 2293 — `Case 2: Racerec.HPBonus = DoMath(Racerec.HPBonus, 1, 1, sName)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 2388 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 2645 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 2772 — `Do While nStatus = 0 And bStopProcess = False`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27094 — `Call AdjMainStatBonus(tabClasses.Fields("AbilVal-" & x), tabClasses.Fields("Name"), , tEquip.nEquip)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27161 — `Call AdjMainStatBonus(tabRaces.Fields("AbilVal-" & x), tabRaces.Fields("Name"), , tEquip.nEquip)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27508 — `If tEquip.nEquip <> 101 Then Call AdjMainStatBonus(nAbilVal * nMultiQTY, tabItems.Fields("Name") & sCarryText & sMultiQTY, , tEquip.nEquip)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonItemDropPct.frm` line 4 — `Caption         =   "Monster Item Drop Percentage Modifier"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonItemDropPct.frm` line 87 — `Begin VB.ComboBox cmbOnlyIfModifier`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonItemDropPct.frm` line 334 — `Private Sub cmbOnlyIfModifier_Click()`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 26947 — `Private Sub AdjMainStatBonus(ByVal nValue As Long, ByVal sSource As String, Optional ByVal nStatIndex As Integer = -1, Optional ByVal nLabelIndex As Integer = -1)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 26982 — `Call HandleError("AdjMainStatBonus")`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 26989 — `Dim StatTips(0 To TOTAL_STAT_LBLS) As String, rc As RECT, nEncumPCT As Integer, nCritBonus As Long, nAccyCalc As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27136 — `lblInvenCharStat(5).Caption = val(lblInvenCharStat(5).Caption) + nRaceBonus`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27229 — `Call AdjMainStatBonus(10, "Quest: Renfry (10)", 0)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27273 — `Call AdjMainStatBonus(tabItems.Fields("AbilVal-" & x), tabItems.Fields("Name") & " (" & tabItems.Fields("AbilVal-" & x) & ")", , 101)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27306 — `Call AdjMainStatBonus(bless_Stats(x), bless_StatText(x), , x)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27694 — `nStrengthBonus = Fix((val(txtCharStats(0).Tag) - 50) / 10) 'matching how mmud does it 2025.03.13`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27697 — `StatTips(11) = AutoAppend(StatTips(11), "Strength (" & nStrengthBonus & ")", vbCrLf)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/tfs_Tests.cs` line 37 — `private HostProcess.ExportedModules.Majorbbs.TFStateCodes tfsrdl()`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
