#!/usr/bin/env python3
"""Validate the current Librarium relationship graph without requiring retired snapshots."""
import collections
import json
import os
import sys

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
legacy = os.path.join(root, 'Graph', 'core_graph.json')

if os.path.exists(legacy):
    graph = json.load(open(legacy, encoding='utf-8'))
    nodes = graph.get('nodes', [])
    edges = graph.get('edges', [])
    node_ids = {n.get('nodeId', n.get('id')) for n in nodes}
    normalized = [
        (
            e.get('source', e.get('subject')),
            e.get('relationship', e.get('predicate')),
            e.get('target', e.get('object')),
            json.dumps(e.get('properties', {}), sort_keys=True),
        )
        for e in edges
    ]
    source_name = 'Librarium/Graph/core_graph.json'
else:
    entity_path = os.path.join(root, 'Assertions', 'knowledge_entities_0011.json')
    assertion_path = os.path.join(root, 'Assertions', 'knowledge_assertions_0011.json')
    nodes = json.load(open(entity_path, encoding='utf-8'))
    edges = json.load(open(assertion_path, encoding='utf-8'))
    node_ids = {n.get('id') for n in nodes}
    normalized = [
        (
            e.get('subject'),
            e.get('predicate'),
            e.get('object'),
            json.dumps(e.get('properties', {}), sort_keys=True),
        )
        for e in edges
    ]
    source_name = 'Librarium/Assertions/knowledge_*_0011.json'

# Objects may be literals rather than entity IDs. Only non-empty subjects must resolve.
missing_src = [edge for edge in normalized if edge[0] and edge[0] not in node_ids]
# A target is required to resolve only when it looks like a Librarium entity ID.
entity_prefixes = ('entity.', 'item.', 'npc.', 'room.', 'spell.', 'formula.', 'class.', 'race.', 'skill.')
missing_tgt = [edge for edge in normalized if isinstance(edge[2], str) and edge[2].startswith(entity_prefixes) and edge[2] not in node_ids]
dups = collections.Counter(normalized)
dupe_count = sum(1 for value in dups.values() if value > 1)
status = 'PASS' if nodes and edges and not missing_src and not missing_tgt else 'FAIL'
print(json.dumps({
    'status': status,
    'source': source_name,
    'nodes': len(nodes),
    'relationships': len(edges),
    'missingSources': len(missing_src),
    'missingEntityTargets': len(missing_tgt),
    'duplicateEdgesDiagnostic': dupe_count,
}, indent=2))
sys.exit(0 if status == 'PASS' else 1)
