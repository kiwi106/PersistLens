using PersistLens.Domain;

namespace PersistLens.Domain.Tests;

public sealed class PersistenceEntryTests
{
    [Fact]
    public void Create_uses_stable_identity_but_changes_state_hash_when_command_changes()
    {
        var first = Create("C:\\Tools\\agent.exe --one");
        var second = Create("C:\\Tools\\agent.exe --two");
        Assert.Equal(first.StableId, second.StableId);
        Assert.NotEqual(first.StateHash, second.StateHash);
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("baseline-2026_07")]
    public void Snapshot_names_accept_safe_values(string value) => Assert.True(SnapshotNameValidator.IsValid(value));

    [Theory]
    [InlineData("../clean")]
    [InlineData("c:\\clean")]
    [InlineData("")]
    public void Snapshot_names_reject_paths(string value) => Assert.False(SnapshotNameValidator.IsValid(value));

    private static PersistenceEntry Create(string raw) => PersistenceEntry.Create(PersistenceType.RegistryRun, "test", new("HKCU\\Run"), "Agent", raw, new(raw, "C:\\Tools\\agent.exe", raw.Split(' ', 2)[1], null, [], EvidenceConfidence.High, null), "user", null, null, DateTimeOffset.UtcNow);
}
