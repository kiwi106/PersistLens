using PersistLens.Domain;
using PersistLens.Enrichment.Windows;

namespace PersistLens.IntegrationTests;

public sealed class AuthenticodeTests
{
    [Theory]
    [InlineData(WinVerifyTrustStatusMapper.Success, SignatureStatus.SignedAndTrusted)]
    [InlineData(WinVerifyTrustStatusMapper.TrustENoSignature, SignatureStatus.Unsigned)]
    [InlineData(WinVerifyTrustStatusMapper.TrustEBadDigest, SignatureStatus.InvalidSignature)]
    [InlineData(WinVerifyTrustStatusMapper.CertEExpired, SignatureStatus.ExpiredSignature)]
    [InlineData(WinVerifyTrustStatusMapper.CertERevoked, SignatureStatus.RevokedCertificate)]
    [InlineData(WinVerifyTrustStatusMapper.CertEUntrustedRoot, SignatureStatus.UntrustedRoot)]
    [InlineData(WinVerifyTrustStatusMapper.TrustEExplicitDistrust, SignatureStatus.ExplicitDistrust)]
    [InlineData(WinVerifyTrustStatusMapper.CryptERevocationOffline, SignatureStatus.VerificationUnavailable)]
    [InlineData(WinVerifyTrustStatusMapper.AccessDenied, SignatureStatus.AccessDenied)]
    public void Mapper_returns_explicit_business_status(int nativeStatus, SignatureStatus expected) => Assert.Equal(expected, WinVerifyTrustStatusMapper.Map(nativeStatus).Status);

    [Fact]
    public void Mapper_keeps_unknown_native_status_without_inventing_meaning()
    {
        var evidence = WinVerifyTrustStatusMapper.Map(unchecked((int)0x81234567));
        Assert.Equal(SignatureStatus.Unknown, evidence.Status);
        Assert.Equal(unchecked((int)0x81234567), evidence.NativeStatus);
    }

    [Fact]
    public async Task Windows_verifier_handles_a_signed_system_binary()
    {
        if (!OperatingSystem.IsWindows()) return;
        var evidence = await new WindowsAuthenticodeVerifier().VerifyAsync(Path.Combine(Environment.SystemDirectory, "kernel32.dll"), CancellationToken.None);
        Assert.True(evidence.HasSignature, $"Statut={evidence.Status}, HRESULT={evidence.NativeStatus}");
        Assert.NotEqual(SignatureStatus.Unsigned, evidence.Status);
        Assert.NotNull(evidence.NativeStatus);
    }

    [Fact]
    public async Task Windows_verifier_reports_an_unsigned_local_file()
    {
        var evidence = await new WindowsAuthenticodeVerifier().VerifyAsync(typeof(AuthenticodeTests).Assembly.Location, CancellationToken.None);
        Assert.Equal(SignatureStatus.Unsigned, evidence.Status);
        Assert.False(evidence.HasSignature);
    }

    [Fact]
    public async Task Windows_verifier_reports_a_missing_file()
    {
        var evidence = await new WindowsAuthenticodeVerifier().VerifyAsync(Path.Combine(Path.GetTempPath(), $"PersistLens-missing-{Guid.NewGuid():N}.exe"), CancellationToken.None);
        Assert.Equal(SignatureStatus.FileNotFound, evidence.Status);
    }
}
