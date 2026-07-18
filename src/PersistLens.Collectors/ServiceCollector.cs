using Microsoft.Win32;
using PersistLens.Domain;

namespace PersistLens.Collectors;

/// <summary>Reads service startup configuration without starting, stopping, or changing a service.</summary>
public sealed class ServiceCollector(WindowsCommandParser parser, IClock clock) : IPersistenceCollector
{
    private const string ServicesPath = "SYSTEM\\CurrentControlSet\\Services";
    public PersistenceType Type => PersistenceType.Service;

    public Task<CollectorResult> CollectAsync(CollectionContext context, CancellationToken cancellationToken)
    {
        var entries = new List<PersistenceEntry>(); var errors = new List<CollectionError>();
        if (!OperatingSystem.IsWindows()) return Task.FromResult(new CollectorResult(Type, entries, [new(nameof(ServiceCollector), "Windows", "La collecte des services est prise en charge uniquement sous Windows.")]));
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(ServicesPath, writable: false);
            if (root is null) return Task.FromResult(new CollectorResult(Type, entries, [new(nameof(ServiceCollector), ServicesPath, "La clé des services est indisponible.")]));
            foreach (var serviceName in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var key = root.OpenSubKey(serviceName, writable: false);
                    if (key is null) continue;
                    var start = key.GetValue("Start") as int? ?? Convert.ToInt32(key.GetValue("Start", -1), System.Globalization.CultureInfo.InvariantCulture);
                    if (start is not 2) continue;
                    var imagePath = key.GetValue("ImagePath") as string ?? string.Empty;
                    var delayed = (key.GetValue("DelayedAutoStart") as int? ?? 0) != 0;
                    var displayName = key.GetValue("DisplayName") as string ?? serviceName;
                    var account = key.GetValue("ObjectName") as string ?? "LocalSystem";
                    entries.Add(PersistenceEntry.Create(Type, nameof(ServiceCollector), new($"HKLM\\{ServicesPath}\\{serviceName}"), serviceName, imagePath, parser.Parse(imagePath), account,
                        new Dictionary<string, string> { ["displayName"] = displayName, ["startType"] = delayed ? "AutomaticDelayed" : "Automatic", ["currentState"] = "Unavailable" }, null, clock.UtcNow));
                }
                catch (UnauthorizedAccessException exception) { errors.Add(new(nameof(ServiceCollector), serviceName, exception.Message, null, true)); }
                catch (IOException exception) { errors.Add(new(nameof(ServiceCollector), serviceName, exception.Message)); }
            }
        }
        catch (UnauthorizedAccessException exception) { errors.Add(new(nameof(ServiceCollector), ServicesPath, exception.Message, null, true)); }
        return Task.FromResult(new CollectorResult(Type, entries, errors));
    }
}
