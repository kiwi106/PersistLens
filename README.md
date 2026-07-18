# PersistLens

## 1. Présentation

PersistLens est un outil Windows défensif, local-first et en ligne de commande. Il inventorie certains mécanismes de persistance, crée des snapshots (instantanés de l’état du système) et compare leurs changements.

## 2. Pourquoi PersistLens existe

Les mécanismes de démarrage automatique sont nombreux et leurs changements méritent d’être visibles, documentés et comparables. PersistLens présente des faits observés, des erreurs de collecte et des indicateurs de prudence, sans verdict automatique.

> PersistLens n’est pas un antivirus. Une entrée inconnue, non signée ou modifiée n’est pas nécessairement malveillante.

## 3. Fonctionnalités

- Inventaire en lecture seule des clés Registry Run/RunOnce, services automatiques, tâches planifiées Task Scheduler 2.0 et dossiers Startup.
- Parcours récursif des dossiers et tâches via l’API officielle `Schedule.Service`, sans lecture de `System32\Tasks`.
- Snapshots JSON locaux, écriture atomique, lecture, liste et suppression.
- Diff déterministe : entrées ajoutées, supprimées et modifiées.
- SHA-256 en streaming pour les fichiers locaux résolus.
- Sorties terminal et JSON scriptables.

## 4. Fonctionnalités partielles

- Un certificat de signature lisible est une preuve limitée : PersistLens ne valide pas la chaîne de confiance Authenticode Windows.
- Les raccourcis `.lnk` sont inventoriés, sans résolution de cible, arguments ou dossier de travail.
- Le propriétaire de fichier et l’état courant des services/tâches ne sont pas collectés.
- Les tâches ou dossiers protégés peuvent rester inaccessibles et produire une erreur partielle structurée.

## 5. Limitations

PersistLens fonctionne sous Windows. Les commandes ambiguës restent non résolues, les fichiers dépassant 512 Mio ne sont pas hashés et la rédaction automatique des rapports n’est pas encore disponible. Consultez [docs/limitations.md](docs/limitations.md).

## 6. Prérequis

- Windows.
- SDK .NET 8 installé.

## 7. Compilation

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

## 8. Utilisation

```powershell
dotnet run --project src/PersistLens.Cli -- --help
dotnet run --project src/PersistLens.Cli -- inventory
dotnet run --project src/PersistLens.Cli -- inventory --format json
dotnet run --project src/PersistLens.Cli -- snapshot create --name clean
dotnet run --project src/PersistLens.Cli -- diff clean current --format json
```

Les snapshots sont stockés par défaut dans `%LOCALAPPDATA%\PersistLens\snapshots`. Utilisez `--storage <dossier>` pour choisir un emplacement local différent. Les noms autorisés contiennent uniquement lettres ASCII, chiffres, `_` et `-`.

## 9. Exemples

```text
Inventaire PersistLens : 314 entrées, 0 erreur(s) de collecte
[Tâche planifiée] Exemple:0
  Emplacement : \Exemple
  Commande : "C:\Program Files\Exemple\agent.exe" --quiet
```

## 10. Codes de sortie

| Code | Signification |
| --- | --- |
| 0 | Succès |
| 1 | Différences détectées par `diff` |
| 2 | Entrée utilisateur invalide |
| 3 | Erreur opérationnelle |
| 4 | Collecte partielle : les résultats valides et les erreurs sont conservés |

Un `snapshot create` avec le code 4 écrit malgré tout un snapshot lisible contenant les erreurs de collecte dans ses métadonnées.

## 11. Confidentialité

PersistLens ne transmet aucune donnée, ne demande aucun compte et n’utilise ni télémétrie ni service cloud. Les snapshots peuvent contenir des chemins et lignes de commande sensibles : traitez-les comme des données sensibles.

## 12. Sécurité

PersistLens n’exécute jamais les commandes ou tâches découvertes et ne modifie ni ne supprime les mécanismes de persistance. Certaines informations peuvent nécessiter des permissions supplémentaires. Consultez [docs/security-model.md](docs/security-model.md) et [docs/threat-model.md](docs/threat-model.md).

## 13. Architecture

`Domain` contient les modèles et interfaces ; `Application` orchestre l’inventaire et le diff ; `Collectors`, `Enrichment`, `Storage` et `Reporting` isolent les intégrations. La CLI compose ces projets. Détails : [docs/architecture.md](docs/architecture.md).

## 14. Tests

La suite xUnit couvre les identifiants, diff, validation de snapshots, JSON invalide, parsing prudent, erreurs partielles, Task Scheduler, CLI et intégration Windows en lecture seule.

## 15. Contribution

Consultez [CONTRIBUTING.md](CONTRIBUTING.md). N’ajoutez ni snapshots réels ni rapports sensibles au dépôt.

## 16. Roadmap

La roadmap est disponible dans [docs/roadmap.md](docs/roadmap.md).

## 17. Licence

PersistLens est distribué sous licence [MIT](LICENSE).
