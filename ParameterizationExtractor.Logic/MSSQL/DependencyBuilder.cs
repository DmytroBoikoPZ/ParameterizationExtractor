using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ParameterizationExtractor;
using System.Threading;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Common;
using Quipu.ParameterizationExtractor.Logic.Model;
using System.Text.RegularExpressions;
using Quipu.ParameterizationExtractor.Logic.Helpers;
using Microsoft.Extensions.Logging;

namespace Quipu.ParameterizationExtractor.Logic.MSSQL
{
    public class DependencyBuilder : IDependencyBuilder
    {
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;
        private readonly ISourceSchema _schema;
        private readonly ILogger _log;
        private readonly IExtractConfiguration _configuration;

        public DependencyBuilder(IUnitOfWorkFactory unitOfWorkFactory, ISourceSchema schema, ILogger<DependencyBuilder> log, IExtractConfiguration configuration)
        {
            Affirm.ArgumentNotNull(unitOfWorkFactory, "unitOfWorkFactory");
            Affirm.ArgumentNotNull(schema, "schema");
            Affirm.ArgumentNotNull(log, "log");
            Affirm.ArgumentNotNull(configuration, "configuration");

            _unitOfWorkFactory = unitOfWorkFactory;
            _schema = schema;
            _log = log;
            _configuration = configuration;
        }

        private HashSet<PRecord> processedTables;

        public async Task<IEnumerable<PRecord>> PrepareAsync(CancellationToken cancellationToken, ISourceForScript template)
        {
            processedTables = new HashSet<PRecord>();
            var queue = new Queue<PRecord>();

            Func<string, string, string, Task> processTable = async (schema, tableName, where) =>
            {
                foreach (var rootTable in await GetPTables(schema, tableName, where, cancellationToken, template))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (rootTable != null)
                    {
                        rootTable.IsStartingPoint = true;
                        queue.Enqueue(rootTable);
                    }
                }
            };

            foreach (var root in template.RootRecords.OrderBy(_ => _.ProcessingOrder))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ConfigHelper.IsRegExp(root.TableName))
                {
                    var tables = ConfigHelper.GetTablesByRawName(_schema, root.TableName);

                    foreach (var table in tables)
                    {
                        // ADR-012 — skip roots whose TablesToProcess entry is Excluded.
                        if (IsExcluded(table.Schema ?? string.Empty, table.TableName, template)) continue;
                        await processTable(table.Schema ?? string.Empty, table.TableName, root.Where);
                    }
                }
                else
                {
                    if (IsExcluded(root.Schema ?? string.Empty, root.TableName, template)) continue;
                    await processTable(root.Schema ?? string.Empty, root.TableName, root.Where);
                }
            }

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _log.Debug("Start iteration");
                var record = queue.Dequeue();
                _log.DebugFormat("{0} is processing now. {1}", record.TableName, record.Source);
                if (!processedTables.Any(_ => _.Equals(record)))
                {
                    processedTables.Add(record);

                    foreach (var item in await GetRelatedTables(record, cancellationToken, template))
                    {
                        queue.Enqueue(item);
                    }

                }
                _log.Debug("End iteration");
            }

            return processedTables;
        }

        private ExtractStrategy GetExtractStrategy(string schema, string tableName, ISourceForScript template)
        {
            var t = ConfigHelper.GetTableToExtract(schema, tableName, template)?.ExtractStrategy;
            return t ?? _configuration.DefaultExtractStrategy;
        }

        private SqlBuildStrategy GetSqlBuildStrategy(string schema, string tableName, ISourceForScript template)
        {
            return ConfigHelper.GetTableToExtract(schema, tableName, template)?.SqlBuildStrategy
                        ?? _configuration.DefaultSqlBuildStrategy;
        }

        /// <summary>
        /// Schema-aware FK match: an FK end matches the current record when both the table name
        /// AND schema match (case-insensitive). Empty schemas degrade to bare-name match for
        /// pre-schema-aware metadata sources.
        /// </summary>
        /// <summary>
        /// True when the operator's <see cref="TableToExtract"/> entry for this table has
        /// <c>Excluded == true</c> — the engine skips it from FK walking and emission (ADR-012).
        /// </summary>
        private static bool IsExcluded(string schema, string tableName, ISourceForScript template) =>
            ConfigHelper.GetTableToExtract(schema, tableName, template)?.Excluded == true;

        private static bool FkEndMatches(string fkSchema, string fkTable, PRecord record)
        {
            var schemaMatches = string.IsNullOrEmpty(fkSchema)
                || string.IsNullOrEmpty(record.Schema)
                || string.Equals(fkSchema, record.Schema, StringComparison.OrdinalIgnoreCase);
            return schemaMatches && fkTable.Equals(record.TableName, StringComparison.InvariantCultureIgnoreCase);
        }

        private async Task<IEnumerable<PRecord>> GetRelatedTables(PRecord table, CancellationToken cancellationToken, ISourceForScript template)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new List<PRecord>();

            var tables = _schema.DependentTables.Where(_ => FkEndMatches(_.ParentSchema, _.ParentTable, table))
                                    .Union(_schema.DependentTables.Where(_ => FkEndMatches(_.ReferencedSchema, _.ReferencedTable, table)));

            foreach (var item in tables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _log.DebugFormat("parent table: {0}.{1} referenced table {2}.{3}", item.ParentSchema, item.ParentTable, item.ReferencedSchema, item.ReferencedTable);
                var extractStrategy = GetExtractStrategy(table.Schema, table.TableName, template);

                Func<string, string, string, string, Task<IEnumerable<PRecord>>> insertTable = async (schema, tableName, columnName, pkColumn) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var candidate = ConfigHelper.GetTableToExtract(schema, tableName, template);
                    if (candidate == null)
                        return await Task.FromResult<IEnumerable<PRecord>>(null);

                    // ADR-012 — skip Excluded tables during the FK walk.
                    if (candidate.Excluded)
                        return await Task.FromResult<IEnumerable<PRecord>>(null);

                    if (extractStrategy.DependencyToExclude.Any(_ => _.Equals(tableName, StringComparison.InvariantCultureIgnoreCase)))
                        return await Task.FromResult<IEnumerable<PRecord>>(null);

                    var value = table.FirstOrDefault(_ => _.FieldName.Equals(columnName, StringComparison.InvariantCultureIgnoreCase))?.ValueToSqlString();
                    if (value != null)
                    {
                        var str = string.Format("{0} = {1}", pkColumn, value);

                        var tableExtractStrategy = GetExtractStrategy(schema, tableName, template);

                        if (!string.IsNullOrEmpty(tableExtractStrategy.Where))
                            str = $"{str} AND {tableExtractStrategy.Where}";

                        return await GetPTables(schema, tableName, str, cancellationToken, template);
                    }

                    return await Task.FromResult<IEnumerable<PRecord>>(null);
                };

                if (FkEndMatches(item.ParentSchema, item.ParentTable, table)
                    && extractStrategy.ProcessParents)
                {
                    var i = await insertTable(item.ReferencedSchema, item.ReferencedTable, item.ParentColumn, item.ReferencedColumn);
                    if (i != null)
                    {
                        foreach (var parent in i)
                        {
                            table.Parents.Add(new PTableDependency() { PRecord = parent, FK = item });
                            result.Add(parent);
                        }
                    }
                }
                if (FkEndMatches(item.ReferencedSchema, item.ReferencedTable, table)
                    && extractStrategy.ProcessChildren)
                {
                    var i = await insertTable(item.ParentSchema, item.ParentTable, item.ReferencedColumn, item.ParentColumn);
                    if (i != null)
                    {
                        foreach (var child in i)
                        {
                            table.Childern.Add(new PTableDependency() { PRecord = child, FK = item });
                        }

                        result.AddRange(i);
                    }
                }
            }

            return result;
        }

        public Task<PRecord> GetPTable(string tableName, string objectId, CancellationToken cancellationToken, ISourceForScript template) =>
            GetPTable(string.Empty, tableName, objectId, cancellationToken, template);

        public async Task<PRecord> GetPTable(string schema, string tableName, string objectId, CancellationToken cancellationToken, ISourceForScript template)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new List<PRecord>();

            var tableMetaData = PrepareTableMetaData(template, schema, tableName);
            var qualifier = QualifyForSelect(tableMetaData);

            var sql = string.Format("select * from {0} where [{1}] = {2}", qualifier, tableMetaData.PK.FieldName, objectId);

            _log.DebugFormat("GetPTable : {0}", sql);
            var processed = processedTables.FirstOrDefault(_ => _.TableName.Equals(tableName, StringComparison.InvariantCultureIgnoreCase) && _.PK == objectId);
            if (processed != null)
            {
                _log.DebugFormat("Object ({0}) with id {1} has been found in processedTables", processed.TableName, processed.PK);
                return processed;
            }
            using (var uow = _unitOfWorkFactory.GetUnitOfWork())
            {
                var reader = await uow.ExecuteReaderAsync(sql, cancellationToken);

                while (reader.Read())
                {
                    result.Add(new PRecord(reader, tableMetaData)
                    {
                        Source = sql.Trim(),
                        ExtractStrategy = GetExtractStrategy(schema, tableName, template),
                        SqlBuildStrategy = GetSqlBuildStrategy(schema, tableName, template),
                        EmissionSchema = ConfigHelper.GetTableToExtract(tableMetaData.Schema ?? string.Empty, tableName, template)?.Schema ?? string.Empty,
                    });
                }
            }

            return result.FirstOrDefault();
        }

        private PTableMetadata PrepareTableMetaData(ISourceForScript template, string schema, string tableName)
        {
            // Schema-aware lookup (ADR-011). Empty schema → bare-name with one-or-throw policy.
            var tabMeta = _schema.ResolveTable(schema, tableName)
                          ?? throw new InvalidOperationException(
                              string.IsNullOrEmpty(schema)
                                  ? $"Table '{tableName}' was not found in source metadata."
                                  : $"Table '{schema}.{tableName}' was not found in source metadata.");

            var fromTemplate = ConfigHelper.GetTableToExtract(tabMeta.Schema ?? string.Empty, tableName, template);

            if (fromTemplate != null && fromTemplate.UniqueColumns != null && fromTemplate.UniqueColumns.Any())
                tabMeta.UniqueColumnsCollection = new List<string>(fromTemplate.UniqueColumns);

            return tabMeta;
        }

        public Task<IEnumerable<PRecord>> GetPTables(string tableName, string where, CancellationToken cancellationToken, ISourceForScript template) =>
            GetPTables(string.Empty, tableName, where, cancellationToken, template);

        public async Task<IEnumerable<PRecord>> GetPTables(string schema, string tableName, string where, CancellationToken cancellationToken, ISourceForScript template)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new List<PRecord>();

            var tableMeta = PrepareTableMetaData(template, schema, tableName);
            var qualifier = QualifyForSelect(tableMeta);

            var sql = string.Format("select * from {0} ", qualifier);
            if (!string.IsNullOrEmpty(where))
                sql = string.Format("{0} where {1} ", sql, where);

            _log.DebugFormat("GetPTables : {0}", sql);

            using (var uow = _unitOfWorkFactory.GetUnitOfWork())
            {
                var reader = await uow.ExecuteReaderAsync(sql, cancellationToken);

                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var record = new PRecord(reader, tableMeta)
                    {
                        Source = sql.Trim(),
                        ExtractStrategy = GetExtractStrategy(schema, tableName, template),
                        SqlBuildStrategy = GetSqlBuildStrategy(schema, tableName, template),
                        EmissionSchema = ConfigHelper.GetTableToExtract(tableMeta.Schema ?? string.Empty, tableName, template)?.Schema ?? string.Empty,
                    };
                    var processed = processedTables.FirstOrDefault(_ => _.Equals(record));
                    result.Add(processed ?? record);
                }
            }
            return result;
        }

        /// <summary>
        /// SELECT identifier qualifier — emits <c>[Schema].[Table]</c> when schema is non-empty,
        /// else bare <c>[Table]</c> (today's behaviour for legacy single-schema setups).
        /// </summary>
        private static string QualifyForSelect(PTableMetadata meta)
        {
            return string.IsNullOrEmpty(meta.Schema)
                ? string.Format("[{0}]", meta.TableName)
                : string.Format("[{0}].[{1}]", meta.Schema, meta.TableName);
        }
    }
}
