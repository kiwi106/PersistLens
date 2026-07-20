using PersistLens.Collectors;
using PersistLens.Collectors.Windows;
using PersistLens.Domain;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace PersistLens.Collectors.Tests;

public sealed class StartupShortcutCollectorTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PersistLensShortcutTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CollectAsync_keeps_shortcut_and_records_resolved_target_separately()
    {
        var path = Create("agent.lnk");
        var result = await Collector(new FakeResolver(Resolved(path))).CollectAsync(Context, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(path, entry.RawValue);
        Assert.Equal(path, entry.Command.Raw);
        Assert.Equal(@"C:\Fixture\agent.exe", entry.Command.ExecutablePath);
        Assert.Equal("--quiet", entry.Command.Arguments);
        Assert.Equal(@"C:\Fixture", entry.Command.WorkingDirectory);
        Assert.Equal(ShortcutResolutionStatus.Resolved, entry.Shortcut!.Status);
        Assert.Equal(@"C:\Fixture\agent.exe", entry.Shortcut.NormalizedTargetPath);
    }

    [Fact]
    public async Task CollectAsync_keeps_broken_shortcut_and_reports_partial_resolution_error()
    {
        var path = Create("broken.lnk");
        var evidence = Resolved(path) with { Status = ShortcutResolutionStatus.BrokenTarget, UserMessage = "La cible du raccourci est introuvable.", PartialErrors = ["La cible du raccourci est introuvable."] };
        var result = await Collector(new FakeResolver(evidence)).CollectAsync(Context, CancellationToken.None);

        Assert.Single(result.Entries);
        Assert.Equal(ShortcutResolutionStatus.BrokenTarget, result.Entries[0].Shortcut!.Status);
        Assert.Single(result.Errors);
        Assert.Equal("BrokenTarget", result.Errors[0].ErrorCode);
        Assert.DoesNotContain(path, result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectAsync_leaves_non_shortcut_behavior_unchanged()
    {
        Create("plain.exe");
        var resolver = new FakeResolver(Resolved("unused"));
        var result = await Collector(resolver).CollectAsync(Context, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Null(entry.Shortcut);
        Assert.Equal(0, resolver.Calls);
        Assert.EndsWith("plain.exe", entry.Command.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollectAsync_propagates_cancellation_without_resolving()
    {
        Create("cancel.lnk");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Collector(new FakeResolver(Resolved("unused"))).CollectAsync(Context, cancellation.Token));
    }

    [Fact]
    public async Task Windows_resolver_reports_missing_shortcut_without_starting_a_process()
    {
        var result = await new WindowsShortcutResolver().ResolveAsync(Path.Combine(directory, "missing.lnk"), CancellationToken.None);
        Assert.Equal(ShortcutResolutionStatus.FileNotFound, result.Evidence.Status);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Windows_resolver_reads_a_real_temporary_shortcut_without_executing_its_target()
    {
        if (!OperatingSystem.IsWindows()) return;
        var target = Create("target.exe"); var shortcut = Path.Combine(directory, "valid.lnk");
        ShortcutFixtureWriter.Create(shortcut, target, "--fixture", directory);
        var result = await new WindowsShortcutResolver().ResolveAsync(shortcut, CancellationToken.None);
        Assert.Equal(ShortcutResolutionStatus.Resolved, result.Evidence.Status);
        Assert.Equal(Path.GetFullPath(target), result.Evidence.NormalizedTargetPath);
        Assert.Equal("--fixture", result.Evidence.Arguments);
        Assert.Equal(directory, result.Evidence.WorkingDirectory);
        Assert.True(File.Exists(target));
    }

    private StartupFolderCollector Collector(IShortcutResolver resolver) => new(new WindowsCommandParser(), new FixedClock(), resolver, [directory]);
    private string Create(string name) { Directory.CreateDirectory(directory); var path = Path.Combine(directory, name); File.WriteAllText(path, "fixture"); return path; }
    private static ShortcutTargetEvidence Resolved(string shortcut) => new(shortcut, ShortcutResolutionStatus.Resolved, @"%SystemRoot%\System32\agent.exe", @"C:\Windows\System32\agent.exe", @"C:\Fixture\agent.exe", "--quiet", @"C:\Fixture", "fixture", null, null, 0, "Raccourci résolu.", EvidenceConfidence.High);
    private static readonly CollectionContext Context = new(new("fixture", null, "x64", "fixture", false));
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch; }
    private sealed class FakeResolver(ShortcutTargetEvidence evidence) : IShortcutResolver
    {
        public int Calls { get; private set; }
        public Task<ShortcutResolutionResult> ResolveAsync(string shortcutPath, CancellationToken cancellationToken) { Calls++; return Task.FromResult(new ShortcutResolutionResult(evidence with { ShortcutPath = shortcutPath }, [])); }
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [SupportedOSPlatform("windows")]
    private static class ShortcutFixtureWriter
    {
        public static void Create(string shortcutPath, string targetPath, string arguments, string workingDirectory)
        {
            IShellLinkW? link = null;
            try
            {
                link = (IShellLinkW)new ShellLink();
                Assert.Equal(0, link.SetPath(targetPath)); Assert.Equal(0, link.SetArguments(arguments)); Assert.Equal(0, link.SetWorkingDirectory(workingDirectory));
                ((IPersistFile)link).Save(shortcutPath, true);
            }
            finally { if (link is not null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link); }
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)] private class ShellLink;
        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            [PreserveSig] int GetPath(System.Text.StringBuilder file, int capacity, IntPtr fileData, uint flags);
            [PreserveSig] int GetIDList(IntPtr itemIdList); [PreserveSig] int SetIDList(IntPtr itemIdList);
            [PreserveSig] int GetDescription(System.Text.StringBuilder description, int capacity); [PreserveSig] int SetDescription(string description);
            [PreserveSig] int GetWorkingDirectory(System.Text.StringBuilder directory, int capacity); [PreserveSig] int SetWorkingDirectory(string directory);
            [PreserveSig] int GetArguments(System.Text.StringBuilder arguments, int capacity); [PreserveSig] int SetArguments(string arguments);
            [PreserveSig] int GetHotkey(out short hotkey); [PreserveSig] int SetHotkey(short hotkey);
            [PreserveSig] int GetShowCmd(out int showCommand); [PreserveSig] int SetShowCmd(int showCommand);
            [PreserveSig] int GetIconLocation(System.Text.StringBuilder iconPath, int capacity, out int iconIndex); [PreserveSig] int SetIconLocation(string iconPath, int iconIndex);
            [PreserveSig] int SetRelativePath(string relativePath, uint reserved); [PreserveSig] int Resolve(IntPtr window, uint flags); [PreserveSig] int SetPath(string file);
        }
    }
}
