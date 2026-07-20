# Architecture

`Domain` contient les modèles immuables et les interfaces, sans dépendance aux API Windows ni à JSON. `Application` orchestre inventaire, snapshots et diff. `Collectors` isole les collecteurs Windows ; `Enrichment` collecte les preuves de fichiers ; `Storage` persiste les snapshots ; `Reporting` présente les résultats ; `Cli` compose l’ensemble.

Le collecteur de tâches dépend de `IScheduledTaskSource`. Sa source Windows isole l’automation COM Task Scheduler 2.0 et libère les références COM. Le collecteur métier ne dépend ni de COM ni du système de fichiers.

Le collecteur Startup dépend de `IShortcutResolver`. `WindowsShortcutResolver` isole l’interop COM typée `IShellLinkW`/`IPersistFile`, les HRESULT et la libération COM. `ShortcutTargetEvidence` est optionnel afin que les snapshots 0.1.0 restent lisibles.

L’enrichissement dépend de `IAuthenticodeVerifier`. `WindowsAuthenticodeVerifier` isole `WinVerifyTrust`, ses structures natives et le mapping HRESULT ; le domaine ne dépend ni du P/Invoke ni de CryptoAPI.

Le masquage est exclusivement une projection de `Reporting`. `ReportRedactor` construit une copie de présentation, puis les reporters terminal ou JSON l’écrivent lorsque `--redact` est explicite. Les objets du domaine, résultats en mémoire, identifiants/hashes et snapshots `Storage` ne sont ni modifiés ni marqués.

Les identifiants stables sont des SHA-256 de type, emplacement, nom et contexte normalisés. Le hash d’état est distinct : il couvre commande, cible, arguments, preuves sélectionnées et métadonnées triées, mais ni l’heure de collecte ni le chemin du snapshot.
