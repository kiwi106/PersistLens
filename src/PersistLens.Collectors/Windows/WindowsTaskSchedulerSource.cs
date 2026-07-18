using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;

namespace PersistLens.Collectors.Windows;

/// <summary>Source d’automation COM Task Scheduler 2.0 en lecture seule. Toute utilisation COM reste isolée dans cette classe.</summary>
public sealed class WindowsTaskSchedulerSource : IScheduledTaskSource
{
    private const int IncludeHiddenTasks = 1;

    public Task<ScheduledTaskSourceResult> EnumerateAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<ScheduledTaskSourceTask>(); var errors = new List<ScheduledTaskSourceError>(); object? service = null; object? root = null;
        if (!OperatingSystem.IsWindows()) return Task.FromResult(new ScheduledTaskSourceResult(tasks, [new("Windows", "PlatformNotSupported", "Task Scheduler 2.0 est pris en charge uniquement sous Windows.")]));
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service", throwOnError: true) ?? throw new InvalidOperationException("La classe COM Task Scheduler est indisponible.");
            service = Activator.CreateInstance(serviceType) ?? throw new InvalidOperationException("Impossible de créer le service COM Task Scheduler.");
            Invoke(service, "Connect", Type.Missing, Type.Missing, Type.Missing, Type.Missing);
            root = Invoke(service, "GetFolder", "\\") ?? throw new InvalidOperationException("Le dossier racine de Task Scheduler est indisponible.");
            VisitFolder(root, tasks, errors, cancellationToken);
        }
        catch (COMException exception) { errors.Add(Error("TaskScheduler", exception)); }
        catch (UnauthorizedAccessException exception) { errors.Add(Error("TaskScheduler", exception)); }
        catch (TargetInvocationException exception) { errors.Add(Error("TaskScheduler", Unwrap(exception))); }
        catch (InvalidOperationException exception) { errors.Add(Error("TaskScheduler", exception)); }
        finally { Release(root); Release(service); }
        return Task.FromResult(new ScheduledTaskSourceResult(tasks.OrderBy(task => task.Path, StringComparer.Ordinal).ToArray(), errors.OrderBy(error => error.Location, StringComparer.Ordinal).ThenBy(error => error.ErrorType, StringComparer.Ordinal).ToArray()));
    }

    private static void VisitFolder(object folder, ICollection<ScheduledTaskSourceTask> tasks, ICollection<ScheduledTaskSourceError> errors, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folderPath = ReadString(folder, "Path", "\\"); object? registeredTasks = null; object? childFolders = null;
        try
        {
            registeredTasks = Invoke(folder, "GetTasks", IncludeHiddenTasks);
            foreach (var task in CollectionItems(registeredTasks).OrderBy(item => ReadString(item, "Path", string.Empty), StringComparer.Ordinal))
            {
                try { tasks.Add(ReadTask(task)); }
                catch (XmlException exception) { errors.Add(Error(SafeTaskPath(task, folderPath), exception, "MalformedDefinition")); }
                catch (COMException exception) { errors.Add(Error(SafeTaskPath(task, folderPath), exception)); }
                catch (UnauthorizedAccessException exception) { errors.Add(Error(SafeTaskPath(task, folderPath), exception)); }
                catch (TargetInvocationException exception) { errors.Add(Error(SafeTaskPath(task, folderPath), Unwrap(exception))); }
                finally { Release(task); }
            }
        }
        catch (COMException exception) { errors.Add(Error(folderPath, exception)); }
        catch (UnauthorizedAccessException exception) { errors.Add(Error(folderPath, exception)); }
        catch (TargetInvocationException exception) { errors.Add(Error(folderPath, Unwrap(exception))); }
        finally { Release(registeredTasks); }
        try
        {
            childFolders = Invoke(folder, "GetFolders", 0);
            foreach (var child in CollectionItems(childFolders).OrderBy(item => ReadString(item, "Path", string.Empty), StringComparer.Ordinal))
            {
                try { VisitFolder(child, tasks, errors, cancellationToken); }
                finally { Release(child); }
            }
        }
        catch (COMException exception) { errors.Add(Error(folderPath, exception)); }
        catch (UnauthorizedAccessException exception) { errors.Add(Error(folderPath, exception)); }
        catch (TargetInvocationException exception) { errors.Add(Error(folderPath, Unwrap(exception))); }
        finally { Release(childFolders); }
    }

    private static ScheduledTaskSourceTask ReadTask(object task)
    {
        var path = ReadString(task, "Path", "\\<unknown>"); var enabled = ReadBoolean(task, "Enabled"); var xml = ReadString(task, "Xml", string.Empty);
        return ScheduledTaskXmlParser.Parse(path, enabled, xml);
    }

    private static IEnumerable<object> CollectionItems(object? collection)
    {
        if (collection is null) yield break;
        var count = Convert.ToInt32(ReadProperty(collection, "Count"), System.Globalization.CultureInfo.InvariantCulture);
        for (var index = 1; index <= count; index++)
        {
            var item = ReadProperty(collection, "Item", index);
            if (item is not null) yield return item;
        }
    }

    private static object? Invoke(object target, string member, params object?[] arguments) => target.GetType().InvokeMember(member, BindingFlags.InvokeMethod, null, target, arguments);
    private static object? ReadProperty(object target, string member, params object?[] arguments) => target.GetType().InvokeMember(member, BindingFlags.GetProperty, null, target, arguments);
    private static string ReadString(object target, string member, string fallback) => ReadProperty(target, member) as string ?? fallback;
    private static bool ReadBoolean(object target, string member) => ReadProperty(target, member) is bool value && value;
    private static string SafeTaskPath(object task, string fallback)
    {
        try { return ReadString(task, "Path", fallback); }
        catch (TargetInvocationException) { return fallback; }
    }

    private static Exception Unwrap(TargetInvocationException exception) => exception.InnerException ?? exception;
    private static ScheduledTaskSourceError Error(string location, Exception exception, string? type = null)
    {
        var accessDenied = exception is UnauthorizedAccessException || exception.HResult == unchecked((int)0x80070005);
        return new(location, type ?? (accessDenied ? "AccessDenied" : "ComFailure"), Clean(exception.Message), accessDenied);
    }
    private static string Clean(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    private static void Release(object? value)
    {
        if (OperatingSystem.IsWindows() && value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}
