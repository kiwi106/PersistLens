namespace PersistLens.Domain;

public sealed record PersistenceCommand(
    string Raw,
    string? ExecutablePath,
    string? Arguments,
    string? WorkingDirectory,
    IReadOnlyList<string> Candidates,
    EvidenceConfidence Confidence,
    string? UncertaintyReason)
{
    public static PersistenceCommand RawOnly(string raw, string reason) =>
        new(raw, null, null, null, Array.Empty<string>(), EvidenceConfidence.None, reason);
}
