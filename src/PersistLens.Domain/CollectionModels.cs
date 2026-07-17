namespace PersistLens.Domain;

public sealed record CollectionError(string Collector, string Location, string Message, string? ErrorCode = null, bool IsAccessDenied = false);
public sealed record CollectorResult(PersistenceType Type, IReadOnlyList<PersistenceEntry> Entries, IReadOnlyList<CollectionError> Errors);
public sealed record InventoryResult(IReadOnlyList<PersistenceEntry> Entries, IReadOnlyList<CollectionError> Errors, DateTimeOffset CollectedAtUtc);
public sealed record CollectionContext(OperatingContext OperatingContext, int MaxFileSizeMegabytes = 512);
public sealed record OperatingContext(string Machine, string? WindowsVersion, string Architecture, string User, bool IsElevated);

public interface IPersistenceCollector
{
    PersistenceType Type { get; }
    Task<CollectorResult> CollectAsync(CollectionContext context, CancellationToken cancellationToken);
}
