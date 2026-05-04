using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.Model;
using Quipu.ParameterizationExtractor.Common;
using Quipu.ParameterizationExtractor.Logic.Helpers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Quipu.ParameterizationExtractor.Logic.MSSQL
{
    public class MSSQLSourceSchema : ISourceSchema
    {        
        public static string sqlPKColumns = @"
select
  C.[object_id]
, object_name(C.[object_id])             as TableName
, C.column_id
, C.is_identity as IsIdentity 
, C.is_computed as IsComputed 
, C.[name] as ColumnName
, C.is_nullable as IsNullable
, T.[name] as TypeName
, TBase.[name] as BaseTypeName
, TBase.[name]+ case
  when (TBase.user_type_id in (165, 167, 173, 175, 231, 239)) then
   case
       when C.max_length = -1 then '(max)'
       else '('+
     case
         when T.system_type_id in (231, 239) then cast(C.max_length/2 as nvarchar(max))
         else cast(C.max_length as nvarchar(max))
     end
       +')'
       --else '('+cast(T.max_length as nvarchar(max))+')'
   end
  /*165-varbinary; 167-varchar; 173-binary; 175-char, 231-nvarchar; 239-nchar*/
  when (TBase.user_type_id in (106, 108))
   then '('+cast(C.[precision] as nvarchar(max))+','+cast(C.scale as nvarchar(max))+')'
  /*106-decimal; numeric-108*/
  else ''
     end
   as base_type_name_str
, case
      when exists
      (
       select * from sys.indexes i
       inner join sys.index_columns ic on ic.[object_id] = i.[object_id] and ic.index_id = i.index_id
       where ic.[object_id] = C.[object_id]
       and ic.column_id = C.column_id
       and i.is_primary_key = 1
      )
      then cast(1 as bit)
      else cast(0 as bit)
  end as IsInPK
from sys.[columns] C
inner join sys.types T on T.system_type_id = C.system_type_id and T.user_type_id = C.user_type_id
inner join sys.types TBase on T.system_type_id = TBase.system_type_id
 and TBase.system_type_id = TBase.user_type_id
where 
--C.[object_id] = object_id('BusinessProcesses_Id')
--C.[object_id] = object_id('LoanApplications')
--C.[object_id] = object_id('f_rep_GetContractStatement_byAccountNumber') 
 T.system_type_id not in (189, 240)
/*189-timestamp;240-all CLR*/
and C.is_computed = 0
";

        private IEnumerable<PDependentTable> _dependentTables;
        private IEnumerable<PTableMetadata> _tables;
        private IUnitOfWorkFactory _uowFactory;
        private IExtractConfiguration _globalConfiguration;
        private ILogger _log;
        private IObjectMetaDataProvider _objectMetaDataProvider;
        
        public MSSQLSourceSchema(IUnitOfWorkFactory uowf, IExtractConfiguration globalConfiguration, ILogger<MSSQLSourceSchema> log, IObjectMetaDataProvider objectMetaDataProvider)
        {
            Affirm.ArgumentNotNull(uowf, "uowf");
            Affirm.ArgumentNotNull(log, "log");
            Affirm.ArgumentNotNull(objectMetaDataProvider, "objectMetaDataProvider");

            _log = log;
            _uowFactory = uowf;
            _globalConfiguration = globalConfiguration;
            _objectMetaDataProvider = objectMetaDataProvider;
            WasInit = false;    
        }

        private void CheckInit()
        {
            if (!WasInit)
                throw new InvalidOperationException("MSSQLSourceSchema was not initialized!");
        }

        public IEnumerable<PDependentTable> DependentTables
        {
            get
            {
                CheckInit();
                return _dependentTables;
            }
        }

        public IEnumerable<PTableMetadata> Tables
        {
            get
            {
                CheckInit();
                return _tables;
            }
        }

        public bool WasInit
        {
            get; private set;
        }

        private string _database;
        public string Database
        {
            get
            {
                CheckInit();
                return _database;
            }
            private set
            {
                _database = value;
            }
        }

        private string _dataSource;
        public string DataSource
        {
            get
            {
                CheckInit();
                return _dataSource;
            }
            private set
            {
                _dataSource = value;
            }
        }

        public PTableMetadata GetTableMetaData(string tableName)
        {
            CheckInit();
            return Tables.First(_ => _.TableName.Equals(tableName, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Resolves a table by (Schema, Name) per ADR-011. Delegates to <see cref="TableResolver.Resolve"/>.
        /// </summary>
        public PTableMetadata ResolveTable(string schema, string tableName)
        {
            CheckInit();
            return TableResolver.Resolve(Tables, schema, tableName);
        }

        public async Task Init(CancellationToken cancellationToken)
        {
            _log.InfoFormat("MS SQL source schema init.");
            Tuple<string,string> sourceInfo;

            using (var uow = _uowFactory.GetUnitOfWork())
            {
                sourceInfo = new Tuple<string, string>(uow.Database, uow.DataSource);
            }
            
            _log.InfoFormat("Connected to Server: '{0}'; DataBase: '{1}'", sourceInfo.Item2, sourceInfo.Item1);

            _log.InfoFormat("Getting metadata...");
            var dTables = _objectMetaDataProvider.GetDependentTables(cancellationToken);          
            var MetaData = GetMetaData(new MetaDataInitializer(), cancellationToken);
            await Task.WhenAll(dTables, MetaData);

            _log.InfoFormat("Done");
            _log.DebugFormat("FKs: {0}", dTables.Result.Count());
            _log.DebugFormat("Tables: {0}", MetaData.Result.Count());

            _dependentTables = dTables.Result;
            _tables = MetaData.Result;
            _database = sourceInfo.Item1;
            _dataSource = sourceInfo.Item2;

            WasInit = true;
        }        

        private async Task<IEnumerable<PTableMetadata>> GetMetaData(IMetaDataInitializer initializer, CancellationToken cancellationToken)
        {
            var result = new List<PTableMetadata>();

            using (var uof = _uowFactory.GetUnitOfWork())
            {
                var metaTables = uof.GetSchemaAsync("Tables", cancellationToken);

                var metaColumns = uof.ExecuteReaderAsync(MSSQLSourceSchema.sqlPKColumns, cancellationToken);

                await Task.WhenAll(metaColumns, metaTables);

                var dt = new DataTable();
                dt.Load(metaColumns.Result);

                var iList = dt.Rows.Cast<DataRow>();

                foreach (DataRow t in metaTables.Result.Rows)
                {
                    var pTab = new PTableMetadata()
                    {
                        TableName = t["table_name"].ToString(),
                        Schema = t.Table.Columns.Contains("table_schema") ? (t["table_schema"]?.ToString() ?? string.Empty) : string.Empty,
                    };

                    foreach (DataRow c in iList.Where(_ => _["TableName"].ToString().Equals(pTab.TableName, StringComparison.InvariantCultureIgnoreCase)
                                                            && !_globalConfiguration.FieldsToExclude.Any(f => f == _["ColumnName"].ToString())))
                    {
                        var field = initializer.InitTableMetaData(c);

                        pTab.Add(field);

                    }

                    var uqCol =  _globalConfiguration
                                .UniqueColums
                                .Where(_=> !ConfigHelper.IsRegExp(_.TableName))
                                .FirstOrDefault(_ => _.TableName.Equals(pTab.TableName, StringComparison.InvariantCultureIgnoreCase));

                    if (uqCol == null) // if no configuration for concrete table name, let's try to check RegExp 
                    {
                        var uqCols = _globalConfiguration
                                    .UniqueColums
                                    .Where(_ => ConfigHelper.IsRegExp(_.TableName))
                                    .Where(_ => Regex.IsMatch(pTab.TableName, ConfigHelper.ExtractPattern(_.TableName)));

                        if (uqCols.Count() > 1)
                            throw new InvalidOperationException($"For table {pTab.TableName} ExtractConfig contains more than one RegExp rules for unique columns");

                        uqCol = uqCols.FirstOrDefault();
                    }

                    if (uqCol != null)
                        pTab.UniqueColumnsCollection = new List<string>(uqCol.UniqueColumns);

                    result.Add(pTab);
                }
            }

            return result;
        }
       
    }
}
