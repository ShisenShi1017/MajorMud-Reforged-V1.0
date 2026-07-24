# Items Runtime Semantic Exhaustion — CodeUpdate0167

## Implemented

- Enforced character-carried and unequipped source state for equip, drop, give, and portable-container insertion.
- Rejected broken-item equip attempts unless later authoritative evidence establishes an exception.
- Activated explicit instance restrictions for no-drop, no-give, no-container, no-remove, and cursed-no-remove.
- Added boolean restriction metadata validation.
- Preserved atomic rollback for every rejected transition.
- Added deterministic smoke coverage for restricted transfers and equipment transitions.

## Evidence boundary

These controls are Reforged compatibility infrastructure and strongly correlated lifecycle behavior. No Classic item flag is mapped to these metadata keys in this pass. Missing metadata remains inactive; malformed metadata is rejected by validation.

## Remaining

- Decode native item flag bits that correspond to transfer, removal, curse, binding, quest, and destruction restrictions.
- Recover exact Classic failure messages and exception cases.
- Determine broken-item equip/use/sale behavior from native evidence.
- Recover automatic displacement, layered slots, ring/wrist multiplicity, readied slots, and death/resurrection equipment handling.
