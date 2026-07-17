namespace PersistLens.Collectors;

public interface IScheduledTaskSource
{
    Task<ScheduledTaskSourceResult> EnumerateAsync(CancellationToken cancellationToken);
}

public sealed record ScheduledTaskSourceResult(IReadOnlyList<ScheduledTaskSourceTask> Tasks, IReadOnlyList<ScheduledTaskSourceError> Errors);
public sealed record ScheduledTaskSourceError(string Location, string ErrorType, string Message, bool IsAccessDenied = false);
public sealed record ScheduledTaskSourceTask(
    string Path,
    bool Enabled,
    string? Principal,
    string? RunLevel,
    IReadOnlyList<ScheduledTaskExecAction> Actions,
    IReadOnlyList<string> Triggers);
public sealed record ScheduledTaskExecAction(string Program, string Arguments, string? WorkingDirectory);
