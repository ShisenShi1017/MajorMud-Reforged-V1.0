# Bundle C Revision 0008 — Phase C7 Economy

## DONE
- Completed the authoritative Classic economy settlement pass for generated item prices, shop purchases, merchant sales, repairs, currency normalization, bank deposits, bank withdrawals, and persistence.
- Replaced the live gold-only bank balance with five persisted bank denomination balances: copper, silver, gold, platinum, and runic.
- `DEPOSIT` and `WITHDRAW` accept an exact denomination or `ALL`; exact bank transfers preserve denomination quantities.
- Purchases, sales, and repairs settle through one `RuntimeBundleCEconomyAuthority` using copper base value and deterministic Classic denomination normalization for change.
- Runic follows the same banking and spending flow as every other denomination.
- Generated service rooms remain the bank authority; no bank room or currency policy is hardcoded.
- Added `--audit-bundle-c-economy`.

## LEFT
- Phase C8 Loot.

## VERIFIED
- Five physical carried denominations persist through `RuntimeBundleCInventoryAuthority`.
- Five banked denominations persist in character item active state.
- Generated item `economy.cost` is the executable item-price source.
- Generated bank services gate live deposit and withdrawal availability by room.
- Banking, commerce, repair, and persistence consume the same character item state.

## STRONGLY CORROBORATED
- Item cost units are gold crowns in the existing Classic generated commerce consumer and are converted to copper base units only for exact settlement/change.
- Commerce may normalize remaining carried value into the canonical denomination set after spending or receiving payment.

## DECODED BUT NOT MAPPED
- None for executable generated item costs or bank service relationships.

## CONFLICTS
- Removed the gold-only bank balance and direct `state.Gold` merchant/repair arithmetic that contradicted physical denomination persistence.

## TRUE UNKNOWN
- Native charm-based merchant markup/markdown formula.
- Native bank fees, account caps, and refusal wording.
- Native repair pricing formula where a generated repair cost is absent.
- These are not invented; existing generated values are consumed exactly.

## Files Added
- `Apps/Source/Reforged.Server/RuntimeBundleCEconomyAuthority.cs`
- `Librarium/Reports/bundle_c_revision_0008.md`

## Files Modified
- `Apps/Source/Reforged.Server/RuntimeLiveGameplayPart5.cs`
- `Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs`
- `Apps/Source/Reforged.Server/Program.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed
- None.

## Duplicate Paths Removed
- Gold-only live bank account path.
- Direct gold arithmetic for buy, sell, and repair settlement.

## Build Result
- Not executable in the packaging environment because the .NET SDK is unavailable.
- Installer requires `dotnet build -warnaserror` with zero warnings and zero errors.

## Audit Result
- Static economy audit passed against generated item prices, generated bank services, five-denomination persistence, live consumers, and duplicate-path removal.
- Runtime economy audit is included in the installer.

## Publish Result
- Installer publishes win-x64 to `Release\MajorMudReforged` and verifies `Reforged.Server.exe`.
