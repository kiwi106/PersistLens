using PersistLens.Domain;

namespace PersistLens.Enrichment.Windows;

internal static class WinVerifyTrustStatusMapper
{
    internal const int Success = 0;
    internal const int TrustENoSignature = unchecked((int)0x800B0100);
    internal const int CertEExpired = unchecked((int)0x800B0101);
    internal const int CertEUntrustedRoot = unchecked((int)0x800B0109);
    internal const int CertEChain = unchecked((int)0x800B010A);
    internal const int CertERevoked = unchecked((int)0x800B010C);
    internal const int CertERevocationFailure = unchecked((int)0x800B010E);
    internal const int TrustEExplicitDistrust = unchecked((int)0x800B0111);
    internal const int TrustESubjectNotTrusted = unchecked((int)0x800B0004);
    internal const int TrustEProviderUnknown = unchecked((int)0x800B0001);
    internal const int TrustEActionUnknown = unchecked((int)0x800B0002);
    internal const int TrustESubjectFormUnknown = unchecked((int)0x800B0003);
    internal const int TrustEBadDigest = unchecked((int)0x80096010);
    internal const int CryptERevocationOffline = unchecked((int)0x80092013);
    internal const int FileNotFound = unchecked((int)0x80070002);
    internal const int AccessDenied = unchecked((int)0x80070005);

    public static SignatureEvidence Map(int nativeStatus, CertificateDetails? certificate = null)
    {
        var status = nativeStatus switch
        {
            Success => SignatureStatus.SignedAndTrusted,
            TrustENoSignature => SignatureStatus.Unsigned,
            TrustEBadDigest => SignatureStatus.InvalidSignature,
            CertEExpired => SignatureStatus.ExpiredSignature,
            CertERevoked => SignatureStatus.RevokedCertificate,
            CertEUntrustedRoot => SignatureStatus.UntrustedRoot,
            TrustEExplicitDistrust => SignatureStatus.ExplicitDistrust,
            TrustESubjectNotTrusted or CertEChain => SignatureStatus.SignedButUntrusted,
            CertERevocationFailure or CryptERevocationOffline => SignatureStatus.VerificationUnavailable,
            TrustEProviderUnknown or TrustEActionUnknown or TrustESubjectFormUnknown => SignatureStatus.UnsupportedFileType,
            FileNotFound => SignatureStatus.FileNotFound,
            AccessDenied => SignatureStatus.AccessDenied,
            _ => SignatureStatus.Unknown,
        };
        var hasSignature = status is not SignatureStatus.Unsigned and not SignatureStatus.FileNotFound and not SignatureStatus.AccessDenied and not SignatureStatus.UnsupportedFileType;
        var isValid = status is SignatureStatus.SignedAndTrusted or SignatureStatus.SignedButUntrusted or SignatureStatus.ExpiredSignature or SignatureStatus.RevokedCertificate or SignatureStatus.UntrustedRoot or SignatureStatus.ExplicitDistrust or SignatureStatus.VerificationUnavailable;
        var trusted = status == SignatureStatus.SignedAndTrusted;
        return new(status, certificate?.Subject, certificate?.Issuer, certificate?.Thumbprint, certificate?.NotBeforeUtc, certificate?.NotAfterUtc, Message(status), nativeStatus,
            hasSignature, isValid, trusted, null, null, null, certificate?.SerialNumber, certificate?.SignatureAlgorithm, "WinVerifyTrust (révocation hors ligne/cache uniquement)", certificate?.PartialErrors);
    }

    private static string Message(SignatureStatus status) => status switch
    {
        SignatureStatus.SignedAndTrusted => "Signé et approuvé par Windows.",
        SignatureStatus.SignedButUntrusted => "Signé, mais la chaîne de certificats n’est pas approuvée.",
        SignatureStatus.InvalidSignature => "Signature invalide.",
        SignatureStatus.ExpiredSignature => "Signature ou certificat expiré.",
        SignatureStatus.RevokedCertificate => "Certificat révoqué.",
        SignatureStatus.UntrustedRoot => "Racine de confiance inconnue.",
        SignatureStatus.ExplicitDistrust => "Certificat explicitement non approuvé.",
        SignatureStatus.Unsigned => "Fichier non signé.",
        SignatureStatus.FileNotFound => "Fichier introuvable.",
        SignatureStatus.AccessDenied => "Accès refusé.",
        SignatureStatus.UnsupportedFileType => "Type de fichier non pris en charge par Authenticode.",
        SignatureStatus.VerificationUnavailable => "Vérification Authenticode impossible : révocation non vérifiable hors ligne.",
        _ => "Vérification Authenticode impossible.",
    };
}

internal sealed record CertificateDetails(string? Subject, string? Issuer, string? Thumbprint, DateTimeOffset? NotBeforeUtc, DateTimeOffset? NotAfterUtc, string? SerialNumber, string? SignatureAlgorithm, IReadOnlyList<string>? PartialErrors = null);
