#!/usr/bin/env python3
import argparse, json, re
from pathlib import Path

def require(condition,msg):
    if not condition: raise SystemExit(msg)

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root',required=True); args=ap.parse_args()
    root=Path(args.root)
    runtime=(root/'Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs').read_text()
    contract=json.loads((root/'Data/NativeDecodeReports/CodeUpdate0157CharacterInventory/character_inventory_contract_0157.json').read_text())
    require(contract['code_update']=='0157','wrong contract update')
    require(contract['summary']['classic_item_records']>0,'missing item inventory')
    for token in ['TransferOwnedTree','PropagateContainedOwnership','MergeStack','SplitStack','CONTAINER_OWNER_MISMATCH','MetadataEquivalent']:
        require(token in runtime,f'missing runtime token {token}')
    require('checked(destination.StackQuantity + quantity)' in runtime,'stack overflow is not checked')
    require('Atomic(() =>' in runtime,'atomic rollback contract missing')
    # coarse structural balance after stripping strings/comments enough for generated source
    require(runtime.count('{')==runtime.count('}'),'C# brace mismatch')
    print('Character inventory 0157 validation passed')

if __name__=='__main__': main()
