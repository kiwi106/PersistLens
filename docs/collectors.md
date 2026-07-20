# Collecteurs

- **Registry Run/RunOnce** : lecture de HKCU/HKLM et des vues 32/64 bits.
- **Services** : lecture de la configuration de démarrage automatique dans Registry ; l’état runtime n’est pas collecté.
- **Tâches planifiées** : Task Scheduler 2.0 utilise `Schedule.Service`, `GetFolder`, `GetFolders` et `GetTasks` pour parcourir les dossiers, y compris les tâches masquées. Les définitions XML fournies par l’API sont traitées avec DTD et résolveurs externes désactivés. Aucun accès à `C:\Windows\System32\Tasks`, aucune exécution ni modification de tâche.
- **Dossiers Startup** : inspection des dossiers utilisateur et commun ; les points de réanalyse ne sont pas suivis. Chaque `.lnk` est conservé comme entrée d’origine, puis résolu au mieux par `IShellLinkW`/`IPersistFile` sans exécution. La cible, les arguments, le dossier de travail, la description et l’icône éventuelle restent des preuves séparées.

Les erreurs d’accès à une tâche ou un dossier sont structurées et n’empêchent pas la restitution des autres résultats.
