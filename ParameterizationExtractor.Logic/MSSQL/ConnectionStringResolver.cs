using Quipu.ParameterizationExtractor.Logic.Interfaces;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Text;

namespace Quipu.ParameterizationExtractor.Logic.MSSQL
{
    public class ConnectionStringResolver : IConnectionStringResolver
    {
        public string GetConnectionString(string serverName, string dbName)
        {
            var builder = new SqlConnectionStringBuilder();
            builder.DataSource = serverName;
            builder.InitialCatalog = dbName;
            builder.Pooling = true;
            builder.MaxPoolSize = 2500;
            builder.IntegratedSecurity = true;
            builder.MultipleActiveResultSets = true;

            return builder.ToString();
        }
    }
}
