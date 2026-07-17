# Snapshot format

Snapshots are UTF-8 JSON with camel-case fields and schema version 1. The root has `metadata` (application version, UTC timestamp, operating context, collectors, errors) and `entries`. Dates are ISO 8601 UTC values; hashes are full lowercase hexadecimal SHA-256. Writes serialize to a unique temporary file, flush it, then replace the target. Names are restricted to safe 1-64 character ASCII identifiers. If collection is partial, valid entries and structured errors are both retained in the snapshot; CLI creation returns exit code 4 while a readable snapshot is still written.
