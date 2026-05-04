using System;
using System.Collections.Generic;
using System.Linq;

namespace Quipu.ParameterizationExtractor.Logic.MSSQL
{
    /// <summary>
    /// Thrown by <see cref="TableResolver.Resolve"/> when a bare-name (no Schema) config entry
    /// matches multiple discovered tables across schemas. The operator's resolution is to add
    /// <c>Schema="..."</c> to the config entry. Per ADR-011 we never silently pick — the wrong
    /// pick produces silently-wrong SQL output, which is the failure mode this exception
    /// guards against.
    /// </summary>
    public sealed class AmbiguousTableException : InvalidOperationException
    {
        public AmbiguousTableException(string tableName, IEnumerable<string> candidateSchemas)
            : base(BuildMessage(tableName, candidateSchemas, out var captured))
        {
            TableName = tableName;
            CandidateSchemas = captured;
        }

        public string TableName { get; }
        public IReadOnlyList<string> CandidateSchemas { get; }

        private static string BuildMessage(string tableName, IEnumerable<string> candidateSchemas, out IReadOnlyList<string> captured)
        {
            captured = candidateSchemas?.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
            var quoted = string.Join(", ", captured.Select(s => $"'{s}'"));
            return $"Table '{tableName}' exists in multiple schemas: {quoted}. Specify Schema in the config to disambiguate.";
        }
    }
}
