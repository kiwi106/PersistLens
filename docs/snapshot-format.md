# Format des snapshots

Les snapshots sont des fichiers JSON UTF-8 camelCase avec `schemaVersion` 1. La racine contient `metadata` (version de l’application, horodatage UTC, contexte, collecteurs et erreurs) et `entries`. Les dates sont ISO 8601 UTC et les hashes SHA-256 sont complets, en hexadécimal minuscule.

L’écriture utilise un fichier temporaire unique, flush, puis remplacement. Les noms sont limités à 1–64 lettres ASCII, chiffres, `_` ou `-`. En cas de collecte partielle, les entrées valides et les erreurs structurées sont conservées ; la CLI retourne alors le code 4.
