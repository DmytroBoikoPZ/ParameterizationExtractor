using System.IO;
using System.Text.Json;
using System.Xml.Serialization;
using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.Configs.Json;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.EngineModelTests;

[TestFixture]
public class TableToExtractExcludedFlagTests
{
    private static string SerializeXml(TableToExtract value)
    {
        var serializer = new XmlSerializer(typeof(TableToExtract));
        using var sw = new StringWriter();
        serializer.Serialize(sw, value);
        return sw.ToString();
    }

    private static TableToExtract DeserializeXml(string xml)
    {
        var serializer = new XmlSerializer(typeof(TableToExtract));
        using var sr = new StringReader(xml);
        return (TableToExtract)serializer.Deserialize(sr)!;
    }

    [Test]
    public void Excluded_DefaultIsFalse()
    {
        var sut = new TableToExtract("Patient", new OnlyOneTableExtractStrategy());

        sut.Excluded.Should().BeFalse();
    }

    [Test]
    public void Json_RoundTrip_DefaultExcluded_OmitsFromOutput()
    {
        var sut = new TableToExtract("Patient", new OnlyOneTableExtractStrategy());

        var json = JsonSerializer.Serialize(sut, JsonOptions.Default);

        json.Should().NotContain("\"excluded\"",
            "default Excluded (false) must round-trip without emitting the property (back-compat)");
    }

    [Test]
    public void Json_RoundTrip_ExcludedTrue_RoundTrips()
    {
        var sut = new TableToExtract("Patient", new OnlyOneTableExtractStrategy()) { Excluded = true };

        var json = JsonSerializer.Serialize(sut, JsonOptions.Default);

        json.Should().Contain("\"excluded\":true");

        var loaded = JsonSerializer.Deserialize<TableToExtract>(json, JsonOptions.Default);
        loaded.Should().NotBeNull();
        loaded!.Excluded.Should().BeTrue();
        loaded.TableName.Should().Be("Patient");
    }

    [Test]
    public void Xml_RoundTrip_ExcludedTrue_PersistsAttribute()
    {
        var sut = new TableToExtract("Patient", new OnlyOneTableExtractStrategy()) { Excluded = true };

        var xml = SerializeXml(sut);

        xml.Should().Contain("excluded=\"true\"");

        var loaded = DeserializeXml(xml);
        loaded.Excluded.Should().BeTrue();
        loaded.TableName.Should().Be("Patient");
    }

    [Test]
    public void Xml_RoundTrip_ExcludedDefault_AttributeOmitted()
    {
        var sut = new TableToExtract("Patient", new OnlyOneTableExtractStrategy());

        var xml = SerializeXml(sut);

        // [DefaultValue(false)] tells XmlSerializer to skip the attribute when value equals default.
        xml.Should().NotContain("excluded=\"false\"",
            "[DefaultValue(false)] must suppress emission for the default case (back-compat)");
    }

    [Test]
    public void LegacyJson_WithoutField_DeserializesAsFalse()
    {
        var json = "{\"tableName\":\"Patient\"}";

        var sut = JsonSerializer.Deserialize<TableToExtract>(json, JsonOptions.Default);

        sut.Should().NotBeNull();
        sut!.Excluded.Should().BeFalse();
        sut.TableName.Should().Be("Patient");
    }

    [Test]
    public void LegacyXml_WithoutAttribute_DeserializesAsFalse()
    {
        var xml = @"<?xml version=""1.0"" encoding=""utf-16""?>
                    <TableToExtract TableName=""Patient"" />";

        var sut = DeserializeXml(xml);

        sut.Excluded.Should().BeFalse();
        sut.TableName.Should().Be("Patient");
    }
}
