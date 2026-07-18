namespace PersistLens.Domain;

public sealed record SignatureEvidence(
    SignatureStatus Status,
    string? Subject,
    string? Issuer,
    string? Thumbprint,
    DateTimeOffset? NotBeforeUtc,
    DateTimeOffset? NotAfterUtc,
    string? TrustMessage,
    int? NativeStatus = null,
    bool? HasSignature = null,
    bool? IsCryptographicallyValid = null,
    bool? IsChainTrusted = null,
    bool? HasTimestamp = null,
    DateTimeOffset? TimestampUtc = null,
    string? TimestampSubject = null,
    string? CertificateSerialNumber = null,
    string? SignatureAlgorithm = null,
    string? TrustSource = null,
    IReadOnlyList<string>? PartialErrors = null);

public sealed record FileEvidence(
    string RawPath,
    string? ExpandedPath,
    string? NormalizedPath,
    bool Exists,
    string? FileType,
    long? SizeBytes,
    DateTimeOffset? CreationTimeUtc,
    DateTimeOffset? LastWriteTimeUtc,
    string? Sha256,
    string? Owner,
    SignatureEvidence Signature,
    EvidenceConfidence Confidence,
    string? Limitation);
