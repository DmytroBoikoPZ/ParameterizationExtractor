using System;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Tests.CharacterisationTests.Harness
{
    public static class TestDbConfig
    {
        public static string ResolveSourceConnectionString()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(TestContext.CurrentContext.TestDirectory)
                .AddJsonFile("appsettings.test.json", optional: false)
                .Build();

            var connectionString = config.GetConnectionString("Source");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException(
                    "Tests/appsettings.test.json must define ConnectionStrings:Source. " +
                    "See ongoing-tasks/characterisation-tests/02-characterisation-tests-test-db-config.md.");

            return connectionString;
        }
    }
}
