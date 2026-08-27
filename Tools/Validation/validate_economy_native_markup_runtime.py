import json,pathlib,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
errors=[]
required=[
 'Apps/Source/Reforged.Server/RuntimeEconomyNativeMarkup.cs',
 'Apps/Source/Reforged.Server/RuntimeClassicEconomyPricing.cs',
 'Apps/Source/Reforged.Server/RuntimeEconomyNativeMarkupSmokeReport.cs',
 'Librarium/Runtime/Economy/native_markup_catalog_0162.json',
 'Librarium/Parity/majormud_economy_evidence_0282.json',
 'Docs/Economy/ECONOMY_NATIVE_MARKUP_RUNTIME_PASS_0162.md',
 'Docs/CODE_UPDATE_0282_MAJORMUD_ECONOMY_TRANSACTIONS_SERVICES_PARITY.md']
for rel in required:
 if not (ROOT/rel).is_file(): errors.append('missing:'+rel)
cat=json.load(open(ROOT/required[3]))
shops=json.load(open(ROOT/'Librarium/Objects/shops/shop_records_0122.json'))['records']
shop_markup={int(x['legacy_id']):int(x['markup_percent']) for x in shops}
if len(cat.get('bindings',[]))!=178: errors.append('binding-count')
for x in cat.get('bindings',[]):
 raw=shop_markup.get(x['legacyShopNumber'])
 if raw!=x['rawMarkupPercent']: errors.append('raw-preservation:'+str(x['legacyShopNumber'])); break
 if x['multiplierBasisPoints']!=(100+raw)*100: errors.append('conversion:'+str(x['legacyShopNumber'])); break
if cat.get('neutralRule','').find('raw value 0')<0: errors.append('neutral-rule')
if 0 not in cat.get('statistics',{}).get('distinctRawValues',[]): errors.append('zero-markup-lost')
cs=(ROOT/required[0]).read_text(); pricing=(ROOT/required[1]).read_text(); world=(ROOT/'Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs').read_text(); listing=(ROOT/'Apps/Source/Reforged.Server/RuntimeLiveGameplayPart6.cs').read_text()
for token in ['NeutralMarkupPercent = 0','(100 + rawMarkupPercent) * 100','StronglyCorrelatedAdditivePercentSemantics']:
 if token not in cs: errors.append('markup-code:'+token)
for token in ['QuoteBuyCopper','QuoteSellCopper','TrainingCostCopper','MidpointRounding.ToEven','4_294_967_295m']:
 if token not in pricing: errors.append('pricing-code:'+token)
for token in ['RuntimeClassicEconomyPricing.QuoteBuyCopper','RuntimeClassicEconomyPricing.QuoteSellCopper','RuntimeClassicEconomyPricing.TrainingCostCopper']:
 if token not in world: errors.append('world-wiring:'+token)
if 'RuntimeClassicEconomyPricing.QuoteBuyCopper' not in listing: errors.append('list-quote-wiring')
if errors:
 print('FAIL',*errors,sep='\n');sys.exit(1)
print('PASS economy native markup correction 0282',len(cat['bindings']))
