using System.Text;
using System.Text.RegularExpressions;
using PersistLens.Domain;

namespace PersistLens.Reporting;

public sealed record RedactionMetadata(bool Applied, int Version, IReadOnlyList<string> Categories)
{
    public static readonly RedactionMetadata Current = new(true, 1, ["userIdentity", "machineName", "personalPath", "commandSecret"]);
}

/// <summary>Creates a presentation-only copy of reports. It never mutates domain objects or snapshots.</summary>
public sealed partial class ReportRedactor
{
    private const int MaximumInputLength = 262_144;
    private const string Mask = "<MASQUÉ>";

    public InventoryResult Redact(InventoryResult report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var context = CreateContext(report.Entries, null, null);
        return new(report.Entries.Select(entry => Redact(entry, context)).ToArray(), report.Errors.Select(error => Redact(error, context)).ToArray(), report.CollectedAtUtc);
    }

    public PersistenceSnapshot Redact(PersistenceSnapshot report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var context = CreateContext(report.Entries, report.Metadata.OperatingContext, null);
        return Redact(report, context);
    }

    public DiffResult Redact(DiffResult report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var entries = report.Before.Entries.Concat(report.After.Entries).Concat(report.Changes.SelectMany(change => new[] { change.Before, change.After }.OfType<PersistenceEntry>()));
        var context = CreateContext(entries, report.Before.Metadata.OperatingContext, report.After.Metadata.OperatingContext);
        return new(Redact(report.Before, context), Redact(report.After, context), report.Changes.Select(change => Redact(change, context)).ToArray());
    }

    private static RedactionContext CreateContext(IEnumerable<PersistenceEntry> source, OperatingContext? firstContext, OperatingContext? secondContext)
    {
        var entries = source.ToArray();
        var users = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var accounts = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var machines = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var secrets = new SortedSet<string>(StringComparer.Ordinal);
        AddContext(firstContext); AddContext(secondContext);
        foreach (var entry in entries)
        {
            AddIdentity(entry.RunAs);
            foreach (var text in EntryStrings(entry)) AddCandidates(text);
        }
        return new(users, accounts, machines, secrets);

        void AddContext(OperatingContext? operatingContext)
        {
            if (operatingContext is null) return;
            AddUser(operatingContext.User);
            if (!string.IsNullOrWhiteSpace(operatingContext.Machine)) machines.Add(operatingContext.Machine);
        }

        void AddIdentity(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || IsWellKnownAccount(value) || IsAlias(value)) return;
            if (value.Contains('\\')) accounts.Add(value); else AddUser(value);
        }

        void AddCandidates(string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumInputLength) return;
            foreach (var secret in ExtractSecretValues(value)) secrets.Add(secret);
            foreach (Match match in UserPathRegex().Matches(value)) AddUser(match.Groups["user"].Value);
            AddAccountCandidates(value);
        }

        void AddAccountCandidates(string value)
        {
            if (IsRegistryPath(value) || value.Contains("\\Users\\", StringComparison.OrdinalIgnoreCase)) return;
            for (var slash = 0; slash < value.Length; slash++)
            {
                if (value[slash] != '\\') continue;
                var start = slash;
                while (start > 0 && IsAccountCharacter(value[start - 1])) start--;
                var end = slash + 1;
                while (end < value.Length && IsAccountCharacter(value[end])) end++;
                var domainLength = slash - start;
                if (domainLength < 2 || end == slash + 1) continue;
                var domain = value[start..slash];
                if (domain.Equals("Users", StringComparison.OrdinalIgnoreCase)) continue;
                accounts.Add(value[start..end]);
                machines.Add(domain);
            }
        }

        void AddUser(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !IsWellKnownAccount(value) && !IsAlias(value)) users.Add(value);
        }
    }

    private static IEnumerable<string?> EntryStrings(PersistenceEntry entry)
    {
        yield return entry.Location.Value; yield return entry.Name; yield return entry.RawValue; yield return entry.Command.Raw;
        yield return entry.Command.ExecutablePath; yield return entry.Command.Arguments; yield return entry.Command.WorkingDirectory; yield return entry.Command.UncertaintyReason;
        foreach (var candidate in entry.Command.Candidates) yield return candidate;
        foreach (var pair in entry.Metadata) { yield return pair.Key; yield return pair.Value; }
        if (entry.Shortcut is not null)
        {
            yield return entry.Shortcut.ShortcutPath; yield return entry.Shortcut.RawTargetPath; yield return entry.Shortcut.ExpandedTargetPath; yield return entry.Shortcut.NormalizedTargetPath;
            yield return entry.Shortcut.Arguments; yield return entry.Shortcut.WorkingDirectory; yield return entry.Shortcut.Description; yield return entry.Shortcut.IconLocation; yield return entry.Shortcut.UserMessage;
            if (entry.Shortcut.PartialErrors is not null) foreach (var error in entry.Shortcut.PartialErrors) yield return error;
        }
        if (entry.FileEvidence is null) yield break;
        yield return entry.FileEvidence.RawPath; yield return entry.FileEvidence.ExpandedPath; yield return entry.FileEvidence.NormalizedPath; yield return entry.FileEvidence.Owner; yield return entry.FileEvidence.Limitation;
        yield return entry.FileEvidence.Signature.Subject; yield return entry.FileEvidence.Signature.Issuer; yield return entry.FileEvidence.Signature.TrustMessage; yield return entry.FileEvidence.Signature.TimestampSubject; yield return entry.FileEvidence.Signature.TrustSource;
        if (entry.FileEvidence.Signature.PartialErrors is not null) foreach (var error in entry.FileEvidence.Signature.PartialErrors) yield return error;
    }

    private PersistenceSnapshot Redact(PersistenceSnapshot snapshot, RedactionContext context) =>
        new(snapshot.Metadata with
        {
            OperatingContext = Redact(snapshot.Metadata.OperatingContext, context),
            Errors = snapshot.Metadata.Errors.Select(error => Redact(error, context)).ToArray()
        }, snapshot.Entries.Select(entry => Redact(entry, context)).ToArray());

    private PersistenceChange Redact(PersistenceChange change, RedactionContext context) => change with
    {
        Before = change.Before is null ? null : Redact(change.Before, context),
        After = change.After is null ? null : Redact(change.After, context),
        Summary = RedactString(change.Summary, context),
        CautionIndicators = change.CautionIndicators.Select(value => RedactString(value, context)).ToArray()
    };

    private PersistenceEntry Redact(PersistenceEntry entry, RedactionContext context) => entry with
    {
        Location = entry.Location with { Value = entry.Type == PersistenceType.RegistryRun ? entry.Location.Value : RedactString(entry.Location.Value, context), RegistryView = RedactOptionalString(entry.Location.RegistryView, context) },
        Name = RedactString(entry.Name, context),
        RawValue = RedactString(entry.RawValue, context),
        Command = entry.Command with
        {
            Raw = RedactString(entry.Command.Raw, context),
            ExecutablePath = RedactOptionalString(entry.Command.ExecutablePath, context),
            Arguments = RedactOptionalString(entry.Command.Arguments, context),
            WorkingDirectory = RedactOptionalString(entry.Command.WorkingDirectory, context),
            Candidates = entry.Command.Candidates.Select(value => RedactString(value, context)).ToArray(),
            UncertaintyReason = RedactOptionalString(entry.Command.UncertaintyReason, context)
        },
        RunAs = RedactIdentity(entry.RunAs, context),
        Metadata = RedactMetadata(entry.Metadata, context),
        FileEvidence = entry.FileEvidence is null ? null : Redact(entry.FileEvidence, context),
        Shortcut = entry.Shortcut is null ? null : Redact(entry.Shortcut, context)
    };

    private ShortcutTargetEvidence Redact(ShortcutTargetEvidence shortcut, RedactionContext context) => shortcut with
    {
        ShortcutPath = RedactString(shortcut.ShortcutPath, context),
        RawTargetPath = RedactOptionalString(shortcut.RawTargetPath, context),
        ExpandedTargetPath = RedactOptionalString(shortcut.ExpandedTargetPath, context),
        NormalizedTargetPath = RedactOptionalString(shortcut.NormalizedTargetPath, context),
        Arguments = RedactOptionalString(shortcut.Arguments, context),
        WorkingDirectory = RedactOptionalString(shortcut.WorkingDirectory, context),
        Description = RedactOptionalString(shortcut.Description, context),
        IconLocation = RedactOptionalString(shortcut.IconLocation, context),
        UserMessage = RedactOptionalString(shortcut.UserMessage, context),
        PartialErrors = shortcut.PartialErrors?.Select(value => RedactString(value, context)).ToArray()
    };

    private FileEvidence Redact(FileEvidence evidence, RedactionContext context) => evidence with
    {
        RawPath = RedactString(evidence.RawPath, context),
        ExpandedPath = RedactOptionalString(evidence.ExpandedPath, context),
        NormalizedPath = RedactOptionalString(evidence.NormalizedPath, context),
        Owner = RedactIdentity(evidence.Owner, context),
        Limitation = RedactOptionalString(evidence.Limitation, context),
        Signature = evidence.Signature with
        {
            Subject = RedactOptionalString(evidence.Signature.Subject, context),
            Issuer = RedactOptionalString(evidence.Signature.Issuer, context),
            TrustMessage = RedactOptionalString(evidence.Signature.TrustMessage, context),
            TimestampSubject = RedactOptionalString(evidence.Signature.TimestampSubject, context),
            TrustSource = RedactOptionalString(evidence.Signature.TrustSource, context),
            PartialErrors = evidence.Signature.PartialErrors?.Select(value => RedactString(value, context)).ToArray()
        }
    };

    private CollectionError Redact(CollectionError error, RedactionContext context) => error with { Location = RedactString(error.Location, context), Message = RedactString(error.Message, context) };
    private OperatingContext Redact(OperatingContext context, RedactionContext aliases) => context with { Machine = aliases.Machine(context.Machine), User = aliases.User(context.User) };
    private static string? RedactIdentity(string? value, RedactionContext context) => value is null ? null : context.Account(value) ?? context.User(value) ?? RedactString(value, context);

    private static string RedactString(string value, RedactionContext context)
    {
        if (value.Length > MaximumInputLength) return "<TEXTE_TROP_LONG_MASQUÉ>";
        var redacted = MaskUriUserInfo(value);
        redacted = MaskSensitiveAssignments(redacted);
        redacted = MaskConnectionUsers(redacted);
        foreach (var secret in context.Secrets) redacted = redacted.Replace(secret, Mask, StringComparison.Ordinal);
        foreach (var account in context.Accounts) redacted = ReplaceIgnoreCase(redacted, account.Key, account.Value);
        foreach (var user in context.Users) redacted = ReplaceIgnoreCase(redacted, user.Key, user.Value);
        foreach (var machine in context.Machines) redacted = ReplaceIgnoreCase(redacted, machine.Key, machine.Value);
        return redacted;
    }

    private static string? RedactOptionalString(string? value, RedactionContext context) => value is null ? null : RedactString(value, context);

    private static IReadOnlyDictionary<string, string> RedactMetadata(IReadOnlyDictionary<string, string> metadata, RedactionContext context)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var key = RedactString(pair.Key, context);
            for (var suffix = 2; result.ContainsKey(key); suffix++) key = $"{RedactString(pair.Key, context)}_{suffix}";
            result[key] = RedactString(pair.Value, context);
        }
        return result;
    }

    private static string MaskUriUserInfo(string value) => UriUserInfoRegex().Replace(value, match => $"{match.Groups["prefix"].Value}<UTILISATEUR>:{Mask}@");
    private static string MaskConnectionUsers(string value) => ConnectionUserRegex().Replace(value, match => $"{match.Groups["prefix"].Value}<UTILISATEUR>");
    private static string MaskSensitiveAssignments(string value)
    {
        value = OptionEqualsRegex().Replace(value, match => $"{match.Groups["prefix"].Value}{Mask}");
        value = OptionSpaceRegex().Replace(value, match => $"{match.Groups["prefix"].Value}{match.Groups["separator"].Value}{Mask}");
        value = SlashOptionRegex().Replace(value, match => $"{match.Groups["prefix"].Value}{Mask}");
        value = NamedAssignmentRegex().Replace(value, match => $"{match.Groups["prefix"].Value}{Mask}");
        return AuthorizationRegex().Replace(value, match => $"{match.Groups["prefix"].Value}{Mask}");
    }

    private static IEnumerable<string> ExtractSecretValues(string value)
    {
        foreach (var regex in new[] { UriUserInfoRegex(), OptionEqualsRegex(), OptionSpaceRegex(), SlashOptionRegex(), NamedAssignmentRegex(), AuthorizationRegex() })
            foreach (Match match in regex.Matches(value))
            {
                var secret = match.Groups["value"].Value;
                if (secret.Length == 0 || secret == Mask) continue;
                if (secret.Length > 1 && ((secret[0] == '\"' && secret[^1] == '\"') || (secret[0] == '\'' && secret[^1] == '\''))) secret = secret[1..^1];
                if (secret.Length != 0) yield return secret;
            }
    }

    private static string ReplaceIgnoreCase(string value, string search, string replacement)
    {
        if (string.IsNullOrEmpty(search)) return value;
        var index = value.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return value;
        var builder = new StringBuilder(value.Length + replacement.Length);
        var start = 0;
        while (index >= 0)
        {
            builder.Append(value, start, index - start).Append(replacement);
            start = index + search.Length;
            index = value.IndexOf(search, start, StringComparison.OrdinalIgnoreCase);
        }
        return builder.Append(value, start, value.Length - start).ToString();
    }

    private static bool IsWellKnownAccount(string value) => value.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) || value.Equals("LOCAL SERVICE", StringComparison.OrdinalIgnoreCase) || value.Equals("NETWORK SERVICE", StringComparison.OrdinalIgnoreCase);
    private static bool IsAlias(string value) => value.StartsWith('<') && value.EndsWith('>');
    private static bool IsAccountCharacter(char value) => char.IsAsciiLetterOrDigit(value) || value is '.' or '_' or '-' or '$';
    private static bool IsRegistryPath(string value) => value.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase) || value.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase) || value.StartsWith("HKCR\\", StringComparison.OrdinalIgnoreCase) || value.StartsWith("HKU\\", StringComparison.OrdinalIgnoreCase) || value.StartsWith("HKCC\\", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"(?i)\\Users\\(?<user>[^\\/:;\""\s]+)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)] private static partial Regex UserPathRegex();
    [GeneratedRegex(@"(?<prefix>[A-Za-z][A-Za-z0-9+.-]*://)(?<user>[^\s/@:]+):(?<value>[^\s/@]*)@", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)] private static partial Regex UriUserInfoRegex();
    [GeneratedRegex(@"(?i)(?<prefix>\b(?:user\s*id|uid|username)\s*=\s*)(?:\""[^\"";]*\""|'[^';]*'|[^;\s]*)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)] private static partial Regex ConnectionUserRegex();
    [GeneratedRegex(@"(?i)(?<prefix>--(?:password|token|api-key|secret|client-secret)\s*=\s*)(?<value>\""[^\""\r\n]*\""|'[^'\r\n]*'|\S*)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)] private static partial Regex OptionEqualsRegex();
    [GeneratedRegex(@"(?i)(?<prefix>--(?:password|token|api-key|secret|client-secret))(?<separator>\s+)(?<value>\""[^\""\r\n]*\""|'[^'\r\n]*'|\S+)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)] private static partial Regex OptionSpaceRegex();
    [GeneratedRegex(@"(?i)(?<prefix>/(?:password|token|api-key|secret|client-secret)\s*:\s*)(?<value>\""[^\""\r\n]*\""|'[^'\r\n]*'|\S*)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)] private static partial Regex SlashOptionRegex();
    [GeneratedRegex(@"(?i)(?<prefix>\b(?:password|pwd|token|api[_-]?key|secret|client[_-]?secret|access[_-]?token|private[_-]?key|access[_-]?key|passwd)\s*=\s*)(?<value>\""[^\"";\r\n]*\""|'[^';\r\n]*'|[^;\s]*)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)] private static partial Regex NamedAssignmentRegex();
    [GeneratedRegex(@"(?i)(?<prefix>\bauthorization\s*:\s*bearer\s+)(?<value>\""[^\""\r\n]*\""|'[^'\r\n]*'|\S+)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 250)] private static partial Regex AuthorizationRegex();

    private sealed class RedactionContext(SortedSet<string> users, SortedSet<string> accounts, SortedSet<string> machines, SortedSet<string> secrets)
    {
        public IReadOnlyList<KeyValuePair<string, string>> Users { get; } = Aliases(users, "<UTILISATEUR_");
        public IReadOnlyList<KeyValuePair<string, string>> Accounts { get; } = Aliases(accounts, "<COMPTE_WINDOWS_");
        public IReadOnlyList<KeyValuePair<string, string>> Machines { get; } = Aliases(machines, "<MACHINE_");
        public IReadOnlyList<string> Secrets { get; } = secrets.OrderByDescending(value => value.Length).ThenBy(value => value, StringComparer.Ordinal).ToArray();
        public string User(string value) => IsAlias(value) ? value : Alias(value, Users) ?? "<UTILISATEUR_1>";
        public string? Account(string? value) => Alias(value, Accounts);
        public string Machine(string value) => Alias(value, Machines) ?? "<MACHINE_1>";
        private static IReadOnlyList<KeyValuePair<string, string>> Aliases(IEnumerable<string> values, string prefix) => values.Select((value, index) => new KeyValuePair<string, string>(value, $"{prefix}{index + 1}>")).ToArray();
        private static string? Alias(string? value, IEnumerable<KeyValuePair<string, string>> aliases) => value is not null && aliases.FirstOrDefault(pair => pair.Key.Equals(value, StringComparison.OrdinalIgnoreCase)) is var pair && !string.IsNullOrEmpty(pair.Key) ? pair.Value : null;
        private static bool IsAlias(string value) => value.StartsWith('<') && value.EndsWith('>');
    }
}
