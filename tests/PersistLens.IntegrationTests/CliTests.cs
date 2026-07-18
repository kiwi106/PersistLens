using System.Text.Json;
using PersistLens.Domain;
using PersistLens.Reporting;

namespace PersistLens.IntegrationTests;

public sealed class CliTests
{
    [Fact]
    public async Task Help_is_successful_and_scriptable()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        var exitCode = await PersistLensProgram.RunAsync(["--help"], output, error, CancellationToken.None);
        Assert.Equal(0, exitCode);
        Assert.Contains("inventaire local", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("persistlens inventory", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_command_returns_input_exit_code()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        var exitCode = await PersistLensProgram.RunAsync(["unknown"], output, error, CancellationToken.None);
        Assert.Equal(2, exitCode);
        Assert.Contains("Commande inconnue", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_reporter_writes_only_valid_json_with_stable_schema()
    {
        using var output = new StringWriter();
        await new JsonReporter().WriteInventoryAsync(new InventoryResult([], [], DateTimeOffset.UnixEpoch), output, CancellationToken.None);
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("inventory", document.RootElement.GetProperty("kind").GetString());
        Assert.DoesNotContain("Inventaire PersistLens", output.ToString(), StringComparison.Ordinal);
    }
}
