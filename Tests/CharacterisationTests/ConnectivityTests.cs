using Microsoft.Data.SqlClient;
using NUnit.Framework;
using Tests.CharacterisationTests.Harness;

namespace Tests.CharacterisationTests
{
    [TestFixture]
    public class ConnectivityTests
    {
        private string _connectionString;

        [OneTimeSetUp]
        public void LoadConfig()
        {
            _connectionString = TestDbConfig.ResolveSourceConnectionString();
        }

        [Test]
        public void Connects_To_TestDb_And_Selects_One()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var result = (int)cmd.ExecuteScalar();

            Assert.That(result, Is.EqualTo(1));
        }
    }
}
