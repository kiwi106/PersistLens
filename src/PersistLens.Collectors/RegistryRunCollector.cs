using Microsoft.Win32;
using PersistLens.Domain;

namespace PersistLens.Collectors;

public sealed class RegistryRunCollector(WindowsCommandParser parser, IClock clock) : IPersistenceCollector
{
    private static readonly string[] SubKeys = ["Software\\Microsoft\\Windows\\CurrentVersion\\Run", "Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce"];
    public PersistenceType Type => PersistenceType.RegistryRun;

    public Task<CollectorResult> CollectAsync(CollectionContext context, CancellationToken cancellationToken)
    {
        var entries = new List<PersistenceEntry>(); var errors = new List<CollectionError>();
        if (!OperatingSystem.IsWindows()) return Task.FromResult(new CollectorResult(Type, entries, [new(nameof(RegistryRunCollector), "Windows", "La collecte Registry est prise en charge uniquement sous Windows.")]));
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                foreach (var subKey in SubKeys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var location = $"{hive}\\{subKey}";
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var key = baseKey.OpenSubKey(subKey, writable: false);
                        if (key is null) continue;
                        foreach (var name in key.GetValueNames())
                        {
                            var kind = key.GetValueKind(name); var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                            var raw = value as string;
                            if (raw is null) { errors.Add(new(nameof(RegistryRunCollector), location, $"La valeur « {name} » a le type non pris en charge {kind}.")); continue; }
                            var parsed = parser.Parse(raw);
                            entries.Add(PersistenceEntry.Create(Type, nameof(RegistryRunCollector), new(location, view.ToString()), name, raw, parsed, hive == RegistryHive.CurrentUser ? context.OperatingContext.User : "SYSTEM", new Dictionary<string, string> { ["registryValueType"] = kind.ToString() }, null, clock.UtcNow));
                        }
                    }
                    catch (UnauthorizedAccessException exception) { errors.Add(new(nameof(RegistryRunCollector), location, exception.Message, null, true)); }
                    catch (System.Security.SecurityException exception) { errors.Add(new(nameof(RegistryRunCollector), location, exception.Message, null, true)); }
                    catch (IOException exception) { errors.Add(new(nameof(RegistryRunCollector), location, exception.Message)); }
                }
        return Task.FromResult(new CollectorResult(Type, entries, errors));
    }
}
