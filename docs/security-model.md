# Modèle de sécurité

Les valeurs collectées sont non fiables. PersistLens ne lance ni shell, ni commande découverte, ni tâche. La collecte Task Scheduler utilise le service COM local en lecture seule ; les références COM sont libérées dans la source Windows isolée. Le XML interdit les DTD et résolveurs externes. L’outil ne demande pas d’élévation automatique et ne modifie pas le système.

Les hashes utilisent une lecture séquentielle bornée ; les snapshots valident leurs noms et ne peuvent pas sortir du dossier de stockage configuré.

La vérification Authenticode utilise `WinVerifyTrust` sur le chemin local et ferme systématiquement son état natif. Elle n’exécute ni ne charge le fichier. La révocation consulte uniquement le cache Windows local : aucune requête réseau n’est déclenchée par PersistLens. Une révocation non vérifiable est distinguée d’un certificat révoqué.

Le masquage de rapport ne traite que les chaînes déjà collectées et ne les exécute jamais. Les expressions régulières utilisées sont bornées et les très longues chaînes sont remplacées par un marqueur sûr. Les valeurs détectées dans une commande sont aussi remplacées dans les autres champs de la même sortie afin d’éviter une fuite indirecte. Cette protection est best effort : aucun rapport ne doit être partagé sans relecture humaine.
