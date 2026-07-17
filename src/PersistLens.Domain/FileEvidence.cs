namespace PersistLens.Domain;

public sealed record SignatureEvidence(
    SignatureStatus Status,
    string? Subject,
    string? Issuer,
    string? Thumbprint,
    DateTimeOffset? NotBeforeUtc,
    DateTimeOffset? NotAfterUtc,
    string? TrustMessage);

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
