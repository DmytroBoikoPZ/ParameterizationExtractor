using System;
using System.Collections.Generic;
using System.Linq;
using Quipu.ParameterizationExtractor.Logic.Model;

namespace Quipu.ParameterizationExtractor.Logic.MSSQL
{
    /// <summary>
    /// Implements the bare-name-vs-qualified lookup policy from ADR-011.
    /// Pure function over an <see cref="IEnumerable{PTableMetadata}"/> snapshot — easy to unit test
    /// without touching <see cref="MSSQLSourceSchema"/>'s constructor surface.
    /// </summary>
    public static class TableResolver
    {
        /// <summary>
        /// Resolves <paramref name="schema"/> + <paramref name="name"/> against
        /// <paramref name="tables"/>. Returns null when no match.
        /// Throws <see cref="AmbiguousTableException"/> when <paramref name="schema"/> is empty
        /// and <paramref name="name"/> matches multiple discovered schemas.
        /// </summary>
        public static PTableMetadata Resolve(IEnumerable<PTableMetadata> tables, string schema, string name)
        {
            if (tables is null) return null;
            if (string.IsNullOrEmpty(name)) return null;

            if (!string.IsNullOrEmpty(schema))
            {
                return tables.FirstOrDefault(t =>
                    string.Equals(t.Schema ?? string.Empty, schema, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.TableName ?? string.Empty, name, StringComparison.OrdinalIgnoreCase));
            }

            var matches = tables
                .Where(t => string.Equals(t.TableName ?? string.Empty, name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0) return null;
            if (matches.Count == 1) return matches[0];

            throw new AmbiguousTableException(name, matches.Select(m => m.Schema ?? string.Empty));
        }
    }
}
