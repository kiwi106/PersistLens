namespace PersistLens.Domain;

public sealed record SnapshotMetadata(
    int SchemaVersion,
    string PersistLensVersion,
    DateTimeOffset CreatedAtUtc,
    OperatingContext OperatingContext,
    IReadOnlyList<string> Collectors,
    IReadOnlyList<CollectionError> Errors);

public sealed record PersistenceSnapshot(SnapshotMetadata Metadata, IReadOnlyList<PersistenceEntry> Entries);
public sealed record PersistenceChange(ChangeType Type, string StableId, PersistenceEntry? Before, PersistenceEntry? After, IReadOnlyList<string> ChangedFields, string Summary, IReadOnlyList<string> CautionIndicators);
public sealed record DiffResult(PersistenceSnapshot Before, PersistenceSnapshot After, IReadOnlyList<PersistenceChange> Changes)
{
    public bool HasChanges => Changes.Count != 0;
}

public interface ISnapshotStore
{
    Task SaveAsync(string name, PersistenceSnapshot snapshot, CancellationToken cancellationToken);
    Task<PersistenceSnapshot> LoadAsync(string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);
    Task DeleteAsync(string name, CancellationToken cancellationToken);
}
