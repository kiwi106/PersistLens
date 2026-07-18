# Modèle de sécurité

Les valeurs collectées sont non fiables. PersistLens ne lance ni shell, ni commande découverte, ni tâche. La collecte Task Scheduler utilise le service COM local en lecture seule ; les références COM sont libérées dans la source Windows isolée. Le XML interdit les DTD et résolveurs externes. L’outil ne demande pas d’élévation automatique et ne modifie pas le système.

Les hashes utilisent une lecture séquentielle bornée ; les snapshots valident leurs noms et ne peuvent pas sortir du dossier de stockage configuré.
