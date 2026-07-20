namespace PersistLens.Domain;

public enum ShortcutResolutionStatus
{
    Resolved,
    PartiallyResolved,
    BrokenTarget,
    InvalidShortcut,
    FileNotFound,
    AccessDenied,
    Unsupported,
    Cancelled,
    ResolutionError,
    Unknown
}

public sealed record ShortcutTargetEvidence(
    string ShortcutPath,
    ShortcutResolutionStatus Status,
    string? RawTargetPath,
    string? ExpandedTargetPath,
    string? NormalizedTargetPath,
    string? Arguments,
    string? WorkingDirectory,
    string? Description,
    string? IconLocation,
    int? IconIndex,
    int? NativeStatus,
    string? UserMessage,
    EvidenceConfidence Confidence,
    IReadOnlyList<string>? PartialErrors = null);

public sealed record ShortcutResolutionResult(ShortcutTargetEvidence Evidence, IReadOnlyList<CollectionError> Errors);

public interface IShortcutResolver
{
    Task<ShortcutResolutionResult> ResolveAsync(string shortcutPath, CancellationToken cancellationToken);
}
