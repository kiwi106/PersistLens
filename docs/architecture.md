# Architecture

`Domain` contient les modèles immuables et les interfaces, sans dépendance aux API Windows ni à JSON. `Application` orchestre inventaire, snapshots et diff. `Collectors` isole les collecteurs Windows ; `Enrichment` collecte les preuves de fichiers ; `Storage` persiste les snapshots ; `Reporting` présente les résultats ; `Cli` compose l’ensemble.

Le collecteur de tâches dépend de `IScheduledTaskSource`. Sa source Windows isole l’automation COM Task Scheduler 2.0 et libère les références COM. Le collecteur métier ne dépend ni de COM ni du système de fichiers.

Les identifiants stables sont des SHA-256 de type, emplacement, nom et contexte normalisés. Le hash d’état est distinct : il couvre commande, cible, arguments, preuves sélectionnées et métadonnées triées, mais ni l’heure de collecte ni le chemin du snapshot.
