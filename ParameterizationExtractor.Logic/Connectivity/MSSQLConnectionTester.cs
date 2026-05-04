#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Quipu.ParameterizationExtractor.Logic.Connectivity
{
    public sealed class MSSQLConnectionTester : IConnectionTester
    {
        private const int ConnectTimeoutSeconds = 10;
        private const string TableCountSql = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0";
        private const string FkCountSql = "SELECT COUNT(*) FROM sys.foreign_keys WHERE is_ms_shipped = 0";

        private readonly ILogger<MSSQLConnectionTester> _log;

        public MSSQLConnectionTester(ILogger<MSSQLConnectionTester> log)
        {
            _log = log;
        }

        public async Task<ConnectionTestResult> TestAsync(string connectionString, CancellationToken ct = default)
        {
            SqlConnectionStringBuilder builder;
            try
            {
                builder = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = ConnectTimeoutSeconds,
                };
            }
            catch (ArgumentException ex)
            {
                _log.LogWarning(ex, "Connection-string parse failed");
                return new ConnectionTestResult(false, 0, 0, ex.Message);
            }

            _log.LogInformation(
                "TestAsync attempting {Server}/{Database} as auth={IntegratedSecurity} user={User} passwordLength={PasswordLength}",
                builder.DataSource, builder.InitialCatalog,
                builder.IntegratedSecurity ? "windows" : "sql",
                string.IsNullOrEmpty(builder.UserID) ? "(empty)" : builder.UserID,
                builder.Password?.Length ?? 0);

            try
            {
                await using var conn = new SqlConnection(builder.ConnectionString);
                await conn.OpenAsync(ct).ConfigureAwait(false);

                var tableCount = await ScalarCountAsync(conn, TableCountSql, ct).ConfigureAwait(false);
                var fkCount = await ScalarCountAsync(conn, FkCountSql, ct).ConfigureAwait(false);

                _log.LogInformation(
                    "Connection test ok against {Server}/{Database}: {TableCount} tables, {FkCount} FKs",
                    builder.DataSource, builder.InitialCatalog, tableCount, fkCount);

                return new ConnectionTestResult(true, tableCount, fkCount, null);
            }
            catch (SqlException ex)
            {
                _log.LogWarning(ex, "Connection test failed against {Server}/{Database}",
                    builder.DataSource, builder.InitialCatalog);
                return new ConnectionTestResult(false, 0, 0, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _log.LogWarning(ex, "Connection test rejected configuration for {Server}/{Database}",
                    builder.DataSource, builder.InitialCatalog);
                return new ConnectionTestResult(false, 0, 0, ex.Message);
            }
        }

        private static async Task<int> ScalarCountAsync(SqlConnection conn, string sql, CancellationToken ct)
        {
            await using var cmd = new SqlCommand(sql, conn);
            var raw = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return raw is int i ? i : Convert.ToInt32(raw);
        }
    }
}
