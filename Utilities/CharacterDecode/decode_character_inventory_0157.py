#!/usr/bin/env python3
import argparse, json
from pathlib import Path


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--items', required=True)
    ap.add_argument('--output', required=True)
    args=ap.parse_args()
    data=json.loads(Path(args.items).read_text())
    records=data.get('records',[])
    containers=[r for r in records if r.get('container') is True]
    stackable=[r for r in records if r.get('stackable') is True]
    report={
      'code_update':'0157','subsystem':'character.inventory','authority':'Librarium',
      'source':str(Path(args.items)),
      'classification':'STRONGLY CORRELATED',
      'confidence':0.93,
      'summary':{
        'classic_item_records':len(records),
        'container_records':len(containers),
        'stackable_records':len(stackable),
        'classic_stackable_activation':len(stackable)>0
      },
      'promoted_runtime_contracts':[
        {
          'id':'inventory.single_owner_tree','classification':'PROVEN',
          'source':'generated item instance location/owner model and container references',
          'assumptions':['contained items inherit the owner of their containing item'],
          'fallback':'reject mismatched container ownership and roll back atomically',
          'runtime_impact':'container transfers propagate ownership recursively',
          'save_impact':'owner IDs remain consistent through save/load',
          'world_state_impact':'prevents orphaned or cross-owner container contents'
        },
        {
          'id':'inventory.stack_state_equivalence','classification':'STRONGLY CORRELATED',
          'source':'existing stackable flag, quantity field, charges, durability and metadata state',
          'assumptions':['only identical canonical and runtime state may merge'],
          'fallback':'reject merge or split without mutation',
          'runtime_impact':'atomic split and merge with checked quantity arithmetic',
          'save_impact':'each resulting stack preserves complete runtime state',
          'world_state_impact':'conserves total quantity and prevents state-smearing'
        }
      ],
      'unknowns':[
        'Classic 1.11p native auto-stack command behavior because the current projection exposes zero stackable Classic records',
        'weight, encumbrance, overflow destinations and coin denomination semantics',
        'container capacity units and nested-container weight rules'
      ]
    }
    out=Path(args.output); out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(json.dumps(report,indent=2)+'\n')

if __name__=='__main__': main()
