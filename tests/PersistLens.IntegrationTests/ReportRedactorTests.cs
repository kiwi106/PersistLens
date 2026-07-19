using System.Text.Json;
using PersistLens.Domain;
using PersistLens.Reporting;

namespace PersistLens.IntegrationTests;

public sealed class ReportRedactorTests
{
    private const string User = "SensitiveUserName";
    private const string Token = "SUPER_SECRET_TOKEN_123";
    private const string Password = "PRIVATE_PASSWORD_456";

    [Fact]
    public void Redact_masks_personal_paths_accounts_and_machine_with_deterministic_aliases()
    {
        var first = new ReportRedactor().Redact(new InventoryResult([Entry(@"C:\Users\bob\tool.exe"), Entry(@"C:\Users\Alice\tool.exe")], [], DateTimeOffset.UnixEpoch));
        var second = new ReportRedactor().Redact(new InventoryResult([Entry(@"C:\Users\Alice\tool.exe"), Entry(@"C:\Users\bob\tool.exe")], [], DateTimeOffset.UnixEpoch));

        Assert.Contains(@"C:\Users\<UTILISATEUR_1>\tool.exe", first.Entries[1].Command.Raw, StringComparison.Ordinal);
        Assert.Contains(@"C:\Users\<UTILISATEUR_2>\tool.exe", first.Entries[0].Command.Raw, StringComparison.Ordinal);
        Assert.Equal(first.Entries.Select(entry => entry.Command.Raw).OrderBy(value => value, StringComparer.Ordinal), second.Entries.Select(entry => entry.Command.Raw).OrderBy(value => value, StringComparer.Ordinal));
        var account = new ReportRedactor().Redact(new InventoryResult([Entry(@"KIWI-PC\SensitiveUserName --token SUPER_SECRET_TOKEN_123")], [], DateTimeOffset.UnixEpoch)).Entries.Single().Command.Raw;
        Assert.Contains("<COMPTE_WINDOWS_1>", account, StringComparison.Ordinal);
        Assert.DoesNotContain("KIWI-PC", account, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(User, account, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_masks_an_account_found_in_a_report_string_even_without_a_run_as_field()
    {
        var entry = Entry(@"EXAMPLE-DOMAIN\ExternalUser --port 443") with { RunAs = null, FileEvidence = null };
        var command = new ReportRedactor().Redact(new InventoryResult([entry], [], DateTimeOffset.UnixEpoch)).Entries.Single().Command.Raw;

        Assert.Contains("<COMPTE_WINDOWS_1>", command, StringComparison.Ordinal);
        Assert.DoesNotContain("EXAMPLE-DOMAIN", command, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalUser", command, StringComparison.Ordinal);
        Assert.Contains("--port 443", command, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--password PRIVATE_PASSWORD_456")]
    [InlineData("--password=PRIVATE_PASSWORD_456")]
    [InlineData("--token SUPER_SECRET_TOKEN_123")]
    [InlineData("--api-key=SUPER_SECRET_TOKEN_123")]
    [InlineData("/password:PRIVATE_PASSWORD_456")]
    [InlineData("API_KEY=SUPER_SECRET_TOKEN_123")]
    [InlineData("Authorization: Bearer SUPER_SECRET_TOKEN_123")]
    public void Redact_masks_explicit_command_secrets(string command)
    {
        var redacted = new ReportRedactor().Redact(new InventoryResult([Entry(command)], [], DateTimeOffset.UnixEpoch)).Entries.Single().Command.Raw;
        Assert.Contains("<MASQUÉ>", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_masks_connection_strings_uri_userinfo_and_preserves_non_sensitive_values()
    {
        var command = "Server=db;User ID=admin;Password=PRIVATE_PASSWORD_456; https://user:SUPER_SECRET_TOKEN_123@example.org:8443/path --port 443";
        var result = new ReportRedactor().Redact(new InventoryResult([Entry(command)], [], DateTimeOffset.UnixEpoch)).Entries.Single();

        Assert.Contains("Server=db", result.Command.Raw, StringComparison.Ordinal);
        Assert.Contains("User ID=<UTILISATEUR>", result.Command.Raw, StringComparison.Ordinal);
        Assert.Contains("Password=<MASQUÉ>", result.Command.Raw, StringComparison.Ordinal);
        Assert.Contains("https://<UTILISATEUR>:<MASQUÉ>@example.org:8443/path", result.Command.Raw, StringComparison.Ordinal);
        Assert.Contains("--port 443", result.Command.Raw, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_is_idempotent_and_keeps_the_original_domain_object_unchanged()
    {
        var original = new InventoryResult([Entry(@"C:\Users\SensitiveUserName\agent.exe --secret SUPER_SECRET_TOKEN_123")], [], DateTimeOffset.UnixEpoch);
        var redactor = new ReportRedactor();
        var once = redactor.Redact(original); var twice = redactor.Redact(once);

        Assert.Equal(once.Entries.Select(entry => entry.Command.Raw), twice.Entries.Select(entry => entry.Command.Raw));
        Assert.Contains(User, original.Entries.Single().Command.Raw, StringComparison.Ordinal);
        Assert.Contains(Token, original.Entries.Single().Command.Raw, StringComparison.Ordinal);
        Assert.Equal(original.Entries.Single().StableId, once.Entries.Single().StableId);
        Assert.Equal(original.Entries.Single().FileEvidence!.Sha256, once.Entries.Single().FileEvidence!.Sha256);
    }

    [Fact]
    public async Task Redacted_json_and_terminal_contain_no_sensitive_sentinel_and_expose_metadata()
    {
        var report = new InventoryResult([Entry(@"C:\Users\SensitiveUserName\agent.exe --password PRIVATE_PASSWORD_456 --token SUPER_SECRET_TOKEN_123")], [new("fixture", @"C:\Users\SensitiveUserName", "Authorization: Bearer SUPER_SECRET_TOKEN_123")], DateTimeOffset.UnixEpoch);
        using var json = new StringWriter(); using var terminal = new StringWriter();
        await new JsonReporter(new ReportRedactor()).WriteInventoryAsync(report, json, CancellationToken.None);
        await new TerminalReporter(new ReportRedactor()).WriteInventoryAsync(report, terminal, CancellationToken.None);

        using var document = JsonDocument.Parse(json.ToString());
        Assert.True(document.RootElement.GetProperty("redaction").GetProperty("applied").GetBoolean());
        Assert.Equal(1, document.RootElement.GetProperty("redaction").GetProperty("version").GetInt32());
        Assert.DoesNotContain("Rapport masqué", json.ToString(), StringComparison.Ordinal);
        Assert.Contains("Rapport masqué", terminal.ToString(), StringComparison.Ordinal);
        foreach (var sentinel in new[] { User, Token, Password })
        {
            Assert.DoesNotContain(sentinel, json.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, terminal.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Redacted_snapshot_and_diff_do_not_change_original_snapshot_or_leak_before_after_or_errors()
    {
        var before = Snapshot("before", @"C:\Users\SensitiveUserName\one.exe --token SUPER_SECRET_TOKEN_123");
        var after = Snapshot("after", @"C:\Users\SensitiveUserName\two.exe --password PRIVATE_PASSWORD_456");
        var change = new PersistenceChange(ChangeType.Modified, before.Entries[0].StableId, before.Entries[0], after.Entries[0], ["command"], "Modifiée SensitiveUserName", []);
        var diff = new DiffResult(before, after, [change]);
        using var output = new StringWriter();
        await new JsonReporter(new ReportRedactor()).WriteDiffAsync(diff, output, CancellationToken.None);

        Assert.Contains(Token, before.Entries[0].Command.Raw, StringComparison.Ordinal);
        Assert.Contains(Password, after.Entries[0].Command.Raw, StringComparison.Ordinal);
        foreach (var sentinel in new[] { User, Token, Password }) Assert.DoesNotContain(sentinel, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_handles_empty_malformed_and_very_long_strings_without_throwing()
    {
        var empty = new ReportRedactor().Redact(new InventoryResult([Entry(string.Empty), Entry("--password=\"unterminated"), Entry(new string('x', 262_145) + Token)], [], DateTimeOffset.UnixEpoch));

        Assert.Equal(string.Empty, empty.Entries[0].Command.Raw);
        Assert.Contains("<MASQUÉ>", empty.Entries[1].Command.Raw, StringComparison.Ordinal);
        Assert.Equal("<TEXTE_TROP_LONG_MASQUÉ>", empty.Entries[2].Command.Raw);
    }

    private static PersistenceSnapshot Snapshot(string name, string command) => new(new(1, "0.1.0", DateTimeOffset.UnixEpoch, new("KIWI-PC", null, "x64", User, false), [], [new("fixture", @"C:\Users\SensitiveUserName", Token)]), [Entry(command, name)]);

    private static PersistenceEntry Entry(string command, string name = "entry")
    {
        var evidence = new FileEvidence(@"C:\Users\SensitiveUserName\agent.exe", @"C:\Users\SensitiveUserName\agent.exe", @"C:\Users\SensitiveUserName\agent.exe", true, ".exe", 1, null, null, "ab".PadRight(64, 'c'), @"KIWI-PC\SensitiveUserName", new(SignatureStatus.Unsigned, "CN=SensitiveUserName", null, null, null, null, "SUPER_SECRET_TOKEN_123"), EvidenceConfidence.High, null);
        return PersistenceEntry.Create(PersistenceType.Service, "fixture", new(@"HKCU\Software\SensitiveUserName"), name, command, new(command, @"C:\Users\SensitiveUserName\agent.exe", command, null, [command], EvidenceConfidence.High, null), @"KIWI-PC\SensitiveUserName", new Dictionary<string, string> { ["environment"] = "TOKEN=SUPER_SECRET_TOKEN_123", ["note"] = "PRIVATE_PASSWORD_456" }, evidence, DateTimeOffset.UnixEpoch);
    }
}
