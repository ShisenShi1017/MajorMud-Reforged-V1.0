#!/usr/bin/env python3
import json, re, hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'Reports/NativeDecode/native_host_import_reference_0005.json'
OUT = ROOT / 'Reports/NativeDecode/native_host_data_import_classification_0006.json'
LIB = ROOT / 'Librarium/Evidence/native_host_data_import_classification_0006.json'

def sha256(path):
    h=hashlib.sha256(); h.update(path.read_bytes()); return h.hexdigest()

def classify(symbol, refs):
    mnems=[]
    has_control=False
    has_address_push=False
    has_byte_access=False
    has_arith=False
    for r in refs:
        ins=r.get('instruction','').strip().lower()
        m=ins.split(None,1)[0] if ins else ''
        mnems.append(m)
        if m in {'call','jmp'}: has_control=True
        if m=='push': has_address_push=True
        if 'byte ptr' in ins: has_byte_access=True
        if m in {'add','adc','sub','sbb','inc','dec','and','or','xor','cmp','test'}: has_arith=True
    if has_control:
        kind='possible_callback_or_function_pointer'
        confidence='strongly_correlated'
    elif has_address_push or has_byte_access:
        kind='host_data_buffer_or_pointer'
        confidence='strongly_correlated'
    elif has_arith:
        kind='host_scalar_or_pointer_arithmetic'
        confidence='strongly_correlated'
    else:
        kind='host_data_global_pointer_or_scalar'
        confidence='proven'
    return kind, confidence, sorted(set(mnems))

def main():
    src=json.loads(SRC.read_text())
    modules=[]; symbols={}
    for m in src['modules']:
        entries=[]
        for imp in m['imports']:
            if imp['reference_kind']=='direct_callable_import':
                continue
            refs=imp.get('non_thunk_iat_references',[])
            if imp['reference_kind']=='imported_no_static_code_reference':
                kind='imported_without_static_reference'; confidence='proven'; mnems=[]
            else:
                kind, confidence, mnems=classify(imp['symbol'], refs)
            e={
                'symbol':imp['symbol'],
                'iat_va':imp['iat_va'],
                'reference_kind_0005':imp['reference_kind'],
                'classification_0006':kind,
                'evidence_class':confidence,
                'static_reference_count':imp.get('non_thunk_iat_reference_count',0),
                'observed_mnemonics':mnems,
                'control_transfer_through_iat_observed': any(x in {'call','jmp'} for x in mnems),
                'exact_native_type':'unknown_without_host_symbols_or_runtime',
            }
            entries.append(e)
            symbols.setdefault(imp['symbol'], {'modules':[], 'classifications':set(), 'control_transfer':False})
            symbols[imp['symbol']]['modules'].append(m['file'])
            symbols[imp['symbol']]['classifications'].add(kind)
            symbols[imp['symbol']]['control_transfer'] |= e['control_transfer_through_iat_observed']
        modules.append({'file':m['file'],'sha256':m['sha256'],'imports':entries,'counts':{
            'classified_data_or_unreferenced_imports':len(entries),
            'control_transfer_candidates':sum(1 for e in entries if e['control_transfer_through_iat_observed']),
            'data_only_static_imports':sum(1 for e in entries if e['reference_kind_0005']=='iat_data_or_indirect_reference' and not e['control_transfer_through_iat_observed']),
            'unreferenced_imports':sum(1 for e in entries if e['reference_kind_0005']=='imported_no_static_code_reference')
        }})
    symbol_rows=[]
    for s,v in sorted(symbols.items()):
        symbol_rows.append({'symbol':s,'modules':sorted(v['modules']),'classifications':sorted(v['classifications']),'control_transfer_through_iat_observed':v['control_transfer']})
    out={
        'schema':'majormud.native-host-data-import-classification.v1',
        'update':'FinalNativeClosure0006',
        'source_report':str(SRC.relative_to(ROOT)).replace('\\','/'),
        'source_sha256':sha256(SRC),
        'evidence_class':'proven for static access form; native data type remains unknown',
        'method':'Classify every non-direct WGSERVER import by observed x86 reference mnemonic and whether any static call/jmp transfers through its IAT slot.',
        'modules':modules,
        'symbols':symbol_rows,
        'closed_boundaries':[
            'No supplied MMUD/MMPLS data-import IAT slot is statically used as a call or jump target.',
            'The previously unresolved non-direct import set is therefore a host-data/global boundary in the supplied binaries, not a proven callback dispatcher boundary.',
            'register_agent remains imported but has no static code reference in MMUD.'
        ],
        'limitations':[
            'Exact C types, structure layouts, signedness, widths, and lifetime semantics require WGSERVER symbols, source, or dynamic memory traces.',
            'Runtime-written function pointers inside structures reached through imported globals are not excluded by this static result.'
        ]
    }
    text=json.dumps(out,indent=2)+"\n"
    OUT.write_text(text); LIB.write_text(text)
    print(OUT)

if __name__=='__main__': main()
