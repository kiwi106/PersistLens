using System.Text.Json;
using System.Text.Json.Serialization;
using PersistLens.Domain;

namespace PersistLens.Reporting;

public sealed class JsonReporter : IReporter
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    public Task WriteInventoryAsync(InventoryResult result, TextWriter writer, CancellationToken cancellationToken) => WriteAsync(new { schemaVersion = 1, kind = "inventory", result }, writer, cancellationToken);
    public Task WriteSnapshotAsync(string name, PersistenceSnapshot snapshot, TextWriter writer, CancellationToken cancellationToken) => WriteAsync(new { schemaVersion = 1, kind = "snapshot", name, snapshot }, writer, cancellationToken);
    public Task WriteDiffAsync(DiffResult result, TextWriter writer, CancellationToken cancellationToken) => WriteAsync(new { schemaVersion = 1, kind = "diff", result }, writer, cancellationToken);
    private static async Task WriteAsync<T>(T value, TextWriter writer, CancellationToken cancellationToken) { await writer.WriteLineAsync(JsonSerializer.Serialize(value, Options).AsMemory(), cancellationToken).ConfigureAwait(false); }
}
