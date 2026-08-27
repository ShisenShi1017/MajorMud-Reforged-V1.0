import json,pathlib,sys
from collections import Counter
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
server=root/'Apps/Source/Reforged.Server'
checks=[]; errors=[]
def require(cond,code):
    (checks if cond else errors).append(code)

pricing_path=server/'RuntimeClassicEconomyPricing.cs'
markup_path=server/'RuntimeEconomyNativeMarkup.cs'
world_path=server/'RuntimeWorldSystemsBundle.cs'
list_path=server/'RuntimeLiveGameplayPart6.cs'
audit_path=server/'RuntimeClassicEconomyParityAudit.cs'
parity_path=server/'RuntimeMajorMudParityAudit.cs'
ledger_path=root/'Librarium/Parity/majormud_parity_assertions_0282.json'
evidence_path=root/'Librarium/Parity/majormud_economy_evidence_0282.json'
markup_catalog_path=root/'Librarium/Runtime/Economy/native_markup_catalog_0162.json'
for p,code in [(pricing_path,'classic-pricing-source'),(markup_path,'markup-source'),(audit_path,'economy-audit-source'),(ledger_path,'ledger-0282'),(evidence_path,'economy-evidence-0282'),(markup_catalog_path,'markup-catalog')]: require(p.exists(),code)

pricing=pricing_path.read_text() if pricing_path.exists() else ''
markup=markup_path.read_text() if markup_path.exists() else ''
world=world_path.read_text(); listing=list_path.read_text(); parity=parity_path.read_text()
for token,code in [
 ('QuoteBuyCopper','buy-quote-authority'),('QuoteSellCopper','sell-quote-authority'),('TrainingCostCopper','training-cost-authority'),
 ('decimal.Truncate(value * markupPercent / 100m)','additive-markup-fix'),('MidpointRounding.ToEven','vb-round-to-even'),('4_294_967_295m','legacy-overflow-boundary'),
 ('(charm / 5) - 10','buy-charm-formula'),('(charm / 2) + 25','sell-charm-formula'),
 ('trainingLevelIndex * 5m * (markupPercent + 100m)','training-markup-plus-100')]: require(token in pricing,code)
for token,code in [('NeutralMarkupPercent = 0','markup-zero-neutral'),('(100 + rawMarkupPercent) * 100','markup-additive-bp'),('StronglyCorrelatedAdditivePercentSemantics','markup-confidence-corrected')]: require(token in markup,code)
for token,code in [('RuntimeClassicEconomyPricing.QuoteBuyCopper','buy-live-wiring'),('RuntimeClassicEconomyPricing.QuoteSellCopper','sell-live-wiring'),('RuntimeClassicEconomyPricing.TrainingCostCopper','train-live-wiring'),('RuntimeBundleCEconomyAuthority.DebitCopperValue(currentItemState, trainingCostCopper)','training-debit-wiring'),('ResolveEconomyCharm(before)','buy-sell-live-charm')]: require(token in world,code)
require('RuntimeClassicEconomyPricing.QuoteBuyCopper' in listing and 'CostTypeCopperMultiplier(ReadItemCostType(generated))' in listing,'list-live-quote-wiring')
require(('majormud_parity_assertions_0283.json' in parity or 'majormud_parity_assertions_0284.json' in parity or 'majormud_parity_assertions_0285.json' in parity or 'majormud_parity_assertions_0286.json' in parity or 'majormud_parity_assertions_0287.json' in parity or 'majormud_parity_assertions_0288.json' in parity),'unified-ledger-current')
require('RuntimeClassicEconomyParityAudit.Run(root)' in parity,'economy-audit-unified')

shops=json.loads((root/'Librarium/Objects/shops/shop_records_0122.json').read_text())['records']
require(len(shops)==178,'shop-count-178')
markups=Counter(int(x['markup_percent']) for x in shops)
require(markups[0]==50,'zero-markup-count-50'); require(markups[100]==44,'hundred-markup-count-44')
require(min(markups)>=0,'shop-markup-nonnegative')
cat=json.loads(markup_catalog_path.read_text())
require(len(cat.get('bindings',[]))==178,'markup-bindings-178')
shop_by={int(x['legacy_id']):int(x['markup_percent']) for x in shops}
for b in cat.get('bindings',[]):
    raw=shop_by.get(int(b['legacyShopNumber']))
    require(raw==b['rawMarkupPercent'],f'raw-markup-preserved:{b["legacyShopNumber"]}')
    require(b['multiplierBasisPoints']==(100+raw)*100,f'additive-bp:{b["legacyShopNumber"]}')
require(0 in cat['statistics']['distinctRawValues'],'catalog-retains-zero-markup')
require('other service-cost stage ordering remains UNKNOWN_FAIL_CLOSED' in cat['orderingState'],'generic-service-order-gated')

coverage=json.loads((root/'Reports/RuntimeVerification/classic_economy_service_coverage.json').read_text())
for key,value in [('ShopCount',178),('BankCount',26),('TrainerCount',44),('InventoryEntryCount',1204),('ClassifiedServiceCount',204),('UnclassifiedServiceCount',0)]: require(coverage.get(key)==value,f'coverage:{key}:{value}')

ledger=json.loads(ledger_path.read_text())
require(ledger.get('update')=='0282','ledger-update-0282')
by={a['id']:a for a in ledger['assertions']}
expected={
 'economy.services-classified':'STRONG','economy.currency-denominations':'STRONG','economy.shop-markup-arithmetic':'STRONG',
 'economy.merchandise-buy-price':'STRONG','economy.merchandise-sell-price':'STRONG','economy.buy-charm-adjustment':'STRONG',
 'economy.sell-charm-adjustment':'STRONG','economy.training-money-cost':'STRONG','economy.shop-transaction-boundary':'STRONG',
 'economy.generic-service-cost-order':'UNKNOWN'}
for i,state in expected.items(): require(by.get(i,{}).get('state')==state,f'ledger:{i}:{state}')
require('economy.native-cost-order' not in by,'broad-economy-unknown-retired')

# Native transaction/transport dependency groups remain explicitly unresolved.
rem=json.loads((root/'Librarium/Reports/COMBINED_DECODE_REMAINING_AUDIT_AFTER_REMAINING_SYSTEMS.json').read_text())
ids={x['id'] for x in rem.get('deduplicatedRemainingSystemsNativeDependencies',[])}
require('player-transactions' in ids,'player-transactions-native-gap-preserved')
require('transport-services' in ids,'transport-native-gap-preserved')

result={'passed':not errors,'check_count':len(checks)+len(errors),'checks':checks,'errors':errors}
out=root/'Reports/RuntimeVerification/economy_transactions_services_pass12_static_validation_0282.json'
out.write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
sys.exit(0 if not errors else 1)
