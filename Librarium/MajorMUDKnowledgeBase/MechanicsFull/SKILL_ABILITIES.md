# Skill/ability mapping

**Mechanic ID:** `SKILL.ABILITIES`  
**Bucket:** Skills and Abilities  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Skill IDs, ability fields, ranks, and derived effects.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 1848
- Representative evidence entries: 45
- Source/path coverage: 20

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 49 — `--This editor gives you a list of all the ability flags in the game by number. Useful for looking up abilities and their description. NOTE: Changes made in the ability editor dont actually effect the game. This editor is only used to resolve ability numbers to names when making edits.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 58 — `--This feature has a variety of functions. You can check any database for any field and perform basic mathmatical functions to the entire database or a range of your choice. You may also limit your result. A very useful tool for making changes to specific fields in an entire database. This editor will add the HP ability to every item that has the stealth ability, or add the picklocks ability to ev`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4944 — `ts.Write (Userrec.Ability(x) & "(" & Userrec.AbilityModifier(x) & ")" & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1855 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1446 — `& "uses and is not set to destroy after the uses expire, the recharge ability value " _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1447 — `& "will be verified against the max uses.  If an item in this case has no recharge ability, " _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1520 — `& " [" & ClipNull(Itemrec.Name) & "] -- Item has " & Itemrec.Uses & " uses, but no recharge ability!")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 4860 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 8272 — `sTemp(1) = AutoAppendString(sTemp(1), Userrec.Ability(x) & "|" & Userrec.AbilityModifier(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 9094 — `txtAbilityB(x).Text = Userrec.AbilityModifier(x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 9835 — `Userrec.AbilityModifier(x) = Val(txtAbilityB(x).Text)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 5498 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 4302 — `'     Penalties or bonuses such as HP per level (Halfling, Half-Ogre), or Ability`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 2860 — `oLI.ListSubItems.Add (1), "Detail", "This is an invalid spell and will not be cast (area attack spells from regular monster casts must use ability 17, Damage-MR)."`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4789 — `sTemp2 = PullSpellEQ(bUseCharacter, tChar.nLevel, nAbilityVal, , , , , , True, , , tChar.nSpellDmgBonus)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 392 — `'this is kind of confusing because after the flag check there are actually 2 text blocks used. each one uses the 511 block, then the others are class specific.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 69 — `6. **Sessions** (`Session/`): `SessionBase` subclasses handle different connection types (Telnet, Rlogin, local console). Sessions are assigned channel numbers and manage user I/O buffers.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/LICENSE` line 8 — `to use, copy, modify, merge, publish, distribute, sublicense, and/or sell`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Assets/MBBSEMU.MSG` line 130 — `comes back to a more normal level. So, users will`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 1083 — `private static byte[] CreateRecord(string username)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 1085 — `var usernameBytes = Encoding.ASCII.GetBytes(username);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 1090 — `Array.Copy(usernameBytes, 0, record, 2, usernameBytes.Length);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 1091 — `record[2 + usernameBytes.Length] = 0;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 213 — `flags = (ushort) (EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.UseExtendedDataType),`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 222 — `flags = (ushort) (EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType),`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
