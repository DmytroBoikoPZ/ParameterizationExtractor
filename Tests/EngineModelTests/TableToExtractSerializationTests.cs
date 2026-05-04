using System.IO;
using System.Text.Json;
using System.Xml.Serialization;
using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.Configs.Json;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Tests.EngineModelTests;

[TestFixture]
public class TableToExtractSerializationTests
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
    public void Xml_LegacyFixture_DeserializesWithEmptySchema()
    {
        var xml = @"<?xml version=""1.0"" encoding=""utf-16""?>
                    <TableToExtract TableName=""Patient"" />";

        var sut = DeserializeXml(xml);

        sut.Schema.Should().BeNullOrEmpty();
        sut.TableName.Should().Be("Patient");
    }

    [Test]
    public void Xml_RoundTrip_EmptySchema_DoesNotEmitAttribute()
    {
        var sut = new TableToExtract("Patient", new OnlyOneTableExtractStrategy());

        var xml = SerializeXml(sut);

        xml.Should().NotContain("schema=\"\"",
            "empty Schema must round-trip without emitting a schema attribute (back-compat)");
    }

    [Test]
    public void Xml_RoundTrip_NonEmptySchema_EmitsSchemaAttribute()
    {
        var sut = new TableToExtract("Log", new OnlyOneTableExtractStrategy()) { Schema = "audit" };

        var xml = SerializeXml(sut);

        xml.Should().Contain("schema=\"audit\"");

        var loaded = DeserializeXml(xml);
        loaded.Schema.Should().Be("audit");
        loaded.TableName.Should().Be("Log");
    }

    [Test]
    public void Json_LegacyFixture_DeserializesWithEmptySchema()
    {
        var json = "{\"tableName\":\"Patient\"}";

        var sut = JsonSerializer.Deserialize<TableToExtract>(json, JsonOptions.Default);

        sut.Should().NotBeNull();
        sut!.Schema.Should().BeNullOrEmpty();
        sut.TableName.Should().Be("Patient");
    }

    [Test]
    public void Json_RoundTrip_EmptySchema_DoesNotEmitProperty()
    {
        var sut = new TableToExtract("Patient", new OnlyOneTableExtractStrategy());

        var json = JsonSerializer.Serialize(sut, JsonOptions.Default);

        json.Should().NotContain("\"schema\"",
            "empty Schema must round-trip without emitting a schema property (back-compat)");
    }

    [Test]
    public void Json_RoundTrip_NonEmptySchema_EmitsSchemaProperty()
    {
        var sut = new TableToExtract("Log", new OnlyOneTableExtractStrategy()) { Schema = "audit" };

        var json = JsonSerializer.Serialize(sut, JsonOptions.Default);

        json.Should().Contain("\"schema\":\"audit\"");

        var loaded = JsonSerializer.Deserialize<TableToExtract>(json, JsonOptions.Default);
        loaded.Should().NotBeNull();
        loaded!.Schema.Should().Be("audit");
        loaded.TableName.Should().Be("Log");
    }
}
