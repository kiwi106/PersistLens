using System.Text.Json;
using System.Text.Json.Serialization;
using PersistLens.Domain;

namespace PersistLens.Reporting;

public sealed class JsonReporter(ReportRedactor? redactor = null) : IReporter
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    public Task WriteInventoryAsync(InventoryResult result, TextWriter writer, CancellationToken cancellationToken) => WriteAsync(new { schemaVersion = 1, kind = "inventory", redaction = redactor is null ? null : RedactionMetadata.Current, result = redactor?.Redact(result) ?? result }, writer, cancellationToken);
    public Task WriteSnapshotAsync(string name, PersistenceSnapshot snapshot, TextWriter writer, CancellationToken cancellationToken) => WriteAsync(new { schemaVersion = 1, kind = "snapshot", redaction = redactor is null ? null : RedactionMetadata.Current, name, snapshot = redactor?.Redact(snapshot) ?? snapshot }, writer, cancellationToken);
    public Task WriteDiffAsync(DiffResult result, TextWriter writer, CancellationToken cancellationToken) => WriteAsync(new { schemaVersion = 1, kind = "diff", redaction = redactor is null ? null : RedactionMetadata.Current, result = redactor?.Redact(result) ?? result }, writer, cancellationToken);
    private static async Task WriteAsync<T>(T value, TextWriter writer, CancellationToken cancellationToken) { await writer.WriteLineAsync(JsonSerializer.Serialize(value, Options).AsMemory(), cancellationToken).ConfigureAwait(false); }
}
