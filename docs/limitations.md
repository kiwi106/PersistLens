# Limitations

PersistLens fonctionne sous Windows. La validation de confiance Authenticode, le propriétaire des fichiers, la résolution des `.lnk`, l’état courant des services et l’état runtime des tâches ne sont pas implémentés. Les tâches ou dossiers Task Scheduler protégés peuvent être inaccessibles ; l’outil conserve les autres tâches et signale une erreur partielle.

Les commandes ambiguës ne sont pas résolues et les fichiers dépassant 512 Mio ne sont pas hashés. Aucun mode de rédaction automatique n’existe encore : manipulez snapshots et rapports avec précaution.
