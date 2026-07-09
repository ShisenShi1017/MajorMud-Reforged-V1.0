# Character stat model

**Mechanic ID:** `CHAR.STATS`  
**Bucket:** Character Progression  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Core STR/INT/WIL/AGI/HEA/etc. field interpretation and derived calculations.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 14968
- Representative evidence entries: 60
- Source/path coverage: 26

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPasteChar.frm` line 2153 — `sRegexPattern = "(?:(Armour Class|Hits|Mana|Encumbrance):\s*\*?\s*(-?\d+)\/(\d+)|(MagicRes|Level|Spellcasting|Strength|Agility|Willpower|Charm|Intellect|Health):\s*\*?\s*(\d+)|(Name|Race|Class):\s*([^\s:]+(?:\s[^\s:]+)?))"`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 101 — `AddStrip(array('Also here:', 'You hear movement ', ' into the room from ', ' just entered the Realm', ' just left the Realm', ' just hung up', ' peeks in from ', 'the server will be shutting', 'nightly "auto-cleanup"', 'Please finish up and log off', 'Software caused connection abort', 'Session ended', 'or type "new"', 'Enter your password', 'password you have given', 'glad to see you back', 'majo`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4001 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "EXP %" & vbTab & "HP Bonus" & vbTab & "CP" & vbTab & "Min INT" & vbTab & "Min AGL" & vbTab & "Min STR" & vbTab & "Min WIL" & vbTab & "Min CHM" & vbTab & "Min HEA" & vbTab)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` line 70 — `The x87 FPU uses a circular 8-slot stack stored in `CpuCore.FpuStack` (a `double[8]` array). The struct manages the stack pointer and control state:`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 902 — `cephC_DebugPrint "  SpellCost=" & CStr(nSpellCost) & "; SpellOverhead=" & Format$(nSpellOverhead, "0.00")`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 2298 — `'    tabRaces has fields: mSTR/xSTR, mINT/xINT, mWIL/xWIL, mAGL/xAGL, mHEA/xHEA, mCHM/xCHM.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 163 — `-UP: spell calculations will now take energy into account`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 1346 — `Public Property Get nStatBetweenRound_AtkDmgResisted(ByVal Index As Integer) As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 1354 — `Public Property Get nStatHitSpell_AtkDmgResisted(ByVal Index As Integer) As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 1374 — `'Public Property Let nStatAtkDmgResisted(ByVal Index As Integer, ByVal vData As Currency)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 1378 — `Public Property Get nStatAtkDmgResisted(ByVal Index As Integer) As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 176 — `-UP: spell calculations will now take energy into account`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 166 — `.Columns.Append CStr("Attack Min Hit/Cast % " & x), adInteger	==> AttMin-`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 167 — `.Columns.Append CStr("Attack Max Hit/Cast LVL " & x), adInteger	==> AttMax-`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 174 — `.Columns.Append CStr("Spell Cast % " & x), adInteger		==> MidSpell%-`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 175 — `.Columns.Append CStr("Spell Cast LVL " & x), adInteger		==> MidSpellLVL-`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1855 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveKey_Tests.cs` line 18 — `private static byte[] CreateNullPaddedString(string s, int length)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 55 — `CopyModuleToTempPath(ResourceManager.GetTestResourceManager());`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Integration/MBBSEmuIntegrationTestBase.cs` line 86 — `CopyModuleToTempPath(ResourceManager.GetTestResourceManager());`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFileProcessor.cs` line 1117 — `const string insertIntoTableStatement =`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFileProcessor.cs` line 1144 — `const string insertIntoTableStatement =`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` line 80 — `The FPU registers struct maintains a circular stack via `GetStackTop()`/`PushStackTop()`/`PopStackTop()` with 3-bit stack pointer in StatusWord.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` line 206 — `Abstract base class managing user state and I/O. Key elements:`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` line 248 — `- **Int21h** (~1,100 lines): Primary DOS API — file operations (open/close/read/write/seek/create/delete/find), memory management (allocate/free with allocation strategies), DTA, FCB, current drive/directory. Handles STDIN/STDOUT/STDERR + custom file handles.`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
