using Quipu.ParameterizationExtractor.Common;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Logic.Helpers
{
    public static class ConfigHelper
    {
        private const string regExp = "RegExp:";

        public static bool IsRegExp(string s)
        {
            return s.StartsWith(regExp);
        }

        public static string ExtractPattern(string s)
        {
            return s.Substring(s.IndexOf(':') + 1);
        }

        public static IList<PTableMetadata> GetTablesByRawName(ISourceSchema schema, string name)
        {
            Affirm.ArgumentNotNull(schema, nameof(schema));

            return GetTablesByPattern(schema, ExtractPattern(name));
        }

        public static IList<PTableMetadata> GetTablesByPattern(ISourceSchema schema, string pattern)
        {
            Affirm.ArgumentNotNull(schema, nameof(schema));

            return schema.Tables.Where(_ => Regex.IsMatch(_.TableName, pattern)).ToList();
        }

        public static Func<TableToExtract, string, bool> PredicateByName = (t, tn) => t.TableName.Equals(tn, StringComparison.InvariantCultureIgnoreCase);
        public static Func<TableToExtract, string, bool> PredicateByRegExp = (t, tn) => Regex.IsMatch(tn, ExtractPattern(t.TableName));
        public static Func<TableToExtract, string, bool> GetPredicateForTable(string tableName)
        {
            return IsRegExp(tableName) ? PredicateByRegExp : PredicateByName;
        }

        /// <summary>
        /// Schema-aware match (ADR-011): a config entry with empty Schema matches any
        /// discovered schema (back-compat); a config entry with explicit Schema matches
        /// only that Schema (case-insensitive).
        /// </summary>
        private static bool SchemaMatches(string configSchema, string discoveredSchema)
        {
            if (string.IsNullOrEmpty(configSchema)) return true;
            return string.Equals(configSchema, discoveredSchema ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Schema-aware lookup. <paramref name="discoveredSchema"/> may be empty; in that case
        /// any config entry with the matching <paramref name="tableName"/> wins regardless of its Schema.
        /// </summary>
        public static TableToExtract GetTableToExtract(string discoveredSchema, string tableName, ISourceForScript template)
        {
            var directName = template.TablesToProcess
                .Where(_ => !IsRegExp(_.TableName))
                .FirstOrDefault(_ => PredicateByName(_, tableName) && SchemaMatches(_.Schema ?? string.Empty, discoveredSchema));

            if (directName == null)
            {
                var matched = template.TablesToProcess
                    .Where(_ => IsRegExp(_.TableName))
                    .Where(_ => PredicateByRegExp(_, tableName) && SchemaMatches(_.Schema ?? string.Empty, discoveredSchema));

                if (matched.Count() > 1)
                    throw new InvalidOperationException($"For table {tableName} exists more than 1 table to process with matched RegExp");

                directName = matched.FirstOrDefault();
            }

            return directName;
        }

        /// <summary>Bare-name overload preserved for back-compat callers; routes to schema-aware lookup with empty schema.</summary>
        public static TableToExtract GetTableToExtract(string tableName, ISourceForScript template) =>
            GetTableToExtract(string.Empty, tableName, template);
    }
}
