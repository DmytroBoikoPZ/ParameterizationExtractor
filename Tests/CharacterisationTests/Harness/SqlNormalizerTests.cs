using NUnit.Framework;

namespace Tests.CharacterisationTests.Harness
{
    [TestFixture]
    public class SqlNormalizerTests
    {
        [Test]
        public void Empty_Or_Null_Input_Returns_Empty_String()
        {
            Assert.That(SqlNormalizer.Normalize(null), Is.EqualTo(string.Empty));
            Assert.That(SqlNormalizer.Normalize(string.Empty), Is.EqualTo(string.Empty));
            Assert.That(SqlNormalizer.Normalize("   \r\n  \r\n"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Crlf_Is_Normalised_To_Lf()
        {
            var input = "INSERT INTO Foo\r\nVALUES (1)\r\n";
            var output = SqlNormalizer.Normalize(input);
            Assert.That(output, Is.EqualTo("INSERT INTO Foo\nVALUES (1)\n"));
        }

        [Test]
        public void Trailing_Whitespace_Per_Line_Is_Stripped()
        {
            var input = "INSERT INTO Foo   \nVALUES (1)\t\t\n";
            var output = SqlNormalizer.Normalize(input);
            Assert.That(output, Is.EqualTo("INSERT INTO Foo\nVALUES (1)\n"));
        }

        [Test]
        public void Output_Always_Ends_With_Single_Newline()
        {
            Assert.That(SqlNormalizer.Normalize("a"),       Does.EndWith("\n"));
            Assert.That(SqlNormalizer.Normalize("a\n\n\n"), Is.EqualTo("a\n"));
        }

        [Test]
        public void Idempotent()
        {
            var input = "INSERT INTO Foo\r\n  VALUES (1)  \r\n\r\n";
            var once  = SqlNormalizer.Normalize(input);
            var twice = SqlNormalizer.Normalize(once);
            Assert.That(twice, Is.EqualTo(once));
        }

        [Test]
        public void Header_Style_Timestamp_On_Its_Own_Line_Is_Masked()
        {
            var input  = "/*\nVersion 1.0.0.0\n02.05.2026 15:04:05\nSource: 1.2.3.4\n*/\n";
            var output = SqlNormalizer.Normalize(input);
            Assert.That(output, Does.Contain("__GENERATED_TIMESTAMP__"));
            Assert.That(output, Does.Not.Match(@"\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}\n"));
        }

        [Test]
        public void Inline_Data_Timestamp_Inside_Values_Is_Preserved()
        {
            var input  = "values (1, 31.05.2021 20:37:31 +03:00, 'x')\n";
            var output = SqlNormalizer.Normalize(input);
            Assert.That(output, Does.Contain("31.05.2021 20:37:31 +03:00"),
                "Inline data values must not be masked.");
        }
    }
}
