using System.Xml;
using PersistLens.Collectors;
using PersistLens.Domain;

namespace PersistLens.Collectors.Tests;

public sealed class WindowsCommandParserTests
{
    private readonly WindowsCommandParser parser = new();

    [Fact]
    public void Parse_preserves_quoted_path_and_arguments()
    {
        var result = parser.Parse("\"C:\\Program Files\\Acme\\agent.exe\" --quiet");
        Assert.Equal("C:\\Program Files\\Acme\\agent.exe", result.ExecutablePath);
        Assert.Equal("--quiet", result.Arguments);
        Assert.Equal(EvidenceConfidence.High, result.Confidence);
    }

    [Fact]
    public void Parse_marks_indirect_interpreter_command_as_uncertain()
    {
        var result = parser.Parse("powershell.exe -File script.ps1");
        Assert.Equal("powershell.exe", result.ExecutablePath);
        Assert.NotNull(result.UncertaintyReason);
    }

    [Fact]
    public void Parse_does_not_invent_path_for_ambiguous_command()
    {
        var result = parser.Parse("C:\\Program Files\\Acme agent --quiet");
        Assert.Null(result.ExecutablePath);
        Assert.Equal(EvidenceConfidence.Low, result.Confidence);
    }

    [Fact]
    public void Task_xml_reader_rejects_external_dtds()
    {
        using var input = new StringReader("<!DOCTYPE Task [<!ENTITY xxe SYSTEM 'file:///not-read'>]><Task>&xxe;</Task>");
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        Assert.Throws<XmlException>(() => ScheduledTaskXmlParser.LoadTaskDocument(reader));
    }
}
