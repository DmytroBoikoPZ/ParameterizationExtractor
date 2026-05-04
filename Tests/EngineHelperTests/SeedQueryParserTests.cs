using FluentAssertions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Logic.Helpers;

namespace Tests.EngineHelperTests
{
    [TestFixture]
    public class SeedQueryParserTests
    {
        [Test]
        public void TryExtract_BareFromTable_ReturnsTableWithEmptySchema()
        {
            var result = SeedQueryParser.TryExtract("SELECT * FROM Patient");
            result.Should().Be(new SeedRoot("", "Patient"));
        }

        [Test]
        public void TryExtract_QualifiedSchemaTable_ReturnsBoth()
        {
            var result = SeedQueryParser.TryExtract("SELECT * FROM dbo.Patient");
            result.Should().Be(new SeedRoot("dbo", "Patient"));
        }

        [Test]
        public void TryExtract_BracketedQualified_StripsBrackets()
        {
            var result = SeedQueryParser.TryExtract("SELECT * FROM [dbo].[Patient]");
            result.Should().Be(new SeedRoot("dbo", "Patient"));
        }

        [Test]
        public void TryExtract_BracketedBareTable_StripsBrackets()
        {
            var result = SeedQueryParser.TryExtract("SELECT * FROM [Patient]");
            result.Should().Be(new SeedRoot("", "Patient"));
        }

        [Test]
        public void TryExtract_ExtraWhitespace_HandlesIt()
        {
            var result = SeedQueryParser.TryExtract("   SELECT *   FROM   audit.Logs   WHERE Id = 1");
            result.Should().Be(new SeedRoot("audit", "Logs"));
        }

        [Test]
        public void TryExtract_MultiLine_NewlineBeforeFrom_FindsIt()
        {
            var sql = "SELECT *\nFROM\n    Patient\nWHERE Id = 1";
            var result = SeedQueryParser.TryExtract(sql);
            result.Should().Be(new SeedRoot("", "Patient"));
        }

        [Test]
        public void TryExtract_NoFromKeyword_ReturnsNull()
        {
            SeedQueryParser.TryExtract("SELECT 1").Should().BeNull();
        }

        [Test]
        public void TryExtract_NullOrEmpty_ReturnsNull()
        {
            SeedQueryParser.TryExtract(null).Should().BeNull();
            SeedQueryParser.TryExtract("").Should().BeNull();
            SeedQueryParser.TryExtract("   ").Should().BeNull();
        }

        [Test]
        public void TryExtract_FromInsideLineComment_ReturnsNull()
        {
            var sql = "-- FROM Patient\n SELECT 1";
            SeedQueryParser.TryExtract(sql).Should().BeNull();
        }

        [Test]
        public void TryExtract_FromInsideBlockComment_ReturnsNull()
        {
            var sql = "/* FROM Patient */ SELECT 1";
            SeedQueryParser.TryExtract(sql).Should().BeNull();
        }

        [Test]
        public void TryExtract_Subquery_ReturnsNull()
        {
            var sql = "SELECT * FROM (SELECT 1) x";
            SeedQueryParser.TryExtract(sql).Should().BeNull();
        }

        [Test]
        public void TryExtract_TrailingSemicolon_HandlesIt()
        {
            var result = SeedQueryParser.TryExtract("SELECT * FROM Patient;");
            result.Should().Be(new SeedRoot("", "Patient"));
        }

        [Test]
        public void TryExtract_FromInStringLiteral_FirstRealFromWins()
        {
            var sql = "SELECT * FROM Patient WHERE LastName = 'FROM cheat'";
            var result = SeedQueryParser.TryExtract(sql);
            result.Should().Be(new SeedRoot("", "Patient"));
        }

        [Test]
        public void TryExtract_CteName_ReturnsCteIdentifier_DocumentedLimitation()
        {
            var sql = "WITH cte AS (SELECT 1) SELECT * FROM cte";
            var result = SeedQueryParser.TryExtract(sql);
            result.Should().Be(new SeedRoot("", "cte"));
        }

        [Test]
        public void TryExtract_BracketedIdentifierWithSpace_PreservesSpace()
        {
            var result = SeedQueryParser.TryExtract("SELECT * FROM dbo.[my table]");
            result.Should().Be(new SeedRoot("dbo", "my table"));
        }

        [Test]
        public void TryExtract_LowercaseFromKeyword_PreservesIdentifierCasing()
        {
            var result = SeedQueryParser.TryExtract("select * from PATIENT");
            result.Should().Be(new SeedRoot("", "PATIENT"));
        }

        [Test]
        public void TryExtract_TempTable_PrefixHashIsValidIdentifierChar()
        {
            var result = SeedQueryParser.TryExtract("select * From #Temp");
            result.Should().Be(new SeedRoot("", "#Temp"));
        }

        [Test]
        public void TryExtract_LeadingStringLiteralBeforeFrom_SkipsString()
        {
            var sql = "SELECT 'FROM cheat' FROM Patient";
            var result = SeedQueryParser.TryExtract(sql);
            result.Should().Be(new SeedRoot("", "Patient"));
        }
    }
}
