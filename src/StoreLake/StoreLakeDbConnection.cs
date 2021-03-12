using System;
using System.Data.Common;


namespace StoreLake.TestStore
{
    public sealed class StoreLakeDbConnection : DbConnection
    {
        public StoreLakeDbConnection(StoreLakeDbProviderFactory dbClient)
        {
            this.dbClient = dbClient;
            //this.CreateCommand_Override = connection => new StoreLakeDbCommand(connection);

        }
        public StoreLakeDbProviderFactory dbClient;
        protected override DbProviderFactory DbProviderFactory
        {
            get
            {
                return this.dbClient;
            }
        }
        public string ConnectionStringProperty;
        public override string ConnectionString
        {
            get
            {
                return ConnectionStringProperty;
            }
            set
            {
                ConnectionStringProperty = value;
            }
        }

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
        {
            return new StoreLakeDbTransaction()
            {
                DbConnection_Property = this,
                IsolationLevel_Property = isolationLevel,
            };
        }

        public override void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            //throw new NotImplementedException();
        }

        public Func<DbConnection, DbCommand> CreateCommand_Override;// = () => new DeCommand();

        protected override DbCommand CreateDbCommand()
        {
            if (CreateCommand_Override == null)
                throw new NotImplementedException();
            return CreateCommand_Override(this);
        }

        public override string DataSource
        {
            get { throw new NotImplementedException(); }
        }

        public override string Database
        {
            get { throw new NotImplementedException(); }
        }
        public Action Open_Override { get; set; }// = () => { };
        public bool opened;
        public override void Open()
        {
            if (Open_Override != null)
            {
                Open_Override();
            }
            else
            {
                if (opened)
                    throw new InvalidOperationException("Already opened.");
                this.opened = true;
            }
        }

        public override string ServerVersion
        {
            get { throw new NotImplementedException(); }
        }

        public override System.Data.ConnectionState State
        {
            get
            {
                return opened ? System.Data.ConnectionState.Open : System.Data.ConnectionState.Closed;
            }
        }

        private object connection_info;
        internal void SetContextInfo(object ci)
        {
            connection_info = ci;
        }

        internal T GetContextInfoAs<T>()
        {
            return (T)connection_info;
        }
    }
}