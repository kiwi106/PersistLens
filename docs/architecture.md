# Architecture

`Domain` contains immutable records and interfaces only. `Application` orchestrates inventory, snapshots, and deterministic diffs. `Collectors` contains Windows-facing read-only collectors and safe parsing. `Enrichment` hashes files by stream. `Storage` performs validated atomic JSON persistence. `Reporting` owns terminal/JSON presentation; `Cli` composes them. Dependencies point inward toward Domain.

Stable IDs are SHA-256 of uppercase, trimmed type/location/name/run-as fields separated by U+001F. State hashes use a separate SHA-256 canonical representation containing command, resolved target, arguments, selected evidence, and sorted metadata. Neither includes collection time or snapshot path.
