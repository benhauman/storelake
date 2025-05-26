using System;
using System.Data;
using System.Diagnostics;
using Microsoft.SqlServer.TransactSql.ScriptDom;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    internal sealed class SourceColumn
    {
        internal readonly QueryColumnSourceBase Source;
        internal readonly DbType? ColumnDbType;
        internal readonly string SourceColumnName;
        internal readonly bool? AllowNull;
        public SourceColumn(QueryColumnSourceBase source, string sourceColumnName, DbType columnDbType, bool allowNull)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(sourceColumnName))
                throw new ArgumentNullException(nameof(sourceColumnName));
            Source = source;
            SourceColumnName = sourceColumnName;
            ColumnDbType = columnDbType;
            AllowNull = allowNull;
        }
        public SourceColumn(QueryColumnSourceBase source, string sourceColumnName)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(sourceColumnName))
                throw new ArgumentNullException(nameof(sourceColumnName));

            Source = source;
            SourceColumnName = sourceColumnName;
            ColumnDbType = null;
            AllowNull = null;
        }
        public SourceColumn(SourceColumn column, DbType columnDbType, bool allowNull)
           : this(column.Source, column.SourceColumnName, columnDbType, allowNull)
        {
        }

        public SourceColumn(SourceColumn column, bool allowNull) // NOT NULL => NULL
        {
            Source = column.Source;
            SourceColumnName = column.SourceColumnName;
            ColumnDbType = column.ColumnDbType;
            AllowNull = allowNull;
        }
    }

    internal sealed class SourceColumnType
    {
        internal readonly QueryColumnSourceBase Source;
        internal readonly string SourceColumnName;
        internal readonly DbType? ColumnDbType;
        internal readonly bool? AllowNull;
        public SourceColumnType(QueryColumnSourceBase source, string sourceColumnName, DbType columnDbType, bool allowNull)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(sourceColumnName))
                throw new ArgumentNullException(nameof(sourceColumnName));
            Source = source;
            SourceColumnName = sourceColumnName;
            ColumnDbType = columnDbType;
            AllowNull = allowNull;
        }
        public SourceColumnType(QueryColumnSourceBase source, string sourceColumnName)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(sourceColumnName))
                throw new ArgumentNullException(nameof(sourceColumnName));

            Source = source;
            SourceColumnName = sourceColumnName;
            ColumnDbType = null;
            AllowNull = null;
        }
    }
    internal abstract class QueryColumnBase
    {
        public abstract string OutputColumnName { get; }
        public abstract string SourceColumnName { get; }

        public readonly QueryColumnSourceBase Source;
        public abstract DbType? ColumnDbType { get; }

        public abstract bool? AllowNull { get; }

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
        private bool? _allowNull;

        public QueryColumnE(QueryColumnSourceBase source, string outputColumnName, string sourceColumnName, DbType? columnDbType, bool? allowNull)
            : base(source)
        {
            if (string.IsNullOrEmpty(outputColumnName))
                throw new ArgumentNullException(nameof(outputColumnName));

            this.outputColumnName = outputColumnName;
            this._sourceColumnName = sourceColumnName;
            this._columnDbType = columnDbType;
            this._allowNull = allowNull;
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

        public override bool? AllowNull => _allowNull;

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