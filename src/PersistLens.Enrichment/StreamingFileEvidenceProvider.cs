using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PersistLens.Domain;

namespace PersistLens.Enrichment;

public sealed class StreamingFileEvidenceProvider(int maximumMegabytes = 512) : IFileEvidenceProvider
{
    private readonly long maximumBytes = maximumMegabytes * 1024L * 1024L;

    public async Task<FileEvidence?> CollectAsync(string rawPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return null;
        var expanded = Environment.ExpandEnvironmentVariables(rawPath);
        string normalized;
        try { normalized = Path.GetFullPath(expanded); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Unavailable(rawPath, expanded, null, SignatureStatus.Error, "The path could not be normalized.");
        }
        try
        {
            var info = new FileInfo(normalized);
            if (!info.Exists) return Unavailable(rawPath, expanded, normalized, SignatureStatus.FileMissing, "The target does not exist.");
            if (info.Length > maximumBytes) return new(rawPath, expanded, normalized, true, info.Extension, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, null, null,
                new(SignatureStatus.Unsupported, null, null, null, null, null, "Hash skipped because file exceeds configured limit."), EvidenceConfidence.Medium, "Hash skipped because file exceeds configured limit.");
            string hash;
            await using (var stream = new FileStream(normalized, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 131072, FileOptions.SequentialScan | FileOptions.Asynchronous))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
            return new(rawPath, expanded, normalized, true, info.Extension, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, hash, null, GetSignature(normalized), EvidenceConfidence.High, "Owner collection is not enabled in this MVP.");
        }
        catch (UnauthorizedAccessException) { return Unavailable(rawPath, expanded, normalized, SignatureStatus.AccessDenied, "Access was denied."); }
        catch (IOException exception) { return Unavailable(rawPath, expanded, normalized, SignatureStatus.Error, exception.Message); }
        catch (CryptographicException exception) { return Unavailable(rawPath, expanded, normalized, SignatureStatus.Error, exception.Message); }
    }

    private static SignatureEvidence GetSignature(string path)
    {
        try
        {
            using var certificate = X509Certificate.CreateFromSignedFile(path);
            return new(SignatureStatus.SignedUntrusted, certificate.Subject, certificate.Issuer, null, null, null,
                "A signing certificate was found; Windows chain trust is not evaluated in this MVP.");
        }
        catch (CryptographicException) { return new(SignatureStatus.Unsigned, null, null, null, null, null, "No readable embedded signing certificate."); }
    }

    private static FileEvidence Unavailable(string raw, string expanded, string? normalized, SignatureStatus status, string limitation) =>
        new(raw, expanded, normalized, false, null, null, null, null, null, null, new(status, null, null, null, null, null, limitation), EvidenceConfidence.Low, limitation);
}
