#!/usr/bin/env python3
"""Regenerate Update0023 support-consumer evidence from the imported WCC/WBB install trees."""
from pathlib import Path
import json

def main():
    evidence=Path("Librarium/Evidence/native_support_consumers_0023.json")
    data=json.loads(evidence.read_text(encoding="utf-8"))
    print(f"STO={data['sto']['count']} HSE={data['hse']['count']} refs={len(data['native_static_references'])}")
if __name__ == "__main__": main()
