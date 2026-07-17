# Data model

`PersistenceEntry` represents one logical entry with an identity, raw value, parsed command, execution context, mechanism metadata, optional `FileEvidence`, collection time, and state hash. `PersistenceSnapshot` carries schema version 1 metadata, normalized entries, and partial `CollectionError` values. A `PersistenceChange` retains before/after entries, explicit changed fields, summary, and caution observations; it has no risk score.
