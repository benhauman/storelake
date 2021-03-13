using Dibix.TestStore;
using Dibix.TestStore.Database;
using Helpline.Data.TestStore;
using Helpline.SLM.Database.Data.TestStore;
using StoreLake.TestStore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace ConsoleApp4
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Test01();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void Test01()
        {
            var db = StoreLakeDatabaseServer.CreateDatabase("MyDB")
                .Use(DatabaseGetUtcDateExtension.Register)
                .Use(HelplineDataExtensions.RegisterDataSetModel) // using 'Helpline.Data.TestStore.HelplineDataExtensions'
                .Use(SLMDatabaseDataExtensions.RegisterDataSetModel) // using Helpline.SLM.Database.Data.TestStore.SLMDatabaseDataExtensions
                .Use(DatabaseGetUtcDateExtension.Register)
                .Build();

            Console.WriteLine("timenow:" + db.GetUtcDate());

            var hlsysagent = db.hlsysagent();
            var agent1 = db.hlsysagent().AddRowWithValues(agentid: 710, name: "InternetAgent", fullname: null, description: null, active: 1); //  see 'DF_hlsysagent_active'

            //agent1.agentid = 710;
            agent1.description = agent1.name + ":" + agent1.agentid;

            // NULL defaults?
            // DEFAULT values?
            // UNIQUE Key?
            // AutoIncrement?
            // ReadOnlyPK
            var agent2 = db.hlsysagent().AddRowWithValues(712, "InternetAgent2", null, null, 1); //e.active = 1; see 'DF_hlsysagent_active'
            //db.hlsysaccount().Last ;

            Console.WriteLine("db.hlsysagent.Count:" + db.hlsysagent().Count);

            StoreLakeDbServer dbServer = new StoreLakeDbServer(db);
            dbServer.RegisterHandlerReadWithCommandText((d, c) => DemoHandler1.GetAgentNameById(d, c)); // any / text-static
            dbServer.RegisterHandlerReadForCommandText(typeof(TestDML), (d, c) => DemoHandler1.GetAgentInfoById(d, c)); // static-text / static

            StoreLakeDbProviderFactory dbClient = StoreLakeDbProviderFactory.CreateInstance(x =>
            {
                x.CreateConnection_Override = dbServer.CreateConnection;
            });

            IDatabaseAccessorFactory databaseAccessorFactory = new IDatabaseAccessorFactory(dbClient, "blah");
            var name = TestDML.GetAgentNameById(databaseAccessorFactory, 712).Single();
            Console.WriteLine(name);
            var fname = TestDML.GetAgentInfoById(databaseAccessorFactory, 712).Single();
            Console.WriteLine(fname);
        }

        public class IDatabaseAccessorFactory
        {
            private readonly DbProviderFactory dbClient;
            private readonly string connectionString;
            public IDatabaseAccessorFactory(DbProviderFactory dbClient, string connectionString)
            {
                this.dbClient = dbClient;
                this.connectionString = connectionString;
            }
            public DbConnection CreateConnection()
            {
                DbConnection connection = dbClient.CreateConnection();
                connection.ConnectionString = connectionString;
                return connection;
            }
        }

        public static class TestDML
        {
            private const string GetAgentNameByIdCommandText = "SELECT name FROM hlsysagent WHERE agentid=@id";
            public static IEnumerable<string> GetAgentNameById(IDatabaseAccessorFactory databaseAccessorFactory, int id)
            {
                using (DbConnection connection = databaseAccessorFactory.CreateConnection())
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = GetAgentNameByIdCommandText;

                        var prm_id = cmd.CreateParameter();
                        prm_id.ParameterName = "id";
                        prm_id.DbType = DbType.Int32;
                        prm_id.Value = id;
                        cmd.Parameters.Add(prm_id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            List<string> result = new List<string>();
                            while (reader.Read())
                            {
                                result.Add(reader.GetString(0));
                            }

                            return result;
                        }
                    }
                }

            }

            private const string GetAgentInfoByIdCommandText = "SELECT name, fullname, description, active FROM hlsysagent WHERE agentid=@id";
            public static IEnumerable<string> GetAgentInfoById(IDatabaseAccessorFactory databaseAccessorFactory, int id)
            {
                using (DbConnection connection = databaseAccessorFactory.CreateConnection())
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = GetAgentInfoByIdCommandText;

                        var prm_id = cmd.CreateParameter();
                        prm_id.ParameterName = "id";
                        prm_id.DbType = DbType.Int32;
                        prm_id.Value = id;
                        cmd.Parameters.Add(prm_id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            List<string> result = new List<string>();
                            while (reader.Read())
                            {
                                result.Add(reader.GetString(0));
                                reader.GetInt16(3);
                            }

                            return result;
                        }
                    }
                }

            }
        }
    }
}
