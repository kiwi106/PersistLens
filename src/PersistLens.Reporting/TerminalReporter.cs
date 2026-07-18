using PersistLens.Domain;

namespace PersistLens.Reporting;

public sealed class TerminalReporter : IReporter
{
    public async Task WriteInventoryAsync(InventoryResult result, TextWriter writer, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync($"Inventaire PersistLens : {result.Entries.Count} entrées, {result.Errors.Count} erreur(s) de collecte").ConfigureAwait(false);
        foreach (var entry in result.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"[{Display(entry.Type)}] {entry.Name}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Emplacement : {entry.Location.Value}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Commande : {entry.Command.Raw}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Cible : {entry.Command.ExecutablePath ?? "non résolue"}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Signature : {(entry.FileEvidence is null ? "non collectée" : Display(entry.FileEvidence.Signature.Status))} ; Hash : {entry.FileEvidence?.Sha256?[..Math.Min(12, entry.FileEvidence.Sha256.Length)] ?? "non collecté"}").ConfigureAwait(false);
            if (entry.Command.UncertaintyReason is not null) await writer.WriteLineAsync($"  Note : {entry.Command.UncertaintyReason}").ConfigureAwait(false);
        }
        foreach (var error in result.Errors) await writer.WriteLineAsync($"Avertissement de collecte [{error.Collector}] {error.Location} : {error.Message}").ConfigureAwait(false);
    }

    public Task WriteSnapshotAsync(string name, PersistenceSnapshot snapshot, TextWriter writer, CancellationToken cancellationToken) => writer.WriteLineAsync($"Snapshot « {name} » : {snapshot.Entries.Count} entrées, créé le {snapshot.Metadata.CreatedAtUtc:O}");
    public async Task WriteDiffAsync(DiffResult result, TextWriter writer, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(result.Changes.Count == 0 ? "Comparaison PersistLens : aucune différence détectée" : $"Comparaison PersistLens : {result.Changes.Count} changement(s)").ConfigureAwait(false);
        foreach (var change in result.Changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(change.Summary).ConfigureAwait(false);
            if (change.ChangedFields.Count > 0) await writer.WriteLineAsync($"  Champs modifiés : {string.Join(", ", change.ChangedFields)}").ConfigureAwait(false);
            if (change.CautionIndicators.Count > 0) await writer.WriteLineAsync($"  Observations : {string.Join(", ", change.CautionIndicators)}").ConfigureAwait(false);
        }
    }

    private static string Display(PersistenceType type) => type switch { PersistenceType.RegistryRun => "Registry Run/RunOnce", PersistenceType.Service => "Service", PersistenceType.ScheduledTask => "Tâche planifiée", PersistenceType.StartupFolder => "Dossier Startup", _ => type.ToString() };
    private static string Display(SignatureStatus status) => status switch { SignatureStatus.Trusted => "Signée et approuvée", SignatureStatus.SignedUntrusted => "Signée, mais non approuvée", SignatureStatus.Invalid => "Signature invalide", SignatureStatus.Unsigned => "Non signée", SignatureStatus.FileMissing => "Fichier introuvable", SignatureStatus.AccessDenied => "Accès refusé", SignatureStatus.Unsupported => "Vérification non prise en charge", SignatureStatus.Error => "Erreur de vérification", _ => status.ToString() };
}
