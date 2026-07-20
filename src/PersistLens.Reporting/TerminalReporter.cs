using PersistLens.Domain;

namespace PersistLens.Reporting;

public sealed class TerminalReporter(ReportRedactor? redactor = null) : IReporter
{
    public async Task WriteInventoryAsync(InventoryResult result, TextWriter writer, CancellationToken cancellationToken)
    {
        if (redactor is not null) { await WriteWarningAsync(writer).ConfigureAwait(false); result = redactor.Redact(result); }
        await writer.WriteLineAsync($"Inventaire PersistLens : {result.Entries.Count} entrées, {result.Errors.Count} erreur(s) de collecte").ConfigureAwait(false);
        foreach (var entry in result.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"[{Display(entry.Type)}] {entry.Name}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Emplacement : {entry.Location.Value}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Commande : {entry.Command.Raw}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Cible : {entry.Command.ExecutablePath ?? "non résolue"}").ConfigureAwait(false);
            if (entry.Shortcut is not null)
            {
                await writer.WriteLineAsync($"  Raccourci : {Display(entry.Shortcut.Status)}").ConfigureAwait(false);
                await writer.WriteLineAsync($"  Cible du raccourci : {entry.Shortcut.NormalizedTargetPath ?? entry.Shortcut.ExpandedTargetPath ?? entry.Shortcut.RawTargetPath ?? "non résolue"}").ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(entry.Shortcut.Arguments)) await writer.WriteLineAsync($"  Arguments : {entry.Shortcut.Arguments}").ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(entry.Shortcut.WorkingDirectory)) await writer.WriteLineAsync($"  Dossier de travail : {entry.Shortcut.WorkingDirectory}").ConfigureAwait(false);
            }
            await writer.WriteLineAsync($"  Signature : {(entry.FileEvidence is null ? "non collectée" : Display(entry.FileEvidence.Signature.Status))} ; Hash : {entry.FileEvidence?.Sha256?[..Math.Min(12, entry.FileEvidence.Sha256.Length)] ?? "non collecté"}").ConfigureAwait(false);
            if (entry.Command.UncertaintyReason is not null) await writer.WriteLineAsync($"  Note : {entry.Command.UncertaintyReason}").ConfigureAwait(false);
        }
        foreach (var error in result.Errors) await writer.WriteLineAsync($"Avertissement de collecte [{error.Collector}] {error.Location} : {error.Message}").ConfigureAwait(false);
    }

    public async Task WriteSnapshotAsync(string name, PersistenceSnapshot snapshot, TextWriter writer, CancellationToken cancellationToken)
    {
        if (redactor is not null) { await WriteWarningAsync(writer).ConfigureAwait(false); snapshot = redactor.Redact(snapshot); }
        await writer.WriteLineAsync($"Snapshot « {name} » : {snapshot.Entries.Count} entrées, créé le {snapshot.Metadata.CreatedAtUtc:O}").ConfigureAwait(false);
    }
    public async Task WriteDiffAsync(DiffResult result, TextWriter writer, CancellationToken cancellationToken)
    {
        if (redactor is not null) { await WriteWarningAsync(writer).ConfigureAwait(false); result = redactor.Redact(result); }
        await writer.WriteLineAsync(result.Changes.Count == 0 ? "Comparaison PersistLens : aucune différence détectée" : $"Comparaison PersistLens : {result.Changes.Count} changement(s)").ConfigureAwait(false);
        foreach (var change in result.Changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(change.Summary).ConfigureAwait(false);
            if (change.ChangedFields.Count > 0) await writer.WriteLineAsync($"  Champs modifiés : {string.Join(", ", change.ChangedFields)}").ConfigureAwait(false);
            if (change.CautionIndicators.Count > 0) await writer.WriteLineAsync($"  Observations : {string.Join(", ", change.CautionIndicators)}").ConfigureAwait(false);
        }
    }

    private static Task WriteWarningAsync(TextWriter writer) => writer.WriteLineAsync("Rapport masqué : certaines données sensibles connues ont été remplacées. Ce mécanisme ne garantit pas l’absence de tout secret.");

    private static string Display(PersistenceType type) => type switch { PersistenceType.RegistryRun => "Registry Run/RunOnce", PersistenceType.Service => "Service", PersistenceType.ScheduledTask => "Tâche planifiée", PersistenceType.StartupFolder => "Dossier Startup", _ => type.ToString() };
    private static string Display(SignatureStatus status) => status switch { SignatureStatus.Trusted => "Signée et approuvée", SignatureStatus.SignedUntrusted => "Signée, mais non approuvée", SignatureStatus.Invalid => "Signature invalide", SignatureStatus.ExpiredSignature => "Signature ou certificat expiré", SignatureStatus.RevokedCertificate => "Certificat révoqué", SignatureStatus.UntrustedRoot => "Racine de confiance inconnue", SignatureStatus.ExplicitDistrust => "Certificat explicitement non approuvé", SignatureStatus.Unsigned => "Fichier non signé", SignatureStatus.FileMissing => "Fichier introuvable", SignatureStatus.AccessDenied => "Accès refusé", SignatureStatus.UnsupportedFileType => "Type de fichier non pris en charge", SignatureStatus.Unsupported => "Vérification Authenticode impossible", SignatureStatus.Error => "Erreur de vérification", SignatureStatus.Unknown => "Statut de vérification inconnu", _ => status.ToString() };
    private static string Display(ShortcutResolutionStatus status) => status switch { ShortcutResolutionStatus.Resolved => "Raccourci résolu", ShortcutResolutionStatus.PartiallyResolved => "Résolution partielle", ShortcutResolutionStatus.BrokenTarget => "Cible introuvable", ShortcutResolutionStatus.InvalidShortcut => "Raccourci invalide", ShortcutResolutionStatus.FileNotFound => "Raccourci introuvable", ShortcutResolutionStatus.AccessDenied => "Accès refusé", ShortcutResolutionStatus.Unsupported => "Résolution impossible", ShortcutResolutionStatus.Cancelled => "Résolution annulée", ShortcutResolutionStatus.ResolutionError => "Résolution impossible", _ => "Statut de résolution inconnu" };
}
