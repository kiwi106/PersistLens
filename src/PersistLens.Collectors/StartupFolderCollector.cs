using PersistLens.Domain;

namespace PersistLens.Collectors;

public sealed class StartupFolderCollector(WindowsCommandParser parser, IClock clock) : IPersistenceCollector
{
    public PersistenceType Type => PersistenceType.StartupFolder;
    public Task<CollectorResult> CollectAsync(CollectionContext context, CancellationToken cancellationToken)
    {
        var entries = new List<PersistenceEntry>(); var errors = new List<CollectionError>();
        var locations = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) }.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var location in locations)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(location, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attributes = File.GetAttributes(file);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) { errors.Add(new(nameof(StartupFolderCollector), file, "Reparse point was not followed.")); continue; }
                    var isShortcut = file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
                    var command = isShortcut ? PersistenceCommand.RawOnly(file, "Shortcut resolution is unavailable without executing COM in this MVP.") : parser.Parse($"\"{file}\"");
                    entries.Add(PersistenceEntry.Create(Type, nameof(StartupFolderCollector), new(location), Path.GetFileName(file), file, command, context.OperatingContext.User,
                        new Dictionary<string, string> { ["isShortcut"] = isShortcut.ToString() }, null, clock.UtcNow));
                }
            }
            catch (UnauthorizedAccessException exception) { errors.Add(new(nameof(StartupFolderCollector), location, exception.Message, null, true)); }
            catch (IOException exception) { errors.Add(new(nameof(StartupFolderCollector), location, exception.Message)); }
        }
        return Task.FromResult(new CollectorResult(Type, entries, errors));
    }
}
