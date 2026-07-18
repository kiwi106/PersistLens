using System.Security.Principal;
using PersistLens.Application;
using PersistLens.Collectors;
using PersistLens.Collectors.Windows;
using PersistLens.Domain;
using PersistLens.Enrichment;
using PersistLens.Enrichment.Windows;
using PersistLens.Reporting;
using PersistLens.Storage;

return await PersistLensProgram.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);

public static class PersistLensProgram
{
    private const string Version = "0.1.0";
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += Cancel;
        try
        {
            var parsed = Parse(args);
            if (parsed.ShowHelp) { await output.WriteLineAsync(Help).ConfigureAwait(false); return 0; }
            if (parsed.ShowVersion) { await output.WriteLineAsync(Version).ConfigureAwait(false); return 0; }
            var clock = new SystemClock(); var commandParser = new WindowsCommandParser();
            var inventory = new InventoryService([new RegistryRunCollector(commandParser, clock), new ServiceCollector(commandParser, clock), new ScheduledTaskCollector(new WindowsTaskSchedulerSource(), commandParser, clock), new StartupFolderCollector(commandParser, clock)], clock, new StreamingFileEvidenceProvider(new WindowsAuthenticodeVerifier()));
            var context = GetContext(); var store = new JsonSnapshotStore(parsed.StorageDirectory); IReporter reporter = parsed.Json ? new JsonReporter() : new TerminalReporter();
            return parsed.Command switch
            {
                "inventory" => await InventoryAsync(inventory, context, reporter, output, cancellation.Token).ConfigureAwait(false),
                "snapshot" => await SnapshotAsync(parsed, inventory, context, store, reporter, output, cancellation.Token).ConfigureAwait(false),
                "diff" => await DiffAsync(parsed, inventory, context, store, reporter, output, cancellation.Token).ConfigureAwait(false),
                "inspect" => await InspectAsync(parsed, inventory, context, reporter, output, cancellation.Token).ConfigureAwait(false),
                _ => await InvalidAsync(error, "Commande inconnue ou absente. Exécutez persistlens --help.").ConfigureAwait(false)
            };
        }
        catch (OperationCanceledException) { await error.WriteLineAsync("Opération annulée.").ConfigureAwait(false); return 3; }
        catch (ArgumentException exception) { await error.WriteLineAsync(exception.Message).ConfigureAwait(false); return 2; }
        catch (IOException exception) { await error.WriteLineAsync($"Erreur opérationnelle : {exception.Message}").ConfigureAwait(false); return 3; }
        finally { Console.CancelKeyPress -= Cancel; }

        void Cancel(object? _, ConsoleCancelEventArgs eventArgs) { eventArgs.Cancel = true; cancellation.Cancel(); }
    }

    private static async Task<int> InventoryAsync(InventoryService service, OperatingContext context, IReporter reporter, TextWriter output, CancellationToken token)
    {
        var result = await service.CollectAsync(context, token).ConfigureAwait(false); await reporter.WriteInventoryAsync(result, output, token).ConfigureAwait(false); return result.Errors.Count == 0 ? 0 : 4;
    }
    private static async Task<int> SnapshotAsync(Arguments arguments, InventoryService service, OperatingContext context, ISnapshotStore store, IReporter reporter, TextWriter output, CancellationToken token)
    {
        if (arguments.Positionals.Count == 0) return await InvalidAsync(output, "Syntaxe attendue : snapshot create|list|show|delete [nom]").ConfigureAwait(false);
        var action = arguments.Positionals[0];
        if (action == "list") { foreach (var snapshotName in await store.ListAsync(token).ConfigureAwait(false)) await output.WriteLineAsync(snapshotName).ConfigureAwait(false); return 0; }
        if (arguments.Positionals.Count < 2) return await InvalidAsync(output, "Un nom de snapshot est requis.").ConfigureAwait(false);
        var name = arguments.Positionals[1];
        if (action == "delete") { await store.DeleteAsync(name, token).ConfigureAwait(false); await output.WriteLineAsync($"Snapshot « {name} » supprimé.").ConfigureAwait(false); return 0; }
        if (action == "show") { await reporter.WriteSnapshotAsync(name, await store.LoadAsync(name, token).ConfigureAwait(false), output, token).ConfigureAwait(false); return 0; }
        if (action == "create") { var result = await service.CollectAsync(context, token).ConfigureAwait(false); var snapshot = service.ToSnapshot(result, context, Version); await store.SaveAsync(name, snapshot, token).ConfigureAwait(false); await reporter.WriteSnapshotAsync(name, snapshot, output, token).ConfigureAwait(false); return result.Errors.Count == 0 ? 0 : 4; }
        return await InvalidAsync(output, "Syntaxe attendue : snapshot create|list|show|delete.").ConfigureAwait(false);
    }
    private static async Task<int> DiffAsync(Arguments arguments, InventoryService service, OperatingContext context, ISnapshotStore store, IReporter reporter, TextWriter output, CancellationToken token)
    {
        if (arguments.Positionals.Count != 2) return await InvalidAsync(output, "Syntaxe attendue : diff <snapshot-a> <snapshot-b|current>").ConfigureAwait(false);
        var before = await store.LoadAsync(arguments.Positionals[0], token).ConfigureAwait(false);
        var after = arguments.Positionals[1].Equals("current", StringComparison.OrdinalIgnoreCase) ? service.ToSnapshot(await service.CollectAsync(context, token).ConfigureAwait(false), context, Version) : await store.LoadAsync(arguments.Positionals[1], token).ConfigureAwait(false);
        var diff = new DiffService().Compare(before, after); await reporter.WriteDiffAsync(diff, output, token).ConfigureAwait(false); return diff.HasChanges ? 1 : 0;
    }
    private static async Task<int> InspectAsync(Arguments arguments, InventoryService service, OperatingContext context, IReporter reporter, TextWriter output, CancellationToken token)
    {
        if (arguments.Positionals.Count != 1) return await InvalidAsync(output, "Syntaxe attendue : inspect <entry-id>").ConfigureAwait(false);
        var result = await service.CollectAsync(context, token).ConfigureAwait(false); var entry = result.Entries.SingleOrDefault(item => item.StableId.Equals(arguments.Positionals[0], StringComparison.OrdinalIgnoreCase));
        if (entry is null) return await InvalidAsync(output, "L’entrée est introuvable dans l’inventaire actuel.").ConfigureAwait(false);
        await reporter.WriteInventoryAsync(new([entry], result.Errors, result.CollectedAtUtc), output, token).ConfigureAwait(false); return result.Errors.Count == 0 ? 0 : 4;
    }
    private static async Task<int> InvalidAsync(TextWriter writer, string message) { await writer.WriteLineAsync(message).ConfigureAwait(false); return 2; }
    private static OperatingContext GetContext()
    {
        var elevated = false;
        if (OperatingSystem.IsWindows()) { using var identity = WindowsIdentity.GetCurrent(); elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }
        return new(Environment.MachineName, Environment.OSVersion.VersionString, Environment.Is64BitOperatingSystem ? "x64" : "x86", Environment.UserName, elevated);
    }
    private static Arguments Parse(string[] args)
    {
        var storage = default(string); var json = false; var remaining = new List<string>(); var help = false; var version = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help" or "-h": help = true; break;
                case "--version": version = true; break;
                case "--format": if (++index >= args.Length || !args[index].Equals("json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Seul --format json est pris en charge."); json = true; break;
                case "--storage": if (++index >= args.Length) throw new ArgumentException("--storage requiert un dossier."); storage = args[index]; break;
                case "--name": if (++index >= args.Length) throw new ArgumentException("--name requiert une valeur."); remaining.Add(args[index]); break;
                default: remaining.Add(args[index]); break;
            }
        }
        var command = remaining.Count > 0 ? remaining[0] : string.Empty;
        var positionals = remaining.Skip(1).ToList();
        if (command == "snapshot" && positionals.Count > 0 && positionals[0] == "create" && positionals.Count == 1 && args.Contains("--name", StringComparer.Ordinal)) { /* name already appended */ }
        return new(command, positionals, storage, json, help, version);
    }
    private sealed record Arguments(string Command, IReadOnlyList<string> Positionals, string? StorageDirectory, bool Json, bool ShowHelp, bool ShowVersion);
    private const string Help = "PersistLens - inventaire local des mécanismes de persistance Windows\n\nCommandes :\n  persistlens inventory [--format json] [--storage DOSSIER]\n  persistlens snapshot create --name NOM [--storage DOSSIER]\n  persistlens snapshot list|show NOM|delete NOM [--storage DOSSIER]\n  persistlens diff SNAPSHOT-A SNAPSHOT-B|current [--format json] [--storage DOSSIER]\n  persistlens inspect ENTRY-ID [--format json]\n\nCodes de sortie : 0 succès ; 1 différences détectées ; 2 entrée invalide ; 3 erreur opérationnelle ; 4 collecte partielle.";
}
