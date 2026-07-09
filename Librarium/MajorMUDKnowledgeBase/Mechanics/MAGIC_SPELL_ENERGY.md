# NPC Spell Energy

**Mechanic ID:** `MAGIC_SPELL_ENERGY`

**Domain:** Magic

**Status:** verified_high_confidence

**Confidence:** high

**Evidence hits:** 47

**Unique evidence files:** 14

**Runtime status:** ready_for_test_mapping_before_runtime_sync


## Description

NPC spell casting energy cost, failed/resisted spell half-cost, and double-cast possibility.


## Source agreement

- NightmareRedux: 22 hits
- MMUDExplorer: 25 hits

## Primary buckets

- character_progression: 27
- magic: 8
- tooling_ui: 5
- combat: 3
- items_equipment: 2
- world_spawns: 2

## Reconstruction notes

- NPC failed/resisted spell half-energy and double-spell behavior are high-confidence and should be represented in deterministic tests.

## Top evidence files

- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` (21 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmSpell.frm` (8 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` (3 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` (2 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` (2 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` (2 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/modMMUDFunctions.bas` (2 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` (1 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMain.bas` (1 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/README.md` (1 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 163: -UP: spell calculations will now take energy into account
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 419: Dim nRemainingEnergy As Long, nEnergyUsed As Integer, bShowHitSpell As Boolean
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 176: -UP: spell calculations will now take energy into account
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3398: ts.Write (Spellrec.Energy & vbTab)
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3515: tabSpells.Fields("Energy") = Spellrec.Energy
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2163: Spellrec.Energy = tabSpells.Fields("Energy")
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1855: & "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 2165: tabSpells.Fields("EnergyCost") = 0
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5055: tabSpells.Fields("EnergyCost") = Spellrec.Energy
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 4860: & "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation

## Next action

Convert this record into deterministic tests and runtime/simulator mapping.