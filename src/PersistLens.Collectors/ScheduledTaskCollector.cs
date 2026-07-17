using PersistLens.Domain;

namespace PersistLens.Collectors;

/// <summary>Maps typed Task Scheduler source data into persistence entries without invoking tasks.</summary>
public sealed class ScheduledTaskCollector(IScheduledTaskSource source, WindowsCommandParser parser, IClock clock) : IPersistenceCollector
{
    public PersistenceType Type => PersistenceType.ScheduledTask;

    public async Task<CollectorResult> CollectAsync(CollectionContext context, CancellationToken cancellationToken)
    {
        var result = await source.EnumerateAsync(cancellationToken).ConfigureAwait(false);
        var errors = result.Errors.Select(error => new CollectionError(nameof(ScheduledTaskCollector), error.Location, error.Message, error.ErrorType, error.IsAccessDenied)).ToArray();
        var entries = result.Tasks.OrderBy(task => task.Path, StringComparer.Ordinal).SelectMany(task => MapTask(task, context.OperatingContext.User)).ToArray();
        return new(Type, entries, errors);
    }

    private IEnumerable<PersistenceEntry> MapTask(ScheduledTaskSourceTask task, string fallbackUser)
    {
        var name = TaskName(task.Path); var folder = TaskFolder(task.Path); var principal = task.Principal ?? fallbackUser;
        var metadata = new Dictionary<string, string>
        {
            ["taskPath"] = task.Path,
            ["folder"] = folder,
            ["enabled"] = task.Enabled.ToString(),
            ["state"] = task.Enabled ? "Enabled" : "Disabled",
            ["triggers"] = string.Join(", ", task.Triggers),
        };
        if (!string.IsNullOrWhiteSpace(task.RunLevel)) metadata["runLevel"] = task.RunLevel;
        if (task.Actions.Count == 0)
        {
            yield return PersistenceEntry.Create(Type, nameof(ScheduledTaskCollector), new(task.Path), name, string.Empty, PersistenceCommand.RawOnly(string.Empty, "Task has no Exec action."), principal, metadata, null, clock.UtcNow);
            yield break;
        }
        for (var index = 0; index < task.Actions.Count; index++)
        {
            var action = task.Actions[index];
            var raw = string.IsNullOrWhiteSpace(action.Program) ? action.Arguments : $"\"{action.Program}\" {action.Arguments}".Trim();
            var command = string.IsNullOrWhiteSpace(action.Program) ? PersistenceCommand.RawOnly(raw, "Exec action has no program.") : parser.Parse(raw, action.WorkingDirectory);
            yield return PersistenceEntry.Create(Type, nameof(ScheduledTaskCollector), new(task.Path), $"{name}:{index}", raw, command, principal, metadata, null, clock.UtcNow);
        }
    }

    private static string TaskName(string path) => path.TrimEnd('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? path;
    private static string TaskFolder(string path)
    {
        var index = path.LastIndexOf('\\');
        return index <= 0 ? "\\" : path[..index];
    }
}
