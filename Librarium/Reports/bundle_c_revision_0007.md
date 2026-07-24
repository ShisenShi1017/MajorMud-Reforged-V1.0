# Bundle C Revision 0007 — Phase C6 Merchants

## DONE
- Completed the generated Classic merchant authority pass across shops, trainers, spell shops, special/quest vendors, stock, buy, sell, buyback, and persistence.
- Live BUY and SELL now accept explicit quantities and execute inside the existing atomic persistent shop-stock transaction.
- SELL now routes through `RuntimeItemEngine.SellToMerchant`, enforcing decoded no-sell, quest-protected, ownership, stack, and transfer restrictions instead of deleting the carried item directly.
- Sold canonical items are retained in persistent shop stock as buyback inventory, including items not in the shop's normal generated inventory.
- Generated shop inventory remains data-driven from `Data/Classic/shops.json`; no inventory was hardcoded.
- Trainer services remain owned by the Bundle A progression consumer and enforce generated room/class/level relationships.
- Spell vendors remain generated shop inventories whose scrolls resolve through decoded LearnSp relationships.
- Jungle Gnome Merchant NPC `npc.716`, textblock `2798`, is validated as the connected atomic level-40 class-scroll turn-in authority.
- Added `--audit-bundle-c-merchants`.

## LEFT
- Phase C7 Economy: exact price policies, markup/markdown, charm effects, repair valuation, denomination settlement, conversion, and banking economy validation.

## VERIFIED
- 178 generated Classic shop records are present in the authoritative projection.
- Every executable shop item relationship must resolve to a canonical runtime item.
- Persistent stock is owned by `RuntimeGameplayCompletionStateStore`.
- Item ownership transfer restrictions are owned by `RuntimeItemEngine`.
- Jungle Gnome Merchant level-40 reward branches use NPC 716 and textblock 2798.

## STRONGLY CORROBORATED
- Sold items enter shop stock and are available as buyback inventory.
- Shops and trainers are room-owned services; ambient merchant NPC presence is not required unless the decoded service explicitly uses the NPC turn-in path.

## DECODED BUT NOT MAPPED
- None for executable merchant inventory relationships.

## CONFLICTS
- Removed the contradictory Bundle A audit assertion that the Jungle Gnome turn-in script was unresolved while the same catalog marked it resolved.

## TRUE UNKNOWN
- Exact native merchant wallet funding limits and refusal wording where no generated relationship exists.
- Exact charm-based pricing belongs to Phase C7 and is not invented here.

## Files Added
- `Apps/Source/Reforged.Server/RuntimeBundleCMerchantAudit.cs`
- `Librarium/Reports/bundle_c_revision_0007.md`

## Files Modified
- `Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs`
- `Apps/Source/Reforged.Server/RuntimeCharacterDecodeBundleAudit.cs`
- `Apps/Source/Reforged.Server/Program.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed
- None.

## Duplicate Paths Removed
- Direct sell-time item deletion bypassing authoritative merchant-transfer restrictions.
- Contradictory unresolved Jungle Gnome audit path.

## Build Result
- Not executable in the packaging environment because the .NET SDK is unavailable.
- Installer requires `dotnet build -warnaserror` with zero warnings and zero errors.

## Audit Result
- Static generated-data audit passed: 178 shops and canonical executable inventory references.
- Runtime merchant audit is included in the installer.

## Publish Result
- Installer publishes win-x64 to `Release\MajorMudReforged` and verifies `Reforged.Server.exe`.
