# Modèle de données

`PersistenceEntry` représente une entrée logique avec identifiant, valeur brute, commande analysée, contexte, métadonnées, preuves de fichier et hash d’état. `PersistenceSnapshot` regroupe métadonnées, entrées normalisées et `CollectionError` partielles. `PersistenceChange` conserve les états avant/après, les champs modifiés, un résumé lisible et des observations explicites, sans score de risque.

Les noms de champs JSON, valeurs d’enums et versions de schéma restent en anglais pour garantir la stabilité des automatisations.
