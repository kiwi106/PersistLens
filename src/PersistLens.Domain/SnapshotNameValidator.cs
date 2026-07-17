namespace PersistLens.Domain;

public static class SnapshotNameValidator
{
    public const int MaximumLength = 64;
    public static bool IsValid(string? name) => name is { Length: > 0 and <= MaximumLength } && name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    public static string Validate(string? name) => IsValid(name) ? name! : throw new ArgumentException("Snapshot names must be 1-64 ASCII letters, digits, hyphens, or underscores.", nameof(name));
}
