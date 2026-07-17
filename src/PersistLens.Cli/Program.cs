using System.Security.Principal;
using PersistLens.Application;
using PersistLens.Collectors;
using PersistLens.Collectors.Windows;
using PersistLens.Domain;
using PersistLens.Enrichment;
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
            var inventory = new InventoryService([new RegistryRunCollector(commandParser, clock), new ServiceCollector(commandParser, clock), new ScheduledTaskCollector(new WindowsTaskSchedulerSource(), commandParser, clock), new StartupFolderCollector(commandParser, clock)], clock, new StreamingFileEvidenceProvider());
            var context = GetContext(); var store = new JsonSnapshotStore(parsed.StorageDirectory); IReporter reporter = parsed.Json ? new JsonReporter() : new TerminalReporter();
            return parsed.Command switch
            {
                "inventory" => await InventoryAsync(inventory, context, reporter, output, cancellation.Token).ConfigureAwait(false),
                "snapshot" => await SnapshotAsync(parsed, inventory, context, store, reporter, output, cancellation.Token).ConfigureAwait(false),
                "diff" => await DiffAsync(parsed, inventory, context, store, reporter, output, cancellation.Token).ConfigureAwait(false),
                "inspect" => await InspectAsync(parsed, inventory, context, reporter, output, cancellation.Token).ConfigureAwait(false),
                _ => await InvalidAsync(error, "Unknown or missing command. Run persistlens --help.").ConfigureAwait(false)
            };
        }
        catch (OperationCanceledException) { await error.WriteLineAsync("Operation cancelled.").ConfigureAwait(false); return 3; }
        catch (ArgumentException exception) { await error.WriteLineAsync(exception.Message).ConfigureAwait(false); return 2; }
        catch (IOException exception) { await error.WriteLineAsync($"Operational error: {exception.Message}").ConfigureAwait(false); return 3; }
        finally { Console.CancelKeyPress -= Cancel; }

        void Cancel(object? _, ConsoleCancelEventArgs eventArgs) { eventArgs.Cancel = true; cancellation.Cancel(); }
    }

    private static async Task<int> InventoryAsync(InventoryService service, OperatingContext context, IReporter reporter, TextWriter output, CancellationToken token)
    {
        var result = await service.CollectAsync(context, token).ConfigureAwait(false); await reporter.WriteInventoryAsync(result, output, token).ConfigureAwait(false); return result.Errors.Count == 0 ? 0 : 4;
    }
    private static async Task<int> SnapshotAsync(Arguments arguments, InventoryService service, OperatingContext context, ISnapshotStore store, IReporter reporter, TextWriter output, CancellationToken token)
    {
        if (arguments.Positionals.Count == 0) return await InvalidAsync(output, "Expected: snapshot create|list|show|delete [name]").ConfigureAwait(false);
        var action = arguments.Positionals[0];
        if (action == "list") { foreach (var snapshotName in await store.ListAsync(token).ConfigureAwait(false)) await output.WriteLineAsync(snapshotName).ConfigureAwait(false); return 0; }
        if (arguments.Positionals.Count < 2) return await InvalidAsync(output, "A snapshot name is required.").ConfigureAwait(false);
        var name = arguments.Positionals[1];
        if (action == "delete") { await store.DeleteAsync(name, token).ConfigureAwait(false); await output.WriteLineAsync($"Deleted snapshot '{name}'.").ConfigureAwait(false); return 0; }
        if (action == "show") { await reporter.WriteSnapshotAsync(name, await store.LoadAsync(name, token).ConfigureAwait(false), output, token).ConfigureAwait(false); return 0; }
        if (action == "create") { var result = await service.CollectAsync(context, token).ConfigureAwait(false); var snapshot = service.ToSnapshot(result, context, Version); await store.SaveAsync(name, snapshot, token).ConfigureAwait(false); await reporter.WriteSnapshotAsync(name, snapshot, output, token).ConfigureAwait(false); return result.Errors.Count == 0 ? 0 : 4; }
        return await InvalidAsync(output, "Expected: snapshot create|list|show|delete.").ConfigureAwait(false);
    }
    private static async Task<int> DiffAsync(Arguments arguments, InventoryService service, OperatingContext context, ISnapshotStore store, IReporter reporter, TextWriter output, CancellationToken token)
    {
        if (arguments.Positionals.Count != 2) return await InvalidAsync(output, "Expected: diff <snapshot-a> <snapshot-b|current>").ConfigureAwait(false);
        var before = await store.LoadAsync(arguments.Positionals[0], token).ConfigureAwait(false);
        var after = arguments.Positionals[1].Equals("current", StringComparison.OrdinalIgnoreCase) ? service.ToSnapshot(await service.CollectAsync(context, token).ConfigureAwait(false), context, Version) : await store.LoadAsync(arguments.Positionals[1], token).ConfigureAwait(false);
        var diff = new DiffService().Compare(before, after); await reporter.WriteDiffAsync(diff, output, token).ConfigureAwait(false); return diff.HasChanges ? 1 : 0;
    }
    private static async Task<int> InspectAsync(Arguments arguments, InventoryService service, OperatingContext context, IReporter reporter, TextWriter output, CancellationToken token)
    {
        if (arguments.Positionals.Count != 1) return await InvalidAsync(output, "Expected: inspect <entry-id>").ConfigureAwait(false);
        var result = await service.CollectAsync(context, token).ConfigureAwait(false); var entry = result.Entries.SingleOrDefault(item => item.StableId.Equals(arguments.Positionals[0], StringComparison.OrdinalIgnoreCase));
        if (entry is null) return await InvalidAsync(output, "Entry was not found in the current inventory.").ConfigureAwait(false);
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
                case "--format": if (++index >= args.Length || !args[index].Equals("json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only --format json is supported."); json = true; break;
                case "--storage": if (++index >= args.Length) throw new ArgumentException("--storage requires a directory."); storage = args[index]; break;
                case "--name": if (++index >= args.Length) throw new ArgumentException("--name requires a value."); remaining.Add(args[index]); break;
                default: remaining.Add(args[index]); break;
            }
        }
        var command = remaining.Count > 0 ? remaining[0] : string.Empty;
        var positionals = remaining.Skip(1).ToList();
        if (command == "snapshot" && positionals.Count > 0 && positionals[0] == "create" && positionals.Count == 1 && args.Contains("--name", StringComparer.Ordinal)) { /* name already appended */ }
        return new(command, positionals, storage, json, help, version);
    }
    private sealed record Arguments(string Command, IReadOnlyList<string> Positionals, string? StorageDirectory, bool Json, bool ShowHelp, bool ShowVersion);
    private const string Help = "PersistLens - local-first Windows persistence inventory\n\nCommands:\n  persistlens inventory [--format json] [--storage DIR]\n  persistlens snapshot create --name NAME [--storage DIR]\n  persistlens snapshot list|show NAME|delete NAME [--storage DIR]\n  persistlens diff SNAPSHOT-A SNAPSHOT-B|current [--format json] [--storage DIR]\n  persistlens inspect ENTRY-ID [--format json]\n\nExit codes: 0 success; 1 differences found; 2 invalid input; 3 operational error; 4 partial collection.";
}
