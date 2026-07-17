using PersistLens.Domain;

namespace PersistLens.Application;

public sealed class InventoryService(IEnumerable<IPersistenceCollector> collectors, IClock clock, IFileEvidenceProvider? evidenceProvider = null)
{
    private readonly IReadOnlyList<IPersistenceCollector> collectors = collectors.ToList();

    public async Task<InventoryResult> CollectAsync(OperatingContext operatingContext, CancellationToken cancellationToken)
    {
        var context = new CollectionContext(operatingContext);
        var results = new List<CollectorResult>();
        foreach (var collector in collectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { results.Add(await collector.CollectAsync(context, cancellationToken).ConfigureAwait(false)); }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                results.Add(new(collector.Type, Array.Empty<PersistenceEntry>(), [new(collector.GetType().Name, collector.Type.ToString(), exception.Message)]));
            }
        }
        IReadOnlyList<PersistenceEntry> collected = results.SelectMany(result => result.Entries).OrderBy(entry => entry.StableId, StringComparer.Ordinal).ToArray();
        if (evidenceProvider is not null)
        {
            var enriched = new List<PersistenceEntry>(collected.Count);
            foreach (var entry in collected)
            {
                var evidence = entry.Command.ExecutablePath is null ? null : await evidenceProvider.CollectAsync(entry.Command.ExecutablePath, cancellationToken).ConfigureAwait(false);
                enriched.Add(PersistenceEntry.Create(entry.Type, entry.Collector, entry.Location, entry.Name, entry.RawValue, entry.Command, entry.RunAs, entry.Metadata, evidence, entry.CollectedAtUtc));
            }
            collected = enriched;
        }
        var deduplicated = collected
            .GroupBy(entry => entry.StableId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(entry => entry.Location.RegistryView, StringComparer.Ordinal).First())
            .OrderBy(entry => entry.StableId, StringComparer.Ordinal)
            .ToArray();
        var errors = results
            .SelectMany(result => result.Errors)
            .OrderBy(error => error.Collector, StringComparer.Ordinal)
            .ThenBy(error => error.Location, StringComparer.Ordinal)
            .ThenBy(error => error.Message, StringComparer.Ordinal)
            .ToArray();
        return new(deduplicated, errors, clock.UtcNow);
    }

    public PersistenceSnapshot ToSnapshot(InventoryResult inventory, OperatingContext context, string version) =>
        new(new(1, version, inventory.CollectedAtUtc, context, collectors.Select(collector => collector.GetType().Name).ToArray(), inventory.Errors), inventory.Entries);
}
