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
            return Unavailable(rawPath, expanded, null, SignatureStatus.Error, "Le chemin n’a pas pu être normalisé.");
        }
        try
        {
            var info = new FileInfo(normalized);
            if (!info.Exists) return Unavailable(rawPath, expanded, normalized, SignatureStatus.FileMissing, "La cible n’existe pas.");
            if (info.Length > maximumBytes) return new(rawPath, expanded, normalized, true, info.Extension, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, null, null,
                new(SignatureStatus.Unsupported, null, null, null, null, null, "Hash ignoré car le fichier dépasse la limite configurée."), EvidenceConfidence.Medium, "Hash ignoré car le fichier dépasse la limite configurée.");
            string hash;
            await using (var stream = new FileStream(normalized, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 131072, FileOptions.SequentialScan | FileOptions.Asynchronous))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
            return new(rawPath, expanded, normalized, true, info.Extension, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, hash, null, GetSignature(normalized), EvidenceConfidence.High, "La collecte du propriétaire n’est pas activée dans ce MVP.");
        }
        catch (UnauthorizedAccessException) { return Unavailable(rawPath, expanded, normalized, SignatureStatus.AccessDenied, "Accès refusé."); }
        catch (IOException exception) { return Unavailable(rawPath, expanded, normalized, SignatureStatus.Error, exception.Message); }
        catch (CryptographicException exception) { return Unavailable(rawPath, expanded, normalized, SignatureStatus.Error, exception.Message); }
    }

    private static SignatureEvidence GetSignature(string path)
    {
        try
        {
            using var certificate = X509Certificate.CreateFromSignedFile(path);
            return new(SignatureStatus.SignedUntrusted, certificate.Subject, certificate.Issuer, null, null, null,
                "Un certificat de signature a été trouvé ; la confiance de la chaîne Windows n’est pas évaluée dans ce MVP.");
        }
        catch (CryptographicException) { return new(SignatureStatus.Unsigned, null, null, null, null, null, "Aucun certificat de signature incorporé lisible."); }
    }

    private static FileEvidence Unavailable(string raw, string expanded, string? normalized, SignatureStatus status, string limitation) =>
        new(raw, expanded, normalized, false, null, null, null, null, null, null, new(status, null, null, null, null, null, limitation), EvidenceConfidence.Low, limitation);
}
