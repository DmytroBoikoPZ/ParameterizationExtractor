using Quipu.ParameterizationExtractor.Common;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Quipu.ParameterizationExtractor.Logic.Model
{
    public class TableToExtract : IAmDSLFriendly
    {
        public TableToExtract() { }
        public TableToExtract(string tableName, ExtractStrategy extractStrategy) : this(tableName, extractStrategy, new SqlBuildStrategy())
        {

        }

        public TableToExtract(string tableName) : this(tableName,new FKDependencyExtractStrategy(), new SqlBuildStrategy())
        {

        }

        public TableToExtract(string tableName,  ExtractStrategy extractStrategy, SqlBuildStrategy sqlBuildStrategy)
        {
            Affirm.NotNullOrEmpty(tableName, "tableName");
            Affirm.ArgumentNotNull(extractStrategy, "extractStrategy");
            Affirm.ArgumentNotNull(sqlBuildStrategy, "sqlBuildStrategy");

            TableName = tableName;
            ExtractStrategy = extractStrategy;
            SqlBuildStrategy = sqlBuildStrategy;
        }

        /// <summary>
        /// Optional schema. Empty (default) means bare-name resolution via
        /// <c>MSSQLSourceSchema.ResolveTable</c>'s one-or-throw policy (ADR-011).
        /// </summary>
        [XmlAttribute("schema"), DefaultValue("")]
        [JsonPropertyName("schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Schema { get; set; }

        [XmlAttribute()]
        public string TableName { get; set; }
        [XmlAttribute()]
        public List<string> UniqueColumns { get; set; }

        /// <summary>
        /// When true, the engine skips this table during the FK walk: no rows are
        /// extracted and no SQL is emitted for it (ADR-012's v1 surface for the Graph
        /// tab's ✗ excluded state). Default <c>false</c>; back-compat preserved for
        /// legacy XML / DSL workspaces that omit the attribute.
        /// </summary>
        [XmlAttribute("excluded"), DefaultValue(false)]
        [JsonPropertyName("excluded"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Excluded { get; set; }

        public ExtractStrategy ExtractStrategy { get; set; }
        public SqlBuildStrategy SqlBuildStrategy { get; set; }

        public string AsString()
        {
            var s = string.Empty;
            if (UniqueColumns.Any())
                s = $"and UniqueColumns \"{string.Join(",", UniqueColumns?.ToArray())}\" ";

            var qualified = string.IsNullOrEmpty(Schema) ? TableName : $"{Schema}.{TableName}";
            return $"  {ExtractStrategy?.AsString()} for \"{qualified}\" {s} {SqlBuildStrategy?.AsString()}";
        }
    }

    public class RecordsToExtract : IAmDSLFriendly
    {
        public RecordsToExtract() { }
        public RecordsToExtract(string tableName, string where)
        {
            Affirm.NotNullOrEmpty(tableName, "tableName");

            TableName = tableName;
            Where = where;
        }

        /// <summary>
        /// Optional schema. Empty (default) means bare-name resolution via
        /// <c>MSSQLSourceSchema.ResolveTable</c>'s one-or-throw policy (ADR-011).
        /// </summary>
        [XmlAttribute("schema"), DefaultValue("")]
        [JsonPropertyName("schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Schema { get; set; }

        [XmlAttribute()]
        public string TableName { get; set; }
        [XmlAttribute()]
        public string Where { get; set; }
        [XmlAttribute()]
        public int ProcessingOrder { get; set; }

        public string AsString()
        {
            var qualified = string.IsNullOrEmpty(Schema) ? TableName : $"{Schema}.{TableName}";
            return $" from \"{qualified}\" where \"{Where}\" ";
        }
    }
}
