namespace StoreLake.TestStore
{
    using System;
    using System.Data.Common;

    internal sealed class StoreLakeDbCommand : DbCommand
    {
        private StoreLakeDbConnection _dbConnectionProperty;

        public StoreLakeDbCommand(DbConnection connection)
        {
            DbConnection = connection;
        }
        protected override DbConnection DbConnection
        {
            get
            {
                return _dbConnectionProperty;
            }
            set
            {
                _dbConnectionProperty = (StoreLakeDbConnection)value;
            }
        }

        public override void Cancel()
        {
            /// throw new NotImplementedException();
        }

        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }

        public override System.Data.CommandType CommandType { get; set; }
        protected override DbParameter CreateDbParameter()
        {
            return new StoreLakeDbParameter();
        }

        private StoreLakeDbParameterCollection _dbParameterCollectionProperty;
        protected override DbParameterCollection DbParameterCollection
        {
            get
            {
                if (_dbParameterCollectionProperty == null)
                {
                    _dbParameterCollectionProperty = new StoreLakeDbParameterCollection();
                }
                return _dbParameterCollectionProperty;
            }
        }

        protected override DbTransaction DbTransaction { get; set; }

        public override bool DesignTimeVisible
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        public Func<DbCommand, System.Data.CommandBehavior, DbDataReader> ExecuteDbDataReader_Override { get; set; }

        protected override DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior)
        {
            if (ExecuteDbDataReader_Override != null)
            {
                var reader = ExecuteDbDataReader_Override(this, behavior);
                if (reader == null)
                {
                    throw new NotImplementedException("Fixme (" + this.Parameters.Count + "):" + this.CommandText);
                }
                return reader;
            }
            throw new NotImplementedException("Fixme (" + this.Parameters.Count + "):" + this.CommandText);
        }

        public Func<DbCommand, int> ExecuteNonQuery_Override { get; set; }
        public override int ExecuteNonQuery()
        {
            if (ExecuteNonQuery_Override == null)
                throw new InvalidOperationException("Test fix needed: SQL(" + this.CommandType + "):" + Environment.NewLine + this.CommandText);
            return ExecuteNonQuery_Override(this);
        }

        public override object ExecuteScalar()
        {
            throw new NotImplementedException();
        }

        public override void Prepare()
        {
            throw new NotImplementedException();
        }

        public override System.Data.UpdateRowSource UpdatedRowSource
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
    }
}