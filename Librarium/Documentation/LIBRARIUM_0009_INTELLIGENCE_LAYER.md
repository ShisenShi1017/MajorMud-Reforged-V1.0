# Librarium-0009 — Intelligence Layer & Knowledge Services

Generated: 2026-07-07T22:21:39Z

This milestone turns the Librarium from a static knowledge graph into a service-ready knowledge system.

## Added

- Natural-language query template map
- Relationship explorer model
- Impact analysis model
- World diff/comparison model
- Canonical promotion workflow
- Documentation generation hooks
- Validation generation hooks
- Runtime/server/editor consumer API contract
- Local query helper: `Librarium/Tools/query_knowledge_services.py`

## Current Graph

- Nodes: 89213
- Relationships: 352691

## Important Boundary

This package defines local knowledge services and API contracts. It does **not** yet start a network service. The runtime, server, editor, and AI world systems should consume these contracts in the next integration stage.

## Project Rule

The Librarium is the source of truth. Runtime/server/editor consumers query Librarium services; they do not invent gameplay knowledge.
