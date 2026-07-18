using PersistLens.Domain;

namespace PersistLens.Collectors;

public sealed class WindowsCommandParser
{
    private static readonly string[] Extensions = [".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".dll"];
    private static readonly HashSet<string> Interpreters = new(StringComparer.OrdinalIgnoreCase) { "cmd.exe", "powershell.exe", "pwsh.exe", "rundll32.exe", "wscript.exe", "cscript.exe", "mshta.exe" };

    public PersistenceCommand Parse(string? raw, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return PersistenceCommand.RawOnly(raw ?? string.Empty, "Commande vide.");
        var expanded = Environment.ExpandEnvironmentVariables(raw.Trim());
        if (expanded[0] == '\"')
        {
            var closing = expanded.IndexOf('\"', 1);
            if (closing > 0) return Build(expanded, expanded[1..closing], expanded[(closing + 1)..].TrimStart(), workingDirectory, EvidenceConfidence.High, null);
            return PersistenceCommand.RawOnly(raw, "Guillemet non fermé.");
        }
        var tokens = expanded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var candidate = string.Empty;
        for (var index = 0; index < tokens.Length; index++)
        {
            candidate = candidate.Length == 0 ? tokens[index] : $"{candidate} {tokens[index]}";
            if (Extensions.Any(extension => candidate.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                return Build(expanded, candidate, string.Join(' ', tokens.Skip(index + 1)), workingDirectory, EvidenceConfidence.Medium, "Le chemin non entouré de guillemets a été déduit d’une extension connue.");
        }
        return new(raw, null, null, workingDirectory, tokens.Take(3).ToArray(), EvidenceConfidence.Low, "Impossible de déterminer de manière sûre la limite de l’exécutable.");
    }

    private static PersistenceCommand Build(string raw, string executable, string arguments, string? workingDirectory, EvidenceConfidence confidence, string? reason)
    {
        var file = Path.GetFileName(executable);
        var indirect = Interpreters.Contains(file);
        return new(raw, executable, arguments, workingDirectory, [executable], indirect ? EvidenceConfidence.Medium : confidence,
            indirect ? "Commande interprétée ; la charge finale peut être indirecte." : reason);
    }
}
