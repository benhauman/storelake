using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Data;
using System.Diagnostics;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    internal abstract class QueryColumnBase
    {
        public abstract string OutputColumnName { get; }
        public abstract string SourceColumnName { get; }

        public readonly QueryColumnSourceBase Source;
        public abstract DbType? ColumnDbType { get; }

        public abstract void SetColumnDbType(DbType columnDbType);
        public abstract void SetSourceColumnName(string sourceColumnName, DbType columnDbType);

        internal int SourceId => Source.Id;
        protected QueryColumnBase(QueryColumnSourceBase source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            Source = source;
        }
    }

    [DebuggerDisplay("{DebuggerText}")]
    internal sealed class QueryColumnE : QueryColumnBase
    {
        private readonly string outputColumnName;
        private string _sourceColumnName;
        private DbType? _columnDbType;

        public QueryColumnE(QueryColumnSourceBase source, string outputColumnName, string sourceColumnName, DbType? columnDbType)
            : base(source)
        {
            if (string.IsNullOrEmpty(outputColumnName))
                throw new ArgumentNullException(nameof(outputColumnName));

            this.outputColumnName = outputColumnName;
            this._sourceColumnName = sourceColumnName;
            this._columnDbType = columnDbType;
        }
        private string DebuggerText
        {
            get
            {
                return (OutputColumnName ?? "?")
                    + " (" + (ColumnDbType.HasValue ? "" + ColumnDbType.Value : "?") + ")"
                    + " : " + (SourceColumnName ?? "?")
                    + " Source:" + SourceId
                    ;
            }
        }

        public override string OutputColumnName => outputColumnName;

        public override string SourceColumnName => _sourceColumnName;

        public override DbType? ColumnDbType => _columnDbType;

        public override void SetColumnDbType(DbType columnDbType)
        {
            if (ColumnDbType.HasValue)
                throw new InvalidOperationException("Column type already resolved.");
            this._columnDbType = columnDbType;
        }
        public override void SetSourceColumnName(string sourceColumnName, DbType columnDbType)
        {
            if (string.IsNullOrEmpty(sourceColumnName))
                throw new ArgumentNullException("sourceColumnName");

            if (string.IsNullOrEmpty(SourceColumnName))
                throw new InvalidOperationException("Column type already resolved.");

            if (ColumnDbType.HasValue)
                throw new InvalidOperationException("Column type already resolved.");

            this._sourceColumnName = sourceColumnName;
            this._columnDbType = columnDbType;
        }
    }
}