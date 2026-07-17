using PersistLens.Collectors;
using PersistLens.Domain;

namespace PersistLens.Collectors.Tests;

public sealed class ScheduledTaskCollectorTests
{
    [Fact]
    public async Task CollectAsync_maps_nested_tasks_and_multiple_exec_actions_in_path_order()
    {
        var source = new FakeSource([
            CreateTask("\\Root\\Later", true, [new("C:\\Fixture\\later.exe", "", null)]),
            CreateTask("\\Root\\Nested\\First", true, [new("C:\\Fixture\\one.exe", "--one", "C:\\Fixture"), new("C:\\Fixture\\two.exe", "--two", null)]),
        ]);
        var result = await Collector(source).CollectAsync(Context, CancellationToken.None);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(["Later:0", "First:0", "First:1"], result.Entries.Select(entry => entry.Name));
        Assert.All(result.Entries, entry => Assert.Equal("Enabled", entry.Metadata["state"]));
        Assert.Contains(result.Entries, entry => entry.Metadata["folder"] == "\\Root\\Nested");
    }

    [Fact]
    public async Task CollectAsync_represents_disabled_task_without_exec_action()
    {
        var result = await Collector(new FakeSource([CreateTask("\\Disabled", false, [])])).CollectAsync(Context, CancellationToken.None);
        var entry = Assert.Single(result.Entries);
        Assert.Equal("Disabled", entry.Metadata["state"]);
        Assert.Equal("Task has no Exec action.", entry.Command.UncertaintyReason);
    }

    [Fact]
    public async Task CollectAsync_retains_accessible_tasks_when_source_has_folder_error()
    {
        var result = await Collector(new FakeSource([CreateTask("\\Accessible", true, [new("C:\\Fixture\\ok.exe", "", null)])], [new("\\Protected", "AccessDenied", "Access denied.", true)])).CollectAsync(Context, CancellationToken.None);
        Assert.Single(result.Entries);
        var error = Assert.Single(result.Errors);
        Assert.True(error.IsAccessDenied);
        Assert.Equal("\\Protected", error.Location);
    }

    [Fact]
    public async Task CollectAsync_propagates_cancellation_to_the_source()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Collector(new CancellingSource()).CollectAsync(Context, cancellation.Token));
    }

    private static readonly CollectionContext Context = new(new("fixture", null, "x64", "fixture", false));
    private static ScheduledTaskCollector Collector(IScheduledTaskSource source) => new(source, new WindowsCommandParser(), new FixedClock());
    private static ScheduledTaskSourceTask CreateTask(string path, bool enabled, IReadOnlyList<ScheduledTaskExecAction> actions) => new(path, enabled, "fixture-user", "LeastPrivilege", actions, ["LogonTrigger"]);
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch; }
    private sealed class FakeSource(IReadOnlyList<ScheduledTaskSourceTask> tasks, IReadOnlyList<ScheduledTaskSourceError>? errors = null) : IScheduledTaskSource
    {
        public Task<ScheduledTaskSourceResult> EnumerateAsync(CancellationToken cancellationToken) => Task.FromResult(new ScheduledTaskSourceResult(tasks, errors ?? []));
    }
    private sealed class CancellingSource : IScheduledTaskSource
    {
        public Task<ScheduledTaskSourceResult> EnumerateAsync(CancellationToken cancellationToken) => Task.FromCanceled<ScheduledTaskSourceResult>(cancellationToken);
    }
}
