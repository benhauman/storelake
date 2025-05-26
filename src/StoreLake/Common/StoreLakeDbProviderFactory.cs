namespace StoreLake.TestStore
{
    using System;
    using System.Data.Common;

    internal sealed class StoreLakeDbProviderFactory : DbProviderFactory // used+linked in Server.YouNeedTest / SLM.Administration
    {
        internal static StoreLakeDbProviderFactory CreateInstance(Action<StoreLakeDbProviderFactory> setup)
        {
            StoreLakeDbProviderFactory dbClient = new StoreLakeDbProviderFactory();
            setup(dbClient);
            return dbClient;
        }

        internal StoreLakeDbProviderFactory()
        {
        }

        internal Func<StoreLakeDbProviderFactory, StoreLakeDbConnection> CreateConnection_Override;
        public override DbConnection CreateConnection()
        {
            if (CreateConnection_Override == null)
                throw new NotImplementedException();
            //var connection = new DeConnection();
            //Connections.Add(connection);
            var conn = CreateConnection_Override(this);
            conn.dbClient = this;
            return conn;
        }

        //public List<DeCommand> Commands = new List<DeCommand>();

        internal Func<StoreLakeDbCommand> CreateCommand_Override { get; set; }
        public override DbCommand CreateCommand()
        {
            if (CreateCommand_Override != null)
                return CreateCommand_Override();
            throw new NotImplementedException("Test fix needed.");
            //var command = new DeCommand();
            //Commands.Add(command);
            //return command;
        }

        internal Func<StoreLakeDbDataAdapter> CreateDataAdapter_Override { get; set; }
        public override DbDataAdapter CreateDataAdapter()
        {
            if (CreateDataAdapter_Override != null)
            {
                return CreateDataAdapter_Override();
            }
            throw new NotImplementedException();
        }
    }
}