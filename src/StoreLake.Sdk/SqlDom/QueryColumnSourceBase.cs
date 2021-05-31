using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    internal abstract class QueryColumnSourceBase
    {
        protected string Alias;

        private readonly string _key;
        internal readonly int Id; // unique in the whole batch
        public QueryColumnSourceBase(int id, string key)
        {
            this.Id = id;
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException("key");
            _key = key;
        }

        internal string Key => _key;

        internal static string BuildKey(SchemaObjectName schemaObject, Identifier alias)
        {
            if (alias != null)
            {
                return alias.Dequote();
            }
            else
            {
                if (schemaObject.SchemaIdentifier != null)
                {
                    return schemaObject.SchemaIdentifier.Dequote() + " . " +
                           schemaObject.BaseIdentifier.Dequote();
                }
                else
                {
                    return schemaObject.BaseIdentifier.Dequote();
                }
            }
        }

        public QueryColumnSourceBase SetAlias(Identifier alias)
        {
            Alias = alias == null ? null : alias.Dequote();
            return this;
        }

        internal abstract bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType);
    }


    [DebuggerDisplay("NT {DebuggerText}")]
    internal sealed class QueryColumnSourceNT : QueryColumnSourceBase
    {
        internal readonly NamedTableReference NtRef;
        internal readonly string SchemaName;
        internal readonly string TableName;
        public QueryColumnSourceNT(int id, NamedTableReference ntRef)
            : base(id, BuildKey(ntRef.SchemaObject, ntRef.Alias))
        {
            NtRef = ntRef;
            SchemaName = NtRef.SchemaObject.SchemaIdentifier != null
                ? NtRef.SchemaObject.SchemaIdentifier.Dequote()
                : null;
            TableName = NtRef.SchemaObject.BaseIdentifier.Dequote();
            //SetAlias(ntRef.Alias);
        }
        private string DebuggerText
        {
            get
            {
                return (SchemaName == null ? "" : "[" + SchemaName + "]")
                    + "[" + TableName + "]"
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }

        IColumnSourceMetadata resolved_table;
        //internal QueryColumnBase override_TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName)
        //{
        //    if (resolved_table == null)
        //    {
        //        resolved_table = batchResolver.SchemaMetadata.TryGetColumnSourceMetadata(SchemaName, TableName);
        //        if (resolved_table == null)
        //        {
        //            // table not exists???
        //            throw new NotImplementedException(Key + "." + sourceColumnName + " => " + outputColumnName);

        //        }
        //    }

        //    string outputColumnNameSafe = outputColumnName ?? sourceColumnName;

        //    if (IsOutputColumnResolved(outputColumnNameSafe, out QueryColumnBase col))
        //    {
        //        if (col.ColumnDbType.HasValue)
        //        {
        //            return col;
        //        }
        //    }

        //    DbType? columnDbType = resolved_table.TryGetColumnTypeByName(sourceColumnName);
        //    if (columnDbType == null)
        //    {
        //        return null;
        //    }
        //    if (col != null)
        //    {
        //        col.SetColumnDbType(columnDbType.Value);
        //        return col;
        //    }
        //    else
        //    {
        //        return base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnNameSafe, sourceColumnName, columnDbType.Value));
        //    }
        //}

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnDbType)
        {
            if (string.IsNullOrEmpty(sourceColumnName))
                throw new ArgumentNullException(nameof(sourceColumnName));

            if (resolved_table == null)
            {
                resolved_table = batchResolver.SchemaMetadata.TryGetColumnSourceMetadata(SchemaName, TableName);
                if (resolved_table == null)
                {
                    // table not exists???
                    throw new NotImplementedException(Key + "." + sourceColumnName);

                }
            }

            DbType? sourcColumnDbType = resolved_table.TryGetColumnTypeByName(sourceColumnName);
            if (sourcColumnDbType.HasValue)
            {
                columnDbType = sourcColumnDbType.Value;
                return true;
            }
            else
            {
                //throw new NotImplementedException(Key + "." + sourceColumnName + "   Table: [" + SchemaName + "].[" + TableName + "]");
                columnDbType = DbType.Object; // column 'personid' without alias => source traversion
                return false;
            }
        }

        //private bool override_TryResolveOutputColumn(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string sourceColumnName)
        //{
        //    if (resolved_table == null)
        //    {
        //        resolved_table = batchResolver.SchemaMetadata.TryGetColumnSourceMetadata(SchemaName, TableName);
        //        if (resolved_table == null)
        //        {
        //            // table not exists???
        //            throw new NotImplementedException(Key + "." + sourceColumnName);

        //        }
        //    }

        //    string outputColumnNameSafe = sourceColumnName;

        //    DbType? columnDbType = resolved_table.TryGetColumnTypeByName(sourceColumnName);
        //    if (columnDbType == null)
        //    {
        //        return false;
        //    }

        //    base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnNameSafe, sourceColumnName, columnDbType.Value));
        //    return true;
        //}
    }

    [DebuggerDisplay("UDTF:{DebuggerText}")]
    internal sealed class QueryColumnSourceUDTF : QueryColumnSourceBase
    {
        internal readonly SchemaObjectFunctionTableReference Udtf;
        internal readonly string SchemaName;
        internal readonly string FunctionName;
        public QueryColumnSourceUDTF(int id, SchemaObjectFunctionTableReference udtf)
            : base(id, BuildKey(udtf.SchemaObject, udtf.Alias))
        {
            Udtf = udtf;
            SchemaName = udtf.SchemaObject.SchemaIdentifier != null
                ? udtf.SchemaObject.SchemaIdentifier.Dequote()
                : null;
            FunctionName = udtf.SchemaObject.BaseIdentifier.Dequote();
            //SetAlias(udtf.Alias);
        }

        private string DebuggerText
        {
            get
            {
                return (SchemaName == null ? "" : "[" + SchemaName + "]")
                    + "[" + FunctionName + "]"
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }

        private IColumnSourceMetadata resolved_source_metadata;
        //internal QueryColumnBase override_TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName)
        //{
        //    if (resolved_source_metadata == null)
        //    {
        //        resolved_source_metadata = batchResolver.SchemaMetadata.TryGetFunctionTableMetadata(SchemaName, FunctionName);
        //        if (resolved_source_metadata == null)
        //        {
        //            throw new NotImplementedException(SchemaName + "." + FunctionName + "  => " + outputColumnName);
        //        }
        //    }

        //    DbType? columnDbType = resolved_source_metadata.TryGetColumnTypeByName(sourceColumnName);
        //    if (columnDbType == null)
        //    {
        //        return null;
        //    }

        //    string outputColumnNameSafe = outputColumnName ?? sourceColumnName;

        //    return base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnNameSafe, sourceColumnName, columnDbType.Value));
        //}

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            if (resolved_source_metadata == null)
            {
                resolved_source_metadata = batchResolver.SchemaMetadata.TryGetFunctionTableMetadata(SchemaName, FunctionName);
                if (resolved_source_metadata == null)
                {
                    throw new NotImplementedException(SchemaName + "." + FunctionName + "  : " + sourceColumnName);
                }
            }


            DbType? columnDbType = resolved_source_metadata.TryGetColumnTypeByName(sourceColumnName);
            if (columnDbType.HasValue)
            {
                columnType = columnDbType.Value;
                return true;
            }

            columnType = DbType.Object;
            return false;
        }
        //???private bool override_TryResolveOutputColumn(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string sourceColumnName)
        //???{
        //???    throw new NotImplementedException();
        //???}

    }

    [DebuggerDisplay("VarT: {DebuggerText}")]
    internal sealed class QueryColumnSourceVarTable : QueryColumnSourceBase
    {
        internal readonly VariableTableReference VarTableRef;
        internal readonly string VariableName;
        public QueryColumnSourceVarTable(int id, VariableTableReference varTableRef)
            : base(id, varTableRef.Alias.Dequote())
        {
            VarTableRef = varTableRef;
            VariableName = varTableRef.Variable.Name;
            //SetAlias(varTableRef.Alias);
        }

        private string DebuggerText
        {
            get
            {
                return VariableName
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }

        IColumnSourceMetadata resolved_source_metadata;
        //private QueryColumnBase override_TryResolveSelectedColumn(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, string sourceColumnName)
        //{
        //    if (resolved_source_metadata == null)
        //    {
        //        resolved_source_metadata = batchResolver.TryGetTableVariable(VarTableRef.Variable.Name);
        //        if (resolved_source_metadata == null)
        //        {
        //            throw new NotImplementedException(VarTableRef.WhatIsThis());
        //        }
        //    }

        //    return base.AddResolveOutputdColumn(new QueryColumnE(this, outputColumnName, sourceColumnName, columnDbType.Value));

        //}

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            if (resolved_source_metadata == null)
            {
                resolved_source_metadata = batchResolver.TryGetTableVariable(VarTableRef.Variable.Name);
                if (resolved_source_metadata == null)
                {
                    throw new NotImplementedException(VarTableRef.WhatIsThis());
                }
            }

            DbType? columnDbType = resolved_source_metadata.TryGetColumnTypeByName(sourceColumnName);
            if (columnDbType.HasValue)
            {
                columnType = columnDbType.Value;
                return true;
            }

            columnType = DbType.Object;
            return false;
        }
        //private bool override_TryResolveOutputColumn(BatchOutputColumnTypeResolver batchResolver, string sourceNameOrAlias, string sourceColumnName)
        //{
        //    if (resolved_source_metadata == null)
        //    {
        //        resolved_source_metadata = batchResolver.TryGetTableVariable(VarTableRef.Variable.Name);
        //        if (resolved_source_metadata == null)
        //        {
        //            throw new NotImplementedException(VarTableRef.WhatIsThis());
        //        }
        //    }

        //    DbType? columnDbType = resolved_source_metadata.TryGetColumnTypeByName(sourceColumnName);
        //    if (columnDbType == null)
        //    {
        //        return false;
        //    }
        //    base.AddResolveOutputdColumn(new QueryColumnE(this, sourceColumnName, sourceColumnName, columnDbType.Value));
        //    return true;

        //}

    }


    [DebuggerDisplay("VALUES: {DebuggerText}")]
    internal sealed class QueryColumnSourceVALUES : QueryColumnSourceBase
    {
        internal readonly InlineDerivedTable _derivedTable;
        public QueryColumnSourceVALUES(int id, InlineDerivedTable derivedTable)
            : base(id, derivedTable.Alias.Dequote())
        {
            _derivedTable = derivedTable;
            //SetAlias(varTableRef.Alias);
        }

        private string DebuggerText
        {
            get
            {
                return "(...)"
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }

        private readonly IDictionary<string, DbType> columns = new SortedDictionary<string, DbType>();
        internal void AddValueColumn(string columnName, DbType columnDbType)
        {
            columns.Add(columnName.ToUpperInvariant(), columnDbType);
        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            if (columns.TryGetValue(sourceColumnName.ToUpperInvariant(), out columnType))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }


    [DebuggerDisplay("NSQ: {DebuggerText}")] // NamedSubCueryOrCte
    internal sealed class QuerySourceOnQuery : QueryColumnSourceBase
    {
        private readonly IQueryModel query;
        public QuerySourceOnQuery(int id, string alias, IQueryModel query)
            : base(id, alias)
        {
            this.query = query;
        }

        private string DebuggerText
        {
            get
            {
                return "(...)"
                    + (Alias == null ? "" : " AS [" + Alias + "]");
            }
        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            if (this.query.TryGetQueryOutputColumn(batchResolver, sourceColumnName, out QueryColumnBase outputColumn))
            {
                columnType = outputColumn.ColumnDbType.Value;
                return true;
            }
            columnType = DbType.Object;
            return false;
        }
    }

    internal sealed class QuerySourceOnConstant : QueryColumnSourceBase
    {
        private readonly DbType constantType;

        public QuerySourceOnConstant(int id, string key, DbType constantType) : base(id, key)
        {
            this.constantType = constantType;
        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            columnType = constantType;
            return true;
        }
    }

    internal sealed class QuerySourceOnNull : QueryColumnSourceBase
    {
        public QuerySourceOnNull(int id, string key) : base(id, key)
        {
        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            columnType = DbType.Object;
            return false;
        }
    }

    internal sealed class QuerySourceOnVariable : QueryColumnSourceBase
    {
        private readonly DbType variableType;

        public QuerySourceOnVariable(int id, VariableReference varRef, DbType variableType) : base(id, varRef.Name)
        {
            this.variableType = variableType;
        }

        internal override bool TryResolveSourceColumnType(BatchOutputColumnTypeResolver batchResolver, string sourceColumnName, out DbType columnType)
        {
            columnType = variableType;
            return true;
        }
    }

}