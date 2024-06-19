using Helpline.Data.TestStore; // generated assembly
using Helpline.SLM.Database.Data.TestStore; // generated assembly
using StoreLake.TestStore.Server;
using StoreLake.TestStore;
using StoreLake.TestStore.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace StoreLake.Test.ConsoleApp
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

        //private static Helpline.Data.IntThreeSetRow[] read_Udt(DbCommand cmd, string name)
        //{
        //    //System.Linq.Enumerable.Select()
        //    IDataRecord[] records = System.Linq.Enumerable.ToArray((IEnumerable<IDataRecord>)cmd.Parameters[name]);

        //    Helpline.Data.IntThreeSetRow[] rows = new Helpline.Data.IntThreeSetRow[records.Length];
        //    for (int ix = 0; ix < records.Length; ix++)
        //    {
        //        rows[ix] = new Helpline.Data.IntThreeSetRow(records[ix]);
        //    }
        //    return rows;
        //}

        private static void Test01()
        {
            var db = StoreLakeDatabaseServer.CreateDatabase("MyDB")
                .Use(DatabaseGetUtcDateExtension.Register)
                .Use(HelplineDataExtensions.RegisterDataSetModel) // using 'Helpline.Data.TestStore.HelplineDataExtensions'
                                                                  //.Use(HelplineDataExtensions_X.RegisterDataSetModel)
                .Use(SLMDatabaseDataExtensions.RegisterDataSetModel) // using Helpline.SLM.Database.Data.TestStore.SLMDatabaseDataExtensions
                .Use(DatabaseGetUtcDateExtension.Register)
                .Use(TweetExtension.Register)
                .Build()
                .SetCommandExecuteHandlerInstanceForHelplineDataProceduresFacade<DemoHandler4_FacadeHandler>();
            //.SetHandlerForHelplineDataProcedures<DemoHandler4_CommandHandler>()
            //.SetCommandExecuteHandlerInstanceForHelplineDataProceduresFacade<DemoHandler4>() // SetFacade
            //.SetCommandExecuteHandlerInstanceForHelplineDataProceduresHandler<DemoHandler4_CommandHandler>()
            ;
            //HelplineDataExtensions.SetCommandExecuteHandlerInstanceForHelplineDataProceduresHandler<DataSet, DemoHandler4_CommandHandler>(db);
            //HelplineDataExtensions.SetCommandExecuteHandlerInstanceForHelplineDataProceduresFacade<DataSet, DemoHandler4_FacadeHandler>(db);

            Console.WriteLine("timenow:" + db.GetUtcDate());

            // [SomeComplicatedMultipleResultSetProc]

            var hlsysagent = db.hlsysagent();
            var agent710 = db.hlsysagent().AddRowWithValues(agentid: 710, name: "InternetAgent", fullname: null, description: null, active: 1); //  see 'DF_hlsysagent_active'

            //agent1.agentid = 710;
            agent710.description = agent710.name + ":" + agent710.agentid;

            // NULL defaults?
            // DEFAULT values?
            // UNIQUE Key?
            // AutoIncrement?
            // ReadOnlyPK
            var agent2 = db.hlsysagent().AddRowWithValues(712, "InternetAgent2", null, null, 1); //e.active = 1; see 'DF_hlsysagent_active'
            //db.hlsysagentroutingblacklist().AddRowWithValues(712);

            var group700 = db.hlsysgroup().AddRowWithValues(700, "Administrators");

            var global_123 = db.hlsysglobalpolicy().AddRowWithValues(123, "g:123", 11223);

            db.hlsysagenttogroup().AddRowWithValues(agent710.agentid, 700);
            db.hlsysglobalacl().AddRowWithValues(123, 700, 0x0010);

            Console.WriteLine("db.hlsysagent.Count:" + db.hlsysagent().Count);

            StoreLakeDbServer dbServer = new StoreLakeDbServer(db);

            dbServer.RegisterProcedureHandlers<HelplineDataProceduresCommandExecuteHandler>(x => x.HelplineDataProceduresHandler()
                , HelplineDataProceduresCommandExecuteHandler.TryGetHandlerForCommandExecuteProcedureNonQuery
                , HelplineDataProceduresCommandExecuteHandler.TryGetHandlerForCommandExecuteProcedureQuery);

            // on reflection/discovery (methodname=procedurename) dbServer.RegisterAddedCommandHandlerContract(db, db.HelplineDataProceduresHandler());
            dbServer.RegisterCommandHandlerMethods(typeof(TestDML), typeof(DemoHandler1)); // dml.methods(+text) => handler(+text)

            StoreLakeDabaseAccessorGate accessorGate = new StoreLakeDabaseAccessorGate();

            DbProviderFactory dbClient = dbServer.CreateDbProviderFactoryInstance();

            xDatabaseAccessorFactory databaseAccessorFactory = new xDatabaseAccessorFactory(dbServer, accessorGate, dbClient, "Initial Catalog=MyDB");

            /* "accessor.QueryMany<TReturn,TSecond>"
            var test12 = Helpline.Data.HelplineData.GetAttributesOfCmdbFlows(databaseAccessorFactory);
            */

            //var test11 = Helpline.Repository.Data.HelplineData.GetUserInfo(databaseAccessorFactory, agent710.agentid);
            //Console.WriteLine("Test11:  UserInfo.Agents : Count = " + test11.Agents.Count);
            //foreach (var agent in test11.Agents)
            //{
            //    Console.WriteLine("    " + agent.Id + ", " + agent.Name + ", HideForRouting=" + agent.HideForRouting);
            //}

            db.hlsysobjectbasetype().AddRowWithValues(2, "p");
            var object_def_A = db.hlsysobjectdef().AddRowWithValues(101, "OD_A", 2);
            var object_def_B = db.hlsysobjectdef().AddRowWithValues(102, "OD_B", 2);
            var object_def_C = db.hlsysobjectdef().AddRowWithValues(103, "OD_C", 2);
            var case_def_A = db.hlsyscasedef().AddRowWithValues(object_def_A.objectdefid);
            var case_def_B = db.hlsyscasedef().AddRowWithValues(object_def_B.objectdefid);
            var case_def_C = db.hlsyscasedef().AddRowWithValues(object_def_C.objectdefid);

            var test10_udt = new Helpline.Data.IntThreeSet();//.From(new int[] { 3, 2, 1 }, (udt, item) => udt.Add(item, (100 * item) + item, (100 * item)));
            test10_udt.Add(1, object_def_A.objectdefid, 3000); // seq, def, objectid
            test10_udt.Add(2, object_def_B.objectdefid, 2000);
            test10_udt.Add(3, object_def_C.objectdefid, 1000);
            Helpline.Data.HelplineData.AddToWatchList(databaseAccessorFactory, agent710.agentid, test10_udt, out bool test10);
            Console.WriteLine("test10: " + test10);

            Helpline.Data.HelplineData.AdministrationRefreshRelationModels(databaseAccessorFactory);

            var test8 = Helpline.Data.HelplineData.CanExecute(databaseAccessorFactory, agent710.agentid, 123);
            Console.WriteLine("test8: " + test8);

            var test7 = TestDML.GetAllAgentIdentities(databaseAccessorFactory);
            foreach (var row in test7)
                Console.WriteLine(row);

            var test6 = TestDML.GetAllAgentIds(databaseAccessorFactory);
            foreach (var row in test6)
                Console.WriteLine(row);

            var test5 = TestDML.GetAllAgentInfos(databaseAccessorFactory);
            foreach (var row in test5)
                Console.WriteLine(row.Id + " | " + row.Name + " | " + row.IsActive);

            var name = TestDML.GetAgentNameById(databaseAccessorFactory, 712).Single();
            Console.WriteLine(name);
            var fname = TestDML.GetAgentInfoById(databaseAccessorFactory, 712).Single();
            Console.WriteLine(fname);
            var desc = TestDML.GetAgentsDescriptionById(databaseAccessorFactory, 712);
            Console.WriteLine("GetAgentsDescriptionById:" + desc);

            var test4 = TestDML.GetAllAgentNames(databaseAccessorFactory);
            foreach (var row in test4)
                Console.WriteLine(row);
        }
    }


    public sealed class AgentInfo
    {
        public int Id;
        public string Name;
        public bool IsActive;
    }
    public static class AdoClient
    {
        public static void SomeComplicatedMultipleResultSetProc(DbProviderFactory dbClient)
        {
            using (var connection = dbClient.CreateConnection())
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "dbo.SomeComplicatedMultipleResultSetProc";
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {

                        }
                    }
                }
            }

        }
    }
    public static class TestDML
    {
        private const string GetAgentNameByIdCommandText = "SELECT name FROM hlsysagent WHERE agentid=@id";
        public static IEnumerable<string> GetAgentNameById(xDatabaseAccessorFactory databaseAccessorFactory, int id)
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
        public static IEnumerable<string> GetAgentInfoById(xDatabaseAccessorFactory databaseAccessorFactory, int id)
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
        private const string GetAgentDescriptionByIdCommandText = "SELECT description FROM hlsysagent WHERE agentid=@id";
        public static IEnumerable<string> GetAgentDescriptionById(xDatabaseAccessorFactory databaseAccessorFactory, int id)
        {
            using (DbConnection connection = databaseAccessorFactory.CreateConnection())
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = GetAgentDescriptionByIdCommandText;

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

        private const string GetAgentsDescriptionByIdCommandText = "SELECT description FROM dbo.hlsysagent WHERE agentid=@id";
        public static string GetAgentsDescriptionById(xDatabaseAccessorFactory databaseAccessorFactory, int id)
        {
            using (DbConnection connection = databaseAccessorFactory.CreateConnection())
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = GetAgentsDescriptionByIdCommandText;

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

                        return result.FirstOrDefault();
                    }
                }
            }

        }

        private const string GetAllAgentNamesCommandText = "SELECT name FROM dbo.hlsysagent";
        public static IEnumerable<string> GetAllAgentNames(xDatabaseAccessorFactory databaseAccessorFactory)
        {
            using (DbConnection connection = databaseAccessorFactory.CreateConnection())
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = GetAllAgentNamesCommandText;

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

        private const string GetAllAgentInfosCommandText = "SELECT id = agentid, name, isactive = CAST(ISNULL(active,0) AS BIT) FROM dbo.hlsysagent";

        public static IEnumerable<AgentInfo> GetAllAgentInfos(xDatabaseAccessorFactory databaseAccessorFactory)
        {
            using (DbConnection connection = databaseAccessorFactory.CreateConnection())
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = GetAllAgentInfosCommandText;

                    using (var reader = cmd.ExecuteReader())
                    {
                        int pos_Id = reader.GetOrdinal("Id");
                        int pos_Name = reader.GetOrdinal("Name");
                        int pos_IsActive = reader.GetOrdinal("IsActive");

                        List<AgentInfo> result = new List<AgentInfo>();
                        while (reader.Read())
                        {
                            var row = new AgentInfo();
                            object v_0 = reader.GetValue(0);
                            row.Id = reader.GetInt32(pos_Id);
                            row.Name = reader.GetString(pos_Name);
                            row.IsActive = reader.GetBoolean(pos_IsActive);
                            result.Add(row);
                        }

                        return result;
                    }
                }
            }

        }

        private const string GetAllAgentIdsCommandText = "SELECT id FROM dbo.hlsysagent";

        public static IEnumerable<int> GetAllAgentIds(xDatabaseAccessorFactory databaseAccessorFactory)
        {
            using (DbConnection connection = databaseAccessorFactory.CreateConnection())
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = GetAllAgentIdsCommandText;

                    using (var reader = cmd.ExecuteReader())
                    {
                        List<int> result = new List<int>();
                        while (reader.Read())
                        {
                            result.Add(reader.GetInt32(0));
                        }

                        return result;
                    }
                }
            }
        }

        private const string GetAllAgentIdentitiesCommandText = "SELECT id FROM dbo.hlsysagent";

        public static IEnumerable<int> GetAllAgentIdentities(Dibix.IDatabaseAccessorFactory databaseAccessorFactory)
        {
            using (Dibix.IDatabaseAccessor accessor = databaseAccessorFactory.Create())
            {
                Dibix.ParametersVisitor @params = accessor.Parameters().SetFromTemplate(new
                {
                }).Build();
                return accessor.QueryMany<int>(GetAllAgentIdentitiesCommandText, System.Data.CommandType.Text, @params).ToArray();
            }
        }
    }

}
