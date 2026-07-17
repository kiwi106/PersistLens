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

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
