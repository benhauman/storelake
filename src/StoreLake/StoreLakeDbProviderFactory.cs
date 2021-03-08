using System;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Dibix.TestStore
{
    public sealed class StoreLakeDbProviderFactory : DbProviderFactory  // used+linked in Server.YouNeedTest / SLM.Administration
    {
        public Func<StoreLakeDbProviderFactory, StoreLakeDbConnection> CreateConnection_Override;
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

        public Func<StoreLakeDbCommand> CreateCommand_Override { get; set; }
        public override DbCommand CreateCommand()
        {
            if (CreateCommand_Override != null)
                return CreateCommand_Override();
            throw new NotImplementedException("Test fix needed.");
            //var command = new DeCommand();
            //Commands.Add(command);
            //return command;
        }

        public Func<StoreLakeDbDataAdapter> CreateDataAdapter_Override { get; set; }
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