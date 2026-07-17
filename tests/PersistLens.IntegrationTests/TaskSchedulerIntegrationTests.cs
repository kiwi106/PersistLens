using PersistLens.Collectors.Windows;

namespace PersistLens.IntegrationTests;

public sealed class TaskSchedulerIntegrationTests
{
    [Fact]
    public async Task Windows_source_enumerates_without_modifying_the_scheduler()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await new WindowsTaskSchedulerSource().EnumerateAsync(CancellationToken.None);
        Assert.Equal(result.Tasks.Select(task => task.Path).Distinct(StringComparer.Ordinal).Count(), result.Tasks.Count);
    }
}
