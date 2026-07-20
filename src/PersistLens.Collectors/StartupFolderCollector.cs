using PersistLens.Domain;

namespace PersistLens.Collectors;

public sealed class StartupFolderCollector : IPersistenceCollector
{
    private readonly WindowsCommandParser parser;
    private readonly IClock clock;
    private readonly IShortcutResolver shortcutResolver;
    private readonly IReadOnlyList<string>? startupLocations;

    public StartupFolderCollector(WindowsCommandParser parser, IClock clock, IShortcutResolver shortcutResolver, IEnumerable<string>? startupLocations = null)
    {
        this.parser = parser;
        this.clock = clock;
        this.shortcutResolver = shortcutResolver;
        this.startupLocations = startupLocations?.ToArray();
    }

    public PersistenceType Type => PersistenceType.StartupFolder;
    public async Task<CollectorResult> CollectAsync(CollectionContext context, CancellationToken cancellationToken)
    {
        var entries = new List<PersistenceEntry>(); var errors = new List<CollectionError>();
        var locations = (startupLocations ?? [Environment.GetFolderPath(Environment.SpecialFolder.Startup), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)]).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var location in locations)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(location, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attributes = File.GetAttributes(file);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) { errors.Add(new(nameof(StartupFolderCollector), file, "Le point de réanalyse n’a pas été suivi.")); continue; }
                    var isShortcut = file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
                    ShortcutTargetEvidence? shortcut = null;
                    PersistenceCommand command;
                    if (isShortcut)
                    {
                        var resolution = await shortcutResolver.ResolveAsync(file, cancellationToken).ConfigureAwait(false);
                        shortcut = resolution.Evidence;
                        errors.AddRange(resolution.Errors);
                        if (shortcut.Status != ShortcutResolutionStatus.Resolved && !string.IsNullOrWhiteSpace(shortcut.UserMessage))
                            errors.Add(new CollectionError(nameof(StartupFolderCollector), file, shortcut.UserMessage, shortcut.Status.ToString(), shortcut.Status == ShortcutResolutionStatus.AccessDenied));
                        else if (shortcut.PartialErrors is not null)
                            errors.AddRange(shortcut.PartialErrors.Select(message => new CollectionError(nameof(StartupFolderCollector), file, message)));
                        command = new(file, shortcut.NormalizedTargetPath ?? shortcut.ExpandedTargetPath ?? shortcut.RawTargetPath, shortcut.Arguments, shortcut.WorkingDirectory, shortcut.NormalizedTargetPath is null ? [] : [shortcut.NormalizedTargetPath], shortcut.Confidence, shortcut.UserMessage);
                    }
                    else command = parser.Parse($"\"{file}\"");
                    entries.Add(PersistenceEntry.Create(Type, nameof(StartupFolderCollector), new(location), Path.GetFileName(file), file, command, context.OperatingContext.User,
                        new Dictionary<string, string> { ["isShortcut"] = isShortcut.ToString() }, null, clock.UtcNow, shortcut));
                }
            }
            catch (UnauthorizedAccessException exception) { errors.Add(new(nameof(StartupFolderCollector), location, exception.Message, null, true)); }
            catch (IOException exception) { errors.Add(new(nameof(StartupFolderCollector), location, exception.Message)); }
        }
        return new CollectorResult(Type, entries.OrderBy(entry => entry.StableId, StringComparer.Ordinal).ToArray(), errors);
    }
}
