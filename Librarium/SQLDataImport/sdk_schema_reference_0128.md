# SDK SQL Schema Reference

`Tools/LibrariumDistribution/sdk_schema.sql` defines the local distribution schema used for Librarium records, relationships, evidence, and build manifests. It is retained as tooling schema, not game source truth.

```sql
-- Librarium Distribution SDK local database schema
-- Generated locally from canonical Librarium/evidence files; do not treat this DB as source truth.
CREATE TABLE IF NOT EXISTS records (
  id TEXT PRIMARY KEY,
  type TEXT,
  name TEXT,
  source_path TEXT NOT NULL,
  evidence_state TEXT,
  confidence TEXT,
  summary TEXT
);
CREATE TABLE IF NOT EXISTS relationships (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  source_id TEXT NOT NULL,
  relation TEXT NOT NULL,
  target_id TEXT NOT NULL,
  provenance TEXT,
  confidence TEXT
);
CREATE INDEX IF NOT EXISTS idx_records_type ON records(type);
CREATE INDEX IF NOT EXISTS idx_records_source_path ON records(source_path);
CREATE INDEX IF NOT EXISTS idx_relationships_source ON relationships(source_id);
CREATE INDEX IF NOT EXISTS idx_relationships_target ON relationships(target_id);
CREATE TABLE IF NOT EXISTS evidence (
  id TEXT PRIMARY KEY,
  record_id TEXT,
  source TEXT,
  source_path TEXT,
  evidence_state TEXT,
  confidence TEXT,
  excerpt TEXT
);
CREATE INDEX IF NOT EXISTS idx_evidence_record ON evidence(record_id);
CREATE TABLE IF NOT EXISTS build_manifest (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);

```
