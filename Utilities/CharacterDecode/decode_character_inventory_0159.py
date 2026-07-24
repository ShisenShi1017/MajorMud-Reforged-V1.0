#!/usr/bin/env python3
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
out=ROOT/'Data/NativeDecodeReports/CodeUpdate0159CharacterInventory/character_inventory_gold_contract_0159.json'
contract={
  'schema_version':1,'code_update':'0159','subsystem':'character.inventory.gold',
  'classification':'STRONGLY CORRELATED','authority':'Librarium',
  'behavior':{
    'representation':'one non-negative 64-bit carried-gold scalar in RuntimeItemState.Gold',
    'operations':['checked credit','checked debit','atomic two-character transfer','corpse escrow and recovery'],
    'conservation':'two-party transfers preserve source+target total exactly',
    'ordering':'validate inputs, compute both post-states, verify conservation, then commit transaction'
  },
  'provenance':['existing persistent RuntimeItemState.Gold field','quest price checks','NPC rewards','player-to-player transfer command','corpse creation and recovery paths'],
  'assumptions':['Gold is indivisible in the currently decoded runtime projection','negative balances are invalid','no denomination conversion is activated'],
  'fallback':'negative values, insufficient funds, non-positive transfers, and arithmetic overflow fail closed before commit',
  'runtime_impact':'centralizes all modified Character reward, transfer, death, and recovery gold arithmetic',
  'save_impact':'no schema change; existing long Gold field remains authoritative',
  'world_state_impact':'corpse escrow remains the only activated Character death gold destination',
  'capacity_quarantine':{
    'classification':'UNKNOWN','behavior':'container metadata named capacity is preserved as inert evidence and no longer enforces a guessed item-count limit'
  },
  'unknown':['native coin denominations','coin weight','bank conversion rules','inventory capacity units','encumbrance thresholds','overflow destinations']
}
out.parent.mkdir(parents=True,exist_ok=True); out.write_text(json.dumps(contract,indent=2)+'\n')
print(out)
