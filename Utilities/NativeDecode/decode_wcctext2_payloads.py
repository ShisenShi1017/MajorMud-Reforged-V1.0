#!/usr/bin/env python3
from __future__ import annotations
import argparse, hashlib, json, struct
from pathlib import Path
PAGE_SIZE=0x800
RECORD_MARKER=0x5600
PAYLOAD_OFFSET=0x10

def decode(path: Path):
    raw=path.read_bytes()
    if len(raw)%PAGE_SIZE: raise ValueError('WCCTEXT2.DAT size is not page aligned')
    records=[]; seen=set()
    for off in range(0,len(raw),PAGE_SIZE):
        page=raw[off:off+PAGE_SIZE]
        if struct.unpack_from('<H',page,0)[0] != RECORD_MARKER: continue
        tid=struct.unpack_from('<H',page,2)[0]
        if tid<=0 or tid in seen: raise ValueError(f'invalid/duplicate textblock id {tid}')
        seen.add(tid)
        encoded=page[PAYLOAD_OFFSET:]
        while encoded and encoded[-1]==0: encoded=encoded[:-1]
        decoded=bytes((b+0xE0)&0xFF for b in encoded)
        text=decoded.decode('cp437',errors='strict')
        records.append({
            'textblock_id':tid,'payload':text,'page_offset':off,'page_index':off//PAGE_SIZE,
            'record_marker':'0x5600','payload_offset':PAYLOAD_OFFSET,'decode':'byte_plus_0xE0_mod_256',
            'evidence_state':'verified_native_runtime'
        })
    records.sort(key=lambda x:x['textblock_id'])
    return {
      'schema':'majormud.classic.native-textblock-payloads.v1',
      'source':{'file':path.name,'size':len(raw),'sha256':hashlib.sha256(raw).hexdigest(),'page_size':PAGE_SIZE},
      'record_count':len(records),'min_textblock_id':records[0]['textblock_id'],'max_textblock_id':records[-1]['textblock_id'],
      'records':records
    }

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('input',type=Path); ap.add_argument('output',type=Path); a=ap.parse_args()
    data=decode(a.input); a.output.parent.mkdir(parents=True,exist_ok=True); a.output.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    print(f"decoded {data['record_count']} WCCTEXT2 records")
if __name__=='__main__': main()
