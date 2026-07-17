using System.Xml;
using System.Xml.Linq;

namespace PersistLens.Collectors;

internal static class ScheduledTaskXmlParser
{
    public static ScheduledTaskSourceTask Parse(string path, bool enabled, string xml)
    {
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 });
        var document = LoadTaskDocument(reader);
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        var principal = document.Descendants(ns + "UserId").FirstOrDefault()?.Value;
        var runLevel = document.Descendants(ns + "RunLevel").FirstOrDefault()?.Value;
        var triggers = document.Descendants(ns + "Triggers").Descendants().Select(element => element.Name.LocalName).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var actions = document.Descendants(ns + "Exec").Select(action => new ScheduledTaskExecAction(
            action.Element(ns + "Command")?.Value ?? string.Empty,
            action.Element(ns + "Arguments")?.Value ?? string.Empty,
            action.Element(ns + "WorkingDirectory")?.Value)).ToArray();
        return new(path, enabled, principal, runLevel, actions, triggers);
    }

    internal static XDocument LoadTaskDocument(XmlReader reader) => XDocument.Load(reader, LoadOptions.None);
}
