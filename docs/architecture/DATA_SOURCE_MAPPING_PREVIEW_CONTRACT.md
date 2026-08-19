# Data Source Mapping Preview Contract

Status: active
Scope: `QDB05`

This contract describes the deterministic, read-only preview used to validate a source mapping before any durable import work happens.

## Purpose

- Map one source table or stream to a canonical Trendplus entity.
- Validate the mapping deterministically.
- Preview a bounded sample of source rows.
- Keep source discovery, key selection and cursor selection explicit.

## Inputs

- `profileName`: named source profile.
- `CanonicalEntity`: the canonical target entity.
- `SourceTable`: the explicit source table.
- `ExternalKeyColumns`: the explicit business key columns.
- `Cursor`: explicit cursor mode and aliases.
- `FieldMappings`: target field names plus ordered source aliases.
- `Take`: requested preview size.

## Behavior

- The preview is read-only and does not persist business data.
- The preview uses deterministic alias resolution against the source schema.
- Field mappings are matched in order and never guessed by model output.
- The preview returns a bounded sample, currently capped at 25 rows.
- The response includes a schema fingerprint derived from profile, provider, canonical entity, table, key columns, cursor and source schema.
- Validation issues are returned as structured reason codes, not as silent fallback.

## Response contract

- `SchemaFingerprint` changes when the source schema or mapping contract changes.
- `RequestedTake` echoes the requested preview size.
- `ReturnedRows` reports the actual number of rows returned.
- `Truncated` signals that the bounded sample was cut off.
- `FieldMappings` carries per-field status, reason code and message.
- `Issues` carries mapping-level and cursor/key validation issues.
- `Rows` carries the projected preview values for the resolved fields.

## Validation rules

- Missing source table, canonical entity, key columns, field mappings or cursor mode are rejected at request validation.
- Missing external key columns are surfaced as `external_key_missing_column`.
- Missing cursor aliases are surfaced as cursor issues and the preview falls back to a full scan path when the session cannot push the cursor predicate.
- Unmatched field aliases are surfaced as `source_column_not_found`.
- Blank target field names and blank alias lists are rejected as field issues.

## Follow-up boundary

- QDB06 owns durable checkpointing and incremental synchronization.
- This contract intentionally stops at preview and does not define write-back, checkpoint persistence or idempotent retry semantics.
