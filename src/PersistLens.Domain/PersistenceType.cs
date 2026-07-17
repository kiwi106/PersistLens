namespace PersistLens.Domain;

public enum PersistenceType { RegistryRun, Service, ScheduledTask, StartupFolder }
public enum ChangeType { Added, Removed, Modified }
public enum SignatureStatus { Trusted, SignedUntrusted, Invalid, Unsigned, FileMissing, AccessDenied, Unsupported, Error }
public enum EvidenceConfidence { None, Low, Medium, High }
