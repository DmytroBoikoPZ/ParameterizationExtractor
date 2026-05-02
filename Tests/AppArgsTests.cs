using NUnit.Framework;
using Quipu.ParameterizationExtractor;

namespace Tests
{
    /// <summary>
    /// Pins the current behaviour of <see cref="AppArgs.GetAppArgs(string[])"/> as implemented
    /// against FluentCommandLineParser. Step 03 of the `replace-stale-deps` feature swaps in
    /// `CommandLineParser`; these same tests must remain green (any divergence is a deliberate
    /// recorded decision, not a silent change).
    ///
    /// Where the tests pin a quirk, a comment above the test calls it out.
    /// </summary>
    [TestFixture]
    public class AppArgsTests
    {
        // -------- PathToPackage --------

        // Deliberate divergence from the FCLP era (recorded in replace-stale-deps step 03 notes):
        // FCLP's `.Required()` did not throw — it silently returned an `AppArgs` with
        // PathToPackage = null because the original `GetAppArgs` discarded the parse result.
        // CommandLineParser raises a `NotParsed<AppArgs>` result for missing required options,
        // which we translate to an exception. Failing fast is the right behaviour; the prior
        // silent-null was a bug.
        [Test]
        public void PathToPackage_Missing_Throws()
        {
            Assert.Throws<System.Exception>(() => AppArgs.GetAppArgs(new string[0]));
        }

        [Test]
        public void PathToPackage_Short_Form()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "somepkg" });
            Assert.That(a.PathToPackage, Is.EqualTo("somepkg"));
        }

        [Test]
        public void PathToPackage_Long_Form()
        {
            var a = AppArgs.GetAppArgs(new[] { "--package", "somepkg" });
            Assert.That(a.PathToPackage, Is.EqualTo("somepkg"));
        }

        // -------- ConnectionName --------

        [Test]
        public void ConnectionName_Default_Is_SourceDB()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x" });
            Assert.That(a.ConnectionName, Is.EqualTo("SourceDB"));
        }

        [Test]
        public void ConnectionName_Short_Form_Overrides_Default()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x", "-n", "MyConn" });
            Assert.That(a.ConnectionName, Is.EqualTo("MyConn"));
        }

        [Test]
        public void ConnectionName_Long_Form_Overrides_Default()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x", "--connectionName", "MyConn" });
            Assert.That(a.ConnectionName, Is.EqualTo("MyConn"));
        }

        // -------- OutputFolder --------

        [Test]
        public void OutputFolder_Default_Is_Output()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x" });
            Assert.That(a.OutputFolder, Is.EqualTo("Output"));
        }

        // Pinned quirk: FCLP treats a leading `/` as a switch prefix on Windows, so values like
        // "/tmp" don't bind to `-o`. Using a non-slash value keeps the test about override
        // behaviour (which is what we're pinning), not about FCLP's switch-prefix parsing.
        [Test]
        public void OutputFolder_Short_Override()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x", "-o", "MyOut" });
            Assert.That(a.OutputFolder, Is.EqualTo("MyOut"));
        }

        // -------- Interactive --------

        [Test]
        public void Interactive_Default_Is_False()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x" });
            Assert.That(a.Interactive, Is.False);
        }

        [Test]
        public void Interactive_Short_Form_True()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x", "-i", "true" });
            Assert.That(a.Interactive, Is.True);
        }

        [Test]
        public void Interactive_Long_Form_True()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x", "--Interactive", "true" });
            Assert.That(a.Interactive, Is.True);
        }

        // -------- DBName / ServerName cross-arg validation --------

        [Test]
        public void DBName_And_ServerName_Both_Set_No_Throw()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x", "-d", "Db1", "-s", "Srv1" });
            Assert.That(a.DBName, Is.EqualTo("Db1"));
            Assert.That(a.ServerName, Is.EqualTo("Srv1"));
        }

        // Pinned bug: when only DBName is provided and ServerName is missing, the code at
        // AppBootstrap.cs:65-66 throws with message "Please specify DBName!". The message is
        // semantically wrong (DBName *is* specified — the missing one is ServerName). Pin the
        // current message exactly. Fixing the message is a separate follow-up after this feature.
        [Test]
        public void DBName_Without_ServerName_Throws_With_Pinned_DBName_Message()
        {
            var ex = Assert.Throws<System.Exception>(() =>
                AppArgs.GetAppArgs(new[] { "-p", "x", "-d", "Db1" }));
            Assert.That(ex.Message, Is.EqualTo("Please specify DBName!"));
        }

        // Pinned bug: symmetrical to the above. ServerName provided without DBName throws
        // "Please specify ServerName!" — the message names the arg the user *did* specify.
        [Test]
        public void ServerName_Without_DBName_Throws_With_Pinned_ServerName_Message()
        {
            var ex = Assert.Throws<System.Exception>(() =>
                AppArgs.GetAppArgs(new[] { "-p", "x", "-s", "Srv1" }));
            Assert.That(ex.Message, Is.EqualTo("Please specify ServerName!"));
        }

        [Test]
        public void Neither_DBName_Nor_ServerName_Is_Allowed_And_Returns_Both_Empty()
        {
            var a = AppArgs.GetAppArgs(new[] { "-p", "x" });
            Assert.That(a.DBName, Is.Null.Or.Empty);
            Assert.That(a.ServerName, Is.Null.Or.Empty);
        }
    }
}
