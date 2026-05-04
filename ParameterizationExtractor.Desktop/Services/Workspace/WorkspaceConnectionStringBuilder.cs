#nullable enable
using System;
using Microsoft.Data.SqlClient;

namespace Quipu.ParameterizationExtractor.Desktop.Services.Workspace;

internal static class WorkspaceConnectionStringBuilder
{
    /// <summary>
    /// Composes a SQL Server connection string from a workspace's <see cref="WorkspaceSource"/>
    /// and the (decrypted) plaintext password. Single call site for desktop consumers
    /// (connection editor + seed tab) so the assembly logic stays consistent.
    /// </summary>
    public static string Build(WorkspaceSource source, string? plaintextPassword)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        // Exception to the "no Microsoft.Data.SqlClient outside Logic" tripwire:
        // SqlConnectionStringBuilder is parameter quoting, not SQL execution. Single call site.
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = source.Server,
            InitialCatalog = source.Database,
            TrustServerCertificate = true,
        };
        if (string.Equals(source.Auth, "windows", StringComparison.OrdinalIgnoreCase))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = source.User ?? string.Empty;
            builder.Password = plaintextPassword ?? string.Empty;
        }
        return builder.ConnectionString;
    }
}
