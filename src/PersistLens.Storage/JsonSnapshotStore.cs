using System.Text.Json;
using System.Text.Json.Serialization;
using PersistLens.Domain;

namespace PersistLens.Storage;

public sealed class JsonSnapshotStore : ISnapshotStore
{
    private readonly string directory;
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public JsonSnapshotStore(string? directory = null)
    {
        this.directory = Path.GetFullPath(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersistLens", "snapshots"));
        Directory.CreateDirectory(this.directory);
    }

    public async Task SaveAsync(string name, PersistenceSnapshot snapshot, CancellationToken cancellationToken)
    {
        var path = PathFor(name); var temporary = Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<PersistenceSnapshot> LoadAsync(string name, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(PathFor(name), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan | FileOptions.Asynchronous);
        return await JsonSerializer.DeserializeAsync<PersistenceSnapshot>(stream, options, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Le JSON du snapshot est vide ou invalide.");
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> names = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Select(Path.GetFileNameWithoutExtension).Where(name => name is not null).Cast<string>().OrderBy(name => name, StringComparer.Ordinal).ToArray();
        return Task.FromResult(names);
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); File.Delete(PathFor(name)); return Task.CompletedTask;
    }

    private string PathFor(string name) => Path.Combine(directory, SnapshotNameValidator.Validate(name) + ".json");
}
