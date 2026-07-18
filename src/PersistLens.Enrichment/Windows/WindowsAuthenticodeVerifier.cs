using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PersistLens.Domain;

namespace PersistLens.Enrichment.Windows;

/// <summary>Validates local files with WinVerifyTrust without executing or loading the target file.</summary>
public sealed class WindowsAuthenticodeVerifier : IAuthenticodeVerifier
{
    private static readonly Guid VerifyV2Action = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
    private const uint UiNone = 2;
    private const uint UnionChoiceFile = 1;
    private const uint StateActionVerify = 1;
    private const uint StateActionClose = 2;
    private const uint RevocationCheckChainExcludeRoot = 0x00000080;
    private const uint CacheOnlyUrlRetrieval = 0x00001000;

    public Task<SignatureEvidence> VerifyAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return Task.FromResult(new SignatureEvidence(SignatureStatus.VerificationUnavailable, null, null, null, null, null, "Vérification Authenticode disponible uniquement sous Windows.", null, null, null, null, null, null, null, null, null, "WinVerifyTrust"));
        if (!File.Exists(filePath)) return Task.FromResult(WinVerifyTrustStatusMapper.Map(WinVerifyTrustStatusMapper.FileNotFound));
        try { return Task.FromResult(Verify(filePath, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Task.FromResult(WinVerifyTrustStatusMapper.Map(WinVerifyTrustStatusMapper.AccessDenied)); }
        catch (IOException exception) { return Task.FromResult(TechnicalError(exception)); }
        catch (CryptographicException exception) { return Task.FromResult(TechnicalError(exception)); }
    }

    private static SignatureEvidence Verify(string path, CancellationToken cancellationToken)
    {
        var fileInfo = new WinTrustFileInfo(path); var filePointer = IntPtr.Zero; var stateCreated = false; var data = default(WinTrustData);
        try
        {
            filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
            Marshal.StructureToPtr(fileInfo, filePointer, false);
            data = WinTrustData.Create(filePointer);
            var action = VerifyV2Action;
            var status = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            stateCreated = data.StateData != IntPtr.Zero;
            cancellationToken.ThrowIfCancellationRequested();
            return WinVerifyTrustStatusMapper.Map(status, ReadCertificate(path));
        }
        finally
        {
            if (stateCreated)
            {
                var closeData = data;
                closeData.StateAction = StateActionClose;
                var action = VerifyV2Action;
                _ = WinVerifyTrust(IntPtr.Zero, ref action, ref closeData);
            }
            if (filePointer != IntPtr.Zero) Marshal.FreeHGlobal(filePointer);
        }
    }

    private static CertificateDetails? ReadCertificate(string path)
    {
        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            return new(certificate.Subject, certificate.Issuer, certificate.Thumbprint, certificate.NotBefore, certificate.NotAfter, certificate.SerialNumber, certificate.SignatureAlgorithm.FriendlyName);
        }
        catch (CryptographicException) { return null; }
    }

    private static SignatureEvidence TechnicalError(Exception exception) => new(SignatureStatus.VerificationError, null, null, null, null, null, "Erreur technique pendant la vérification Authenticode.", exception.HResult, null, null, null, null, null, null, null, null, "WinVerifyTrust", [exception.GetType().Name]);

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [In] ref Guid actionId, [In, Out] ref WinTrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
        public WinTrustFileInfo(string filePath) { StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(); FilePath = filePath; FileHandle = IntPtr.Zero; KnownSubject = IntPtr.Zero; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
        public static WinTrustData Create(IntPtr fileInfo) => new()
        {
            StructSize = (uint)Marshal.SizeOf<WinTrustData>(),
            UiChoice = UiNone,
            RevocationChecks = 1,
            UnionChoice = UnionChoiceFile,
            FileInfo = fileInfo,
            StateAction = StateActionVerify,
            ProviderFlags = RevocationCheckChainExcludeRoot | CacheOnlyUrlRetrieval,
        };
    }
}
