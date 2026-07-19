# Modèle de menaces

Les valeurs Registry, XML Task Scheduler, chemins, liens et fichiers sont traités comme des données, jamais comme des instructions. Les erreurs RPC/COM et accès refusés d’un dossier ou d’une tâche deviennent des erreurs partielles. Les entités XML externes sont désactivées et la taille des définitions est bornée.

Les snapshots ne sont pas signés et peuvent être altérés par un utilisateur qui peut écrire dans leur dossier. Les rapports peuvent révéler des chemins et secrets présents dans des lignes de commande. Ne publiez pas de snapshots ou rapports réels.

Les chemins vérifiés par Authenticode restent non fiables et le fichier peut disparaître ou changer entre inventaire et vérification. Les HRESULT inconnus sont conservés avec un statut `Unknown`, sans interprétation inventée. Une signature approuvée ne constitue pas une preuve d’innocuité.

Le partage d’un rapport demeure un risque : les lignes de commande et métadonnées peuvent contenir des formats de secrets inconnus, ou être sur-masqués. `--redact` réduit l’exposition de catégories explicites sans promettre l’absence de toute donnée personnelle ou confidentielle. Les snapshots locaux sont volontairement inchangés et doivent être protégés comme des données sensibles.
