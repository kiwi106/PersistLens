namespace PersistLens.IntegrationTests;

public sealed class CliTests
{
    [Fact]
    public async Task Help_is_successful_and_scriptable()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        var exitCode = await PersistLensProgram.RunAsync(["--help"], output, error, CancellationToken.None);
        Assert.Equal(0, exitCode); Assert.Contains("persistlens inventory", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_command_returns_input_exit_code()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        var exitCode = await PersistLensProgram.RunAsync(["unknown"], output, error, CancellationToken.None);
        Assert.Equal(2, exitCode);
    }
}
