namespace PersistLens.Domain;

public enum PersistenceType { RegistryRun, Service, ScheduledTask, StartupFolder }
public enum ChangeType { Added, Removed, Modified }
public enum SignatureStatus
{
    SignedAndTrusted,
    SignedButUntrusted,
    InvalidSignature,
    ExpiredSignature,
    RevokedCertificate,
    UntrustedRoot,
    ExplicitDistrust,
    Unsigned,
    FileNotFound,
    AccessDenied,
    UnsupportedFileType,
    VerificationUnavailable,
    VerificationError,
    Unknown,
    Trusted = SignedAndTrusted,
    SignedUntrusted = SignedButUntrusted,
    Invalid = InvalidSignature,
    FileMissing = FileNotFound,
    Unsupported = VerificationUnavailable,
    Error = VerificationError,
}
public enum EvidenceConfidence { None, Low, Medium, High }
