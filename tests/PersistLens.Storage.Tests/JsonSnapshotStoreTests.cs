using PersistLens.Domain;
using PersistLens.Storage;

namespace PersistLens.Storage.Tests;

public sealed class JsonSnapshotStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PersistLensTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Save_load_list_and_delete_round_trip()
    {
        var store = new JsonSnapshotStore(directory); var snapshot = new PersistenceSnapshot(new(1, "test", DateTimeOffset.UtcNow, new("machine", null, "x64", "user", false), [], []), []);
        await store.SaveAsync("clean", snapshot, CancellationToken.None);
        Assert.Equal("test", (await store.LoadAsync("clean", CancellationToken.None)).Metadata.PersistLensVersion);
        Assert.Equal(["clean"], await store.ListAsync(CancellationToken.None));
        await store.DeleteAsync("clean", CancellationToken.None);
        Assert.Empty(await store.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Save_rejects_path_traversal() => await Assert.ThrowsAsync<ArgumentException>(() => new JsonSnapshotStore(directory).SaveAsync("..", new(new(1, "test", DateTimeOffset.UtcNow, new("m", null, "x64", "u", false), [], []), []), CancellationToken.None));

    [Fact]
    public async Task Load_rejects_malformed_json()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "broken.json"), "{ invalid json", CancellationToken.None);
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => new JsonSnapshotStore(directory).LoadAsync("broken", CancellationToken.None));
    }

    [Fact]
    public async Task Load_reads_legacy_signature_status_name()
    {
        var store = new JsonSnapshotStore(directory);
        var evidence = new FileEvidence("C:\\fixture.exe", "C:\\fixture.exe", "C:\\fixture.exe", true, ".exe", 1, null, null, null, null, new(SignatureStatus.SignedAndTrusted, "subject", "issuer", null, null, null, "fixture"), EvidenceConfidence.High, null);
        var entry = PersistenceEntry.Create(PersistenceType.RegistryRun, "fixture", new("HKCU\\Run"), "fixture", "C:\\fixture.exe", new("C:\\fixture.exe", "C:\\fixture.exe", null, null, [], EvidenceConfidence.High, null), "fixture", null, evidence, DateTimeOffset.UnixEpoch);
        var snapshot = new PersistenceSnapshot(new(1, "0.1.0", DateTimeOffset.UnixEpoch, new("machine", null, "x64", "user", false), [], []), [entry]);
        await store.SaveAsync("current", snapshot, CancellationToken.None);
        var json = await File.ReadAllTextAsync(Path.Combine(directory, "current.json"), CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(directory, "legacy.json"), json.Replace("signedAndTrusted", "trusted", StringComparison.Ordinal), CancellationToken.None);
        var loaded = await store.LoadAsync("legacy", CancellationToken.None);
        Assert.Equal(SignatureStatus.SignedAndTrusted, loaded.Entries.Single().FileEvidence!.Signature.Status);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
