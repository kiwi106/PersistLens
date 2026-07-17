namespace PersistLens.Domain;

public interface IClock { DateTimeOffset UtcNow { get; } }
public interface IIdentifierGenerator { string Create(string canonicalValue); }
public interface IFileEvidenceProvider { Task<FileEvidence?> CollectAsync(string rawPath, CancellationToken cancellationToken); }
public interface IReporter
{
    Task WriteInventoryAsync(InventoryResult result, TextWriter writer, CancellationToken cancellationToken);
    Task WriteSnapshotAsync(string name, PersistenceSnapshot snapshot, TextWriter writer, CancellationToken cancellationToken);
    Task WriteDiffAsync(DiffResult result, TextWriter writer, CancellationToken cancellationToken);
}
