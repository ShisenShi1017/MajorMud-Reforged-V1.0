#!/usr/bin/env python3
"""
Reforged Native DAT Decoder / Inspector

This tool performs safe structural inspection of Reforged runtime .DAT files.
It does not modify source files. It is intentionally conservative: unknown
bytes are preserved and reported instead of guessed away.
"""
import argparse, csv, hashlib, json, os, re
from pathlib import Path

PRINTABLE_RE = re.compile(rb'[\x20-\x7E]{4,}')

def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()

def candidate_blocks(size: int):
    blocks = []
    for block_size in [64, 96, 128, 153, 176, 192, 256, 400, 440, 512, 696, 720, 1024, 2048, 4096]:
        if size % block_size == 0:
            blocks.append({'blockSize': block_size, 'blockCount': size // block_size})
    return blocks

def extract_strings(data: bytes, limit: int | None = None):
    rows = []
    for i, m in enumerate(PRINTABLE_RE.finditer(data)):
        if limit is not None and i >= limit:
            break
        rows.append({'offset': m.start(), 'length': len(m.group()), 'text': m.group().decode('latin1', 'replace')})
    return rows

def block_samples(data: bytes, block_size: int, limit: int = 24):
    out = []
    count = min(limit, len(data) // block_size)
    for i in range(count):
        rec = data[i*block_size:(i+1)*block_size]
        ascii_preview = ''.join(chr(c) if 32 <= c < 127 else '.' for c in rec[:160])
        out.append({'index': i, 'offset': i * block_size, 'hexFirst64': rec[:64].hex(' '), 'asciiFirst160': ascii_preview})
    return out

def inspect_file(path: Path, outdir: Path):
    data = path.read_bytes()
    outdir.mkdir(parents=True, exist_ok=True)
    info = {
        'file': path.name,
        'size': len(data),
        'sha256': sha256(path),
        'nonZeroBytes': sum(b != 0 for b in data),
        'asciiStringCount': len(PRINTABLE_RE.findall(data)),
        'candidateBlockSizes': candidate_blocks(len(data)),
    }
    (outdir / f'{path.stem}_summary.json').write_text(json.dumps(info, indent=2), encoding='utf-8')
    strings = extract_strings(data)
    with (outdir / f'{path.stem}_strings.tsv').open('w', encoding='utf-8', newline='') as f:
        f.write('offset\tlength\ttext\n')
        for row in strings:
            text = row['text'].replace('\t',' ').replace('\r','\\r').replace('\n','\\n')
            f.write(f"{row['offset']}\t{row['length']}\t{text}\n")
    # choose a useful sample block when possible
    preferred = None
    for bs in [1024, 512, 256, 128, 153, 176, 440, 696, 720]:
        if len(data) % bs == 0:
            preferred = bs
            break
    if preferred:
        (outdir / f'{path.stem}_block_samples_{preferred}.json').write_text(
            json.dumps({'file': path.name, 'blockSize': preferred, 'samples': block_samples(data, preferred)}, indent=2),
            encoding='utf-8')
    return info

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('source', help='Folder containing .DAT files')
    ap.add_argument('--out', default='NativeDecodeReports', help='Output report folder')
    args = ap.parse_args()
    source = Path(args.source)
    outdir = Path(args.out)
    if not source.exists():
        raise SystemExit(f'Source folder not found: {source}')
    all_info = []
    for path in sorted(source.glob('*.DAT')):
        print(f'Inspecting {path.name}...')
        all_info.append(inspect_file(path, outdir))
    (outdir / 'native_dat_file_catalog.json').write_text(json.dumps(all_info, indent=2), encoding='utf-8')
    print(f'Wrote reports to {outdir}')

if __name__ == '__main__':
    main()
