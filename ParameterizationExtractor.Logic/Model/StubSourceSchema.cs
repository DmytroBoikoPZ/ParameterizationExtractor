using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Logic.Model
{
    public class StubSourceSchema : ISourceSchema
    {
        public IEnumerable<PTableMetadata> Tables => new List<PTableMetadata>();

        public IEnumerable<PDependentTable> DependentTables => new List<PDependentTable>();

        public bool WasInit => true;

        public string Database => string.Empty;

        public string DataSource => string.Empty;

        public string GetConnectionString(string serverName, string dbName)
        {
            throw new NotImplementedException();
        }

        public PTableMetadata GetTableMetaData(string tableName)
        {
            throw new NotImplementedException();
        }

        public PTableMetadata ResolveTable(string schema, string tableName) => null;

        public Task Init(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
