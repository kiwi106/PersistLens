using PersistLens.Domain;

namespace PersistLens.Reporting;

public sealed class TerminalReporter : IReporter
{
    public async Task WriteInventoryAsync(InventoryResult result, TextWriter writer, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync($"PersistLens inventory: {result.Entries.Count} entries, {result.Errors.Count} collection errors").ConfigureAwait(false);
        foreach (var entry in result.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"[{entry.Type}] {entry.Name}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Location: {entry.Location.Value}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Command: {entry.Command.Raw}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Target: {entry.Command.ExecutablePath ?? "unresolved"}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Signature: {entry.FileEvidence?.Signature.Status.ToString() ?? "not collected"}; Hash: {entry.FileEvidence?.Sha256?[..Math.Min(12, entry.FileEvidence.Sha256.Length)] ?? "not collected"}").ConfigureAwait(false);
            if (entry.Command.UncertaintyReason is not null) await writer.WriteLineAsync($"  Note: {entry.Command.UncertaintyReason}").ConfigureAwait(false);
        }
        foreach (var error in result.Errors) await writer.WriteLineAsync($"Collection warning [{error.Collector}] {error.Location}: {error.Message}").ConfigureAwait(false);
    }

    public Task WriteSnapshotAsync(string name, PersistenceSnapshot snapshot, TextWriter writer, CancellationToken cancellationToken) => writer.WriteLineAsync($"Snapshot '{name}': {snapshot.Entries.Count} entries, created {snapshot.Metadata.CreatedAtUtc:O}");
    public async Task WriteDiffAsync(DiffResult result, TextWriter writer, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync($"PersistLens diff: {result.Changes.Count} changes").ConfigureAwait(false);
        foreach (var change in result.Changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(change.Summary).ConfigureAwait(false);
            if (change.ChangedFields.Count > 0) await writer.WriteLineAsync($"  Fields: {string.Join(", ", change.ChangedFields)}").ConfigureAwait(false);
            if (change.CautionIndicators.Count > 0) await writer.WriteLineAsync($"  Observations: {string.Join(", ", change.CautionIndicators)}").ConfigureAwait(false);
        }
    }
}
