using PersistLens.Application;
using PersistLens.Domain;

namespace PersistLens.Application.Tests;

public sealed class DiffServiceTests
{
    [Fact]
    public void Compare_reports_add_remove_and_modified_entries_deterministically()
    {
        var oldChanged = Entry("changed", "one"); var oldRemoved = Entry("removed", "one");
        var newChanged = Entry("changed", "two"); var newAdded = Entry("added", "one");
        var before = Snapshot([oldChanged, oldRemoved]); var after = Snapshot([newChanged, newAdded]);
        var result = new DiffService().Compare(before, after);
        Assert.Equal([ChangeType.Added, ChangeType.Removed, ChangeType.Modified], result.Changes.Select(change => change.Type).OrderBy(type => type).ToArray());
        Assert.Contains(result.Changes, change => change.Type == ChangeType.Modified && change.ChangedFields.Contains("command"));
        Assert.Equal(result.Changes.Select(change => change.StableId).OrderBy(id => id, StringComparer.Ordinal), result.Changes.Select(change => change.StableId));
    }

    [Fact]
    public async Task CollectAsync_retains_entries_when_a_collector_reports_a_partial_error()
    {
        var service = new InventoryService([new PartialCollector()], new FixedClock());
        var result = await service.CollectAsync(new("machine", null, "x64", "user", false), CancellationToken.None);
        Assert.Single(result.Entries);
        Assert.Single(result.Errors);
        Assert.True(result.Errors[0].IsAccessDenied);
    }

    private static PersistenceSnapshot Snapshot(IReadOnlyList<PersistenceEntry> entries) => new(new(1, "test", DateTimeOffset.UtcNow, new("machine", null, "x64", "user", false), [], []), entries);
    private static PersistenceEntry Entry(string name, string command) => PersistenceEntry.Create(PersistenceType.Service, "test", new("service"), name, command, new(command, command, null, null, [], EvidenceConfidence.High, null), "SYSTEM", null, null, DateTimeOffset.UtcNow);

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch; }

    private sealed class PartialCollector : IPersistenceCollector
    {
        public PersistenceType Type => PersistenceType.RegistryRun;
        public Task<CollectorResult> CollectAsync(CollectionContext context, CancellationToken cancellationToken) => Task.FromResult(new CollectorResult(Type, [Entry("kept", "command")], [new("test", "location", "access denied", null, true)]));
    }
}
