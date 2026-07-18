using PersistLens.Domain;

namespace PersistLens.Application;

public sealed class DiffService
{
    public DiffResult Compare(PersistenceSnapshot before, PersistenceSnapshot after)
    {
        var oldEntries = before.Entries.ToDictionary(entry => entry.StableId, StringComparer.Ordinal);
        var newEntries = after.Entries.ToDictionary(entry => entry.StableId, StringComparer.Ordinal);
        var changes = new List<PersistenceChange>();
        foreach (var id in oldEntries.Keys.Union(newEntries.Keys, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            oldEntries.TryGetValue(id, out var oldEntry);
            newEntries.TryGetValue(id, out var newEntry);
            if (oldEntry is null) changes.Add(Create(ChangeType.Added, null, newEntry!));
            else if (newEntry is null) changes.Add(Create(ChangeType.Removed, oldEntry, null));
            else if (!StringComparer.Ordinal.Equals(oldEntry.StateHash, newEntry.StateHash)) changes.Add(Create(ChangeType.Modified, oldEntry, newEntry));
        }
        return new(before, after, changes);
    }

    private static PersistenceChange Create(ChangeType type, PersistenceEntry? before, PersistenceEntry? after)
    {
        var fields = type == ChangeType.Modified ? ChangedFields(before!, after!) : Array.Empty<string>();
        var item = after ?? before!;
        var indicators = CautionIndicators(item).ToArray();
        return new(type, item.StableId, before, after, fields, $"{Display(type)} : {item.Type} {item.Name}", indicators);
    }

    private static IReadOnlyList<string> ChangedFields(PersistenceEntry before, PersistenceEntry after)
    {
        var changed = new List<string>();
        if (!StringComparer.Ordinal.Equals(before.Command.Raw, after.Command.Raw)) changed.Add("command");
        if (!StringComparer.Ordinal.Equals(before.Command.ExecutablePath, after.Command.ExecutablePath)) changed.Add("targetPath");
        if (!StringComparer.Ordinal.Equals(before.Command.Arguments, after.Command.Arguments)) changed.Add("arguments");
        if (!StringComparer.Ordinal.Equals(before.RunAs, after.RunAs)) changed.Add("runAs");
        if (!StringComparer.Ordinal.Equals(before.FileEvidence?.Sha256, after.FileEvidence?.Sha256)) changed.Add("fileHash");
        if (before.FileEvidence?.Signature.Status != after.FileEvidence?.Signature.Status) changed.Add("signature");
        foreach (var key in before.Metadata.Keys.Union(after.Metadata.Keys, StringComparer.Ordinal))
            if (!StringComparer.Ordinal.Equals(before.Metadata.GetValueOrDefault(key), after.Metadata.GetValueOrDefault(key))) changed.Add($"metadata.{key}");
        return changed;
    }

    private static IEnumerable<string> CautionIndicators(PersistenceEntry entry)
    {
        if (entry.FileEvidence is { Exists: false }) yield return "file-missing";
        if (entry.FileEvidence?.Signature.Status == SignatureStatus.Unsigned) yield return "unsigned-file";
        var path = entry.Command.ExecutablePath ?? string.Empty;
        if (path.Contains("\\Users\\", StringComparison.OrdinalIgnoreCase)) yield return "user-path";
        if (path.Contains("\\Temp\\", StringComparison.OrdinalIgnoreCase)) yield return "temporary-path";
        if (entry.Command.Confidence is EvidenceConfidence.None or EvidenceConfidence.Low) yield return "ambiguous-command";
    }

    private static string Display(ChangeType type) => type switch { ChangeType.Added => "Ajoutée", ChangeType.Removed => "Supprimée", ChangeType.Modified => "Modifiée", _ => type.ToString() };
}
