using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace PersistLens.Domain;

public sealed record PersistenceLocation(string Value, string? RegistryView = null);

public sealed record PersistenceEntry(
    string StableId,
    PersistenceType Type,
    string Collector,
    PersistenceLocation Location,
    string Name,
    string RawValue,
    PersistenceCommand Command,
    string? RunAs,
    IReadOnlyDictionary<string, string> Metadata,
    FileEvidence? FileEvidence,
    DateTimeOffset CollectedAtUtc,
    string StateHash)
{
    public static PersistenceEntry Create(
        PersistenceType type, string collector, PersistenceLocation location, string name, string rawValue,
        PersistenceCommand command, string? runAs, IReadOnlyDictionary<string, string>? metadata,
        FileEvidence? evidence, DateTimeOffset collectedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collector);
        ArgumentException.ThrowIfNullOrWhiteSpace(location.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var safeMetadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal));
        var identity = Canonical(type.ToString(), location.Value, name, runAs ?? string.Empty);
        var state = Canonical(identity, rawValue, command.Raw, command.ExecutablePath ?? string.Empty, command.Arguments ?? string.Empty,
            command.WorkingDirectory ?? string.Empty, evidence?.Sha256 ?? string.Empty, evidence?.Signature.Status.ToString() ?? string.Empty,
            string.Join("|", safeMetadata.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}")));
        return new(Hash(identity), type, collector, location, name, rawValue, command, runAs, safeMetadata, evidence, collectedAtUtc, Hash(state));
    }

    private static string Canonical(params string[] values) => string.Join("\u001f", values.Select(value => value.Trim().ToUpperInvariant()));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
