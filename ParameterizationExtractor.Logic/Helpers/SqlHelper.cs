using Quipu.ParameterizationExtractor.Logic.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor.Logic.Helpers
{
    public static class SqlHelper
    {
        /// <summary>
        /// Schema-aware T-SQL identifier (ADR-011). Returns <c>[Schema].[Table]</c> when
        /// <paramref name="emissionSchema"/> is non-empty (operator-supplied via config),
        /// or bare <paramref name="tableName"/> otherwise — preserving today's emission for legacy configs.
        /// </summary>
        public static string Qualify(string emissionSchema, string tableName)
        {
            return string.IsNullOrEmpty(emissionSchema)
                ? tableName
                : string.Format("[{0}].[{1}]", emissionSchema, tableName);
        }

        /// <summary>PRecord overload — reads <see cref="PRecord.EmissionSchema"/> + <see cref="PRecord.TableName"/>.</summary>
        public static string Qualify(PRecord record) => Qualify(record.EmissionSchema, record.TableName);

        /// <summary>
        /// Schema-aware identifier for the Deleter SP's <c>@TableName</c> parameter (which expects
        /// <c>schema.table</c> with no brackets). Falls back to bare <paramref name="tableName"/> for legacy.
        /// </summary>
        public static string QualifyForDeleter(string emissionSchema, string tableName)
        {
            return string.IsNullOrEmpty(emissionSchema)
                ? tableName
                : string.Format("{0}.{1}", emissionSchema, tableName);
        }

        /// <summary>PRecord overload.</summary>
        public static string QualifyForDeleter(PRecord record) => QualifyForDeleter(record.EmissionSchema, record.TableName);

        public static IEnumerable<PField> NotIdentityFields(PRecord table)
        {
            // all not identity fields filtered by SqlBuildStrategy.FieldsToExclude
            return table.Where(_ => !_.MetaData.IsIdentity 
                                                && table.SqlBuildStrategy.FieldsToExclude.All(__ => !__.Equals(_.FieldName, StringComparison.InvariantCultureIgnoreCase))
                                );
        }

        public static IEnumerable<PField> InjectSqlVariable(IEnumerable<PField> fields, string sqlVar, string fieldName)
        {         
            var f = fields.FirstOrDefault(_ => _.FieldName == fieldName);

            if (f != null)
                f.Expression = sqlVar;

            return fields;
        }

        public static IEnumerable<PField> PrepareFieldsForChild(PRecord child, string sqlVar, PDependentTable fk)
        {
            if (child.SqlBuildStrategy.IdentityInsert
                && child.TableName.Equals(fk.ParentTable, StringComparison.InvariantCultureIgnoreCase)
                && child.TableName.Equals(fk.ReferencedTable, StringComparison.InvariantCultureIgnoreCase))
                return child; // if reference on itself and identity insert, no sense to inject sql var which is not yet initialized. and we know the value

            return InjectSqlVariable(NotIdentityFields(child), sqlVar, child.TableName == fk.ParentTable ? fk.ParentColumn : fk.ReferencedColumn);
        }

        public static string GetSeparatedNameValueString(IEnumerable<PField> fields, string separator, Func<PField,string> valueGetter)
        {
            return string.Join(separator, fields.Where(_=>!string.IsNullOrEmpty(_.ValueToSqlString().Trim()))
                                                .Select(_ => string.Format("[{0}] = {1}", _.FieldName, valueGetter?.Invoke(_)))
                               );

        }

        public static string GetNameValueString(IEnumerable<PField> fields)
        {
            return GetSeparatedNameValueString(fields, " and ", _ => _.ValueToSqlString()).Replace("= null", "is null");
            //return string.Join(" and ", fields.Select(_ => string.Format("[{0}] = {1}", _.FieldName, _.ValueToSqlString())));
        }

        public static string GetNameValueForUpdateString(IEnumerable<PField> fields)
        {
            return GetSeparatedNameValueString(fields, " , ", _ => _.ValueToSqlString());
        }
        public static string GetNameNormalValueString(IEnumerable<PField> fields)
        {
            return GetSeparatedNameValueString(fields, " ", _ => PField.PrepareValueForScript(_.Value.ToString()));
            //return string.Join(" ", fields.Select(_ => string.Format("[{0}] = {1}", _.FieldName, _.Value)));
        }

        public static string IfExistsSql(PRecord table)
        {
            var fields = NotIdentityFields(table);

            var sql = new StringBuilder();

            sql.AppendFormat("if not exists(select top 1 1 from {0} where {1})", table.TableName, table.GetUniqueSqlWhere());
            sql.AppendLine();
            sql.AppendFormat("insert into {0} (", table.TableName);
            sql.Append(string.Join(",", fields.Select(_=>_.FieldName)));           
            sql.AppendFormat(")");
            sql.AppendLine("Values(");
            sql.Append(string.Join(",", fields.Select(_ => _.ValueToSqlString())));
            sql.Append(")");

            return sql.ToString();
        }
        public static bool IsNumericType(this object o)
        {
            return o.GetType().IsNumericType();
        }
        public static bool IsNumericType(this Type o)
        {
            switch (Type.GetTypeCode(o))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }


}
