using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Logic.Model
{
    public class PTableMetadata : HashSet<PFieldMetadata>
    {
        public PTableMetadata()
        {
            UniqueColumnsCollection = new List<string>();
        }

        private PFieldMetadata _pk;
        public PFieldMetadata PK
        {
            get {
                _pk = _pk ?? this.First(_ => _.IsPK);

                return _pk;
            }
        }

        /// <summary>
        /// SQL schema for this discovered table (e.g. <c>"dbo"</c>). Always populated by
        /// <c>MSSQLSourceSchema.GetMetaData</c> from <c>INFORMATION_SCHEMA.TABLES</c>.
        /// Equality + GetHashCode use the <c>(Schema, TableName)</c> tuple, OrdinalIgnoreCase
        /// (ADR-011).
        /// </summary>
        public string Schema { get; set; } = string.Empty;
        public string TableName { get; set; }
        public IList<string> UniqueColumnsCollection { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is PTableMetadata other)
                return string.Equals(Schema ?? string.Empty, other.Schema ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(TableName ?? string.Empty, other.TableName ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            return base.Equals(obj);
        }

        public override int GetHashCode() =>
            HashCode.Combine(
                (Schema ?? string.Empty).ToLowerInvariant(),
                (TableName ?? string.Empty).ToLowerInvariant());
    }
     
    public class PFieldMetadata 
    {
        public PFieldMetadata()
        {

        }

        public string BaseTypeName { get; set; }
        public string SqlType { get; set; }
        public bool IsPK { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsNullable { get; set; }
        public bool IsComputed { get; set; }
        public string FieldName { get; set; }
        public Type FieldType { get; set; }

        public override bool Equals(object obj)
        {
            var p = obj as PFieldMetadata;
            if (p != null)
                return p.FieldName == this.FieldName;

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    [DebuggerDisplay("{ParentSchema}.{ParentTable}-{ReferencedSchema}.{ReferencedTable}-{ParentColumn}-{ReferencedColumn}")]
    public class PDependentTable
    {
        public string Name { get; set; }

        /// <summary>FK source-table schema. Populated by metadata reader; empty when source is pre-schema-aware.</summary>
        public string ParentSchema { get; set; } = string.Empty;
        public string ParentTable { get; set; }
        public string ParentColumn { get; set; }

        /// <summary>FK target-table schema. Populated by metadata reader; empty when source is pre-schema-aware.</summary>
        public string ReferencedSchema { get; set; } = string.Empty;
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
    }
}
