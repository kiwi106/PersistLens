# Modèle de données

`PersistenceEntry` représente une entrée logique avec identifiant, valeur brute, commande analysée, contexte, métadonnées, preuves de fichier et hash d’état. `PersistenceSnapshot` regroupe métadonnées, entrées normalisées et `CollectionError` partielles. `PersistenceChange` conserve les états avant/après, les champs modifiés, un résumé lisible et des observations explicites, sans score de risque.

`SignatureEvidence` conserve un statut métier, le HRESULT natif `WinVerifyTrust`, un message humain, les indicateurs de présence de signature, validité cryptographique et confiance de chaîne, ainsi que les métadonnées de certificat disponibles. Les champs d’horodatage sont prévus par le schéma et restent absents lorsque l’API ne les expose pas de manière fiable. Aucun certificat binaire n’est stocké.

Les nouveaux champs de signature sont optionnels : les snapshots 0.1.0 restent lisibles. Les noms de champs JSON, valeurs d’enums et versions de schéma restent en anglais pour garantir la stabilité des automatisations.
