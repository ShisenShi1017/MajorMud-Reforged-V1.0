#!/usr/bin/env python3
"""Librarium-0009 knowledge service query helper.

Examples:
  python Librarium/Tools/query_knowledge_services.py services
  python Librarium/Tools/query_knowledge_services.py templates
  python Librarium/Tools/query_knowledge_services.py record Ability:123
"""
import json, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LIB = ROOT / "Librarium"

def load_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)

def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "help"
    if mode == "services":
        data = load_json(LIB / "Services" / "knowledge_services_0009.json")
        for svc in data.get("services", []):
            print(f"{svc['serviceId']}: {svc['name']} - {svc['purpose']}")
        return 0
    if mode == "templates":
        data = load_json(LIB / "Queries" / "natural_language_query_templates_0009.json")
        for t in data.get("templates", []):
            print(f"{t['intent']}: {', '.join(t.get('examples', [])[:2])}")
        return 0
    if mode == "record" and len(sys.argv) > 2:
        target = sys.argv[2]
        graph = load_json(LIB / "Graph" / "core_graph.json")
        nodes = graph.get("nodes", [])
        edges = graph.get("edges", [])
        node = next((n for n in nodes if str(n.get("id")) == target or str(n.get("nodeId")) == target), None)
        if not node:
            print(json.dumps({"found": False, "recordId": target}, indent=2))
            return 1
        rels = [e for e in edges if str(e.get("source")) == target or str(e.get("target")) == target or str(e.get("sourceId")) == target or str(e.get("targetId")) == target]
        print(json.dumps({"found": True, "record": node, "relationshipCount": len(rels), "sampleRelationships": rels[:25]}, indent=2))
        return 0
    print("Usage: query_knowledge_services.py services|templates|record <recordId>")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
