# Guild mechanics

**Mechanic ID:** `SKILL.GUILDS`  
**Bucket:** Skills and Abilities  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Guild membership, abilities, restrictions, and progression.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 20
- Representative evidence entries: 14
- Source/path coverage: 4

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_clsMenuItems.cls` line 82 — `Attribute NewEnum.VB_MemberFlags = "40"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 66 — `Attribute m_colIcons.VB_VarMemberFlags = "440"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 68 — `Attribute m_colTrayIcons.VB_VarMemberFlags = "440"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 166 — `Attribute MainMenu.VB_VarMemberFlags = "400"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 200 — `Attribute RaiseMenuItemClick.VB_MemberFlags = "40"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 216 — `Attribute RaiseChildCreate.VB_MemberFlags = "40"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 233 — `Attribute RaiseChildDestroy.VB_MemberFlags = "40"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 246 — `Attribute RaiseChildMinimize.VB_MemberFlags = "40"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 263 — `Attribute RaiseChildMaximize.VB_MemberFlags = "40"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 280 — `Attribute RaiseChildRestore.VB_MemberFlags = "40"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 297 — `Attribute RaiseChildActivate.VB_MemberFlags = "40"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_ctlTaskBar.ctl` line 2857 — `Attribute MenuButtonIcon.VB_MemberFlags = "400"`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/LICENSE` line 12 — `The above copyright notice and this permission notice shall be included in all`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 217 — `' Membership test against a parsed paste`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
