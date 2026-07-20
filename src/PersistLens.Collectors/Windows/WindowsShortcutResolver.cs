using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using PersistLens.Domain;

namespace PersistLens.Collectors.Windows;

public sealed class WindowsShortcutResolver : IShortcutResolver
{
    private const uint SlrNoUi = 0x0001;
    private const uint SlrNoUpdate = 0x0008;
    private const uint SlrNoSearch = 0x0010;
    private const uint SlrNoTrack = 0x0020;
    private const uint SlgpRawPath = 0x0004;

    public Task<ShortcutResolutionResult> ResolveAsync(string shortcutPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return Task.FromResult(Result(shortcutPath, ShortcutResolutionStatus.Unsupported, null, null, null, null, "La résolution des raccourcis est disponible uniquement sous Windows.", EvidenceConfidence.None));
        if (string.IsNullOrWhiteSpace(shortcutPath) || !File.Exists(shortcutPath)) return Task.FromResult(Result(shortcutPath, ShortcutResolutionStatus.FileNotFound, null, null, null, null, "Le fichier raccourci est introuvable.", EvidenceConfidence.None));

        IShellLinkW? shellLink = null;
        try
        {
            shellLink = (IShellLinkW)new ShellLink();
            var persist = (IPersistFile)shellLink;
            persist.Load(shortcutPath, 0);
            cancellationToken.ThrowIfCancellationRequested();
            var resolveStatus = shellLink.Resolve(IntPtr.Zero, SlrNoUi | SlrNoUpdate | SlrNoSearch | SlrNoTrack);
            if (Failed(resolveStatus) && resolveStatus != HResultFileNotFound) return Task.FromResult(FromHResult(shortcutPath, resolveStatus));
            var rawTarget = ReadPath(shellLink);
            var arguments = Read(shellLink.GetArguments);
            var workingDirectory = Read(shellLink.GetWorkingDirectory);
            var description = Read(shellLink.GetDescription);
            var icon = ReadIcon(shellLink);
            if (string.IsNullOrWhiteSpace(rawTarget)) return Task.FromResult(Result(shortcutPath, ShortcutResolutionStatus.InvalidShortcut, null, null, null, null, "Le raccourci ne contient pas de cible exploitable.", EvidenceConfidence.Low));
            var expanded = Environment.ExpandEnvironmentVariables(rawTarget);
            var normalized = Normalize(expanded, workingDirectory, shortcutPath, out var normalizationError);
            var exists = normalized is not null && File.Exists(normalized);
            var status = !exists || resolveStatus == HResultFileNotFound ? ShortcutResolutionStatus.BrokenTarget : normalizationError is null ? ShortcutResolutionStatus.Resolved : ShortcutResolutionStatus.PartiallyResolved;
            var confidence = status == ShortcutResolutionStatus.Resolved ? EvidenceConfidence.High : status == ShortcutResolutionStatus.BrokenTarget ? EvidenceConfidence.Medium : EvidenceConfidence.Low;
            var message = status switch { ShortcutResolutionStatus.Resolved => "Raccourci résolu.", ShortcutResolutionStatus.BrokenTarget => "La cible du raccourci est introuvable.", _ => normalizationError ?? "Résolution partielle du raccourci." };
            var evidence = new ShortcutTargetEvidence(shortcutPath, status, rawTarget, expanded, normalized, arguments, workingDirectory, description, icon.Location, icon.Index, resolveStatus, message, confidence, normalizationError is null ? null : [normalizationError]);
            return Task.FromResult(new ShortcutResolutionResult(evidence, []));
        }
        catch (OperationCanceledException) { throw; }
        catch (UnauthorizedAccessException) { return Task.FromResult(Result(shortcutPath, ShortcutResolutionStatus.AccessDenied, null, null, null, null, "Accès refusé pendant la lecture du raccourci.", EvidenceConfidence.None)); }
        catch (COMException exception) { return Task.FromResult(FromHResult(shortcutPath, exception.HResult)); }
        catch (IOException) { return Task.FromResult(Result(shortcutPath, ShortcutResolutionStatus.ResolutionError, null, null, null, null, "Le raccourci n’a pas pu être lu.", EvidenceConfidence.Low)); }
        catch (Exception) { return Task.FromResult(Result(shortcutPath, ShortcutResolutionStatus.ResolutionError, null, null, null, null, "La résolution du raccourci a échoué.", EvidenceConfidence.Low)); }
        finally { if (shellLink is not null && Marshal.IsComObject(shellLink)) Marshal.FinalReleaseComObject(shellLink); }
    }

    private static ShortcutResolutionResult FromHResult(string path, int hresult) => hresult switch
    {
        HResultFileNotFound => Result(path, ShortcutResolutionStatus.FileNotFound, null, null, null, hresult, "Le raccourci ou sa cible est introuvable.", EvidenceConfidence.None),
        HResultAccessDenied => Result(path, ShortcutResolutionStatus.AccessDenied, null, null, null, hresult, "Accès refusé pendant la résolution du raccourci.", EvidenceConfidence.None),
        HResultCancelled => Result(path, ShortcutResolutionStatus.Cancelled, null, null, null, hresult, "La résolution du raccourci a été annulée.", EvidenceConfidence.None),
        _ => Result(path, ShortcutResolutionStatus.Unknown, null, null, null, hresult, "Le raccourci a retourné un code Windows inconnu.", EvidenceConfidence.Low)
    };

    private static ShortcutResolutionResult Result(string shortcutPath, ShortcutResolutionStatus status, string? target, string? arguments, string? workingDirectory, int? nativeStatus, string message, EvidenceConfidence confidence) => new(new(shortcutPath, status, target, target, target, arguments, workingDirectory, null, null, null, nativeStatus, message, confidence), []);
    private static string? Normalize(string expanded, string? workingDirectory, string shortcutPath, out string? error)
    {
        try
        {
            var baseDirectory = !string.IsNullOrWhiteSpace(workingDirectory) && Path.IsPathFullyQualified(workingDirectory) ? workingDirectory : Path.GetDirectoryName(shortcutPath) ?? Environment.CurrentDirectory;
            error = null;
            return Path.GetFullPath(Path.IsPathFullyQualified(expanded) ? expanded : Path.Combine(baseDirectory, expanded));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { error = "La cible du raccourci n’a pas pu être normalisée."; return null; }
    }
    private static string? Read(Func<StringBuilder, int, int> action) { var value = new StringBuilder(32768); return action(value, value.Capacity) >= 0 && value.Length > 0 ? value.ToString() : null; }
    private static string? ReadPath(IShellLinkW link) { var value = new StringBuilder(32768); return link.GetPath(value, value.Capacity, IntPtr.Zero, SlgpRawPath) >= 0 && value.Length > 0 ? value.ToString() : null; }
    private static (string? Location, int? Index) ReadIcon(IShellLinkW link) { var value = new StringBuilder(32768); return link.GetIconLocation(value, value.Capacity, out var index) >= 0 && value.Length > 0 ? (value.ToString(), index) : (null, null); }
    private static bool Failed(int hresult) => hresult < 0;
    private const int HResultFileNotFound = unchecked((int)0x80070002);
    private const int HResultAccessDenied = unchecked((int)0x80070005);
    private const int HResultCancelled = unchecked((int)0x800704C7);

    [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)] private class ShellLink;
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        [PreserveSig] int GetPath(StringBuilder file, int capacity, IntPtr fileData, uint flags);
        [PreserveSig] int GetIDList(IntPtr itemIdList); [PreserveSig] int SetIDList(IntPtr itemIdList);
        [PreserveSig] int GetDescription(StringBuilder description, int capacity); [PreserveSig] int SetDescription(string description);
        [PreserveSig] int GetWorkingDirectory(StringBuilder directory, int capacity); [PreserveSig] int SetWorkingDirectory(string directory);
        [PreserveSig] int GetArguments(StringBuilder arguments, int capacity); [PreserveSig] int SetArguments(string arguments);
        [PreserveSig] int GetHotkey(out short hotkey); [PreserveSig] int SetHotkey(short hotkey);
        [PreserveSig] int GetShowCmd(out int showCommand); [PreserveSig] int SetShowCmd(int showCommand);
        [PreserveSig] int GetIconLocation(StringBuilder iconPath, int capacity, out int iconIndex); [PreserveSig] int SetIconLocation(string iconPath, int iconIndex);
        [PreserveSig] int SetRelativePath(string relativePath, uint reserved); [PreserveSig] int Resolve(IntPtr window, uint flags); [PreserveSig] int SetPath(string file);
    }
}
