using Dibix.TestStore.Database;
using SLM.Database.Data;
using System;
using System.Data;

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
                .Use(SLM.Database.Data.NewDataSetExtensions.RegisterDataSetModel)
                .Use(DatabaseGetUtcDateExtension.Register)
                .Build();

            Console.WriteLine("timenow:" + db.GetUtcDate());
            
            var hlsysagent = db.hlsysagent();
            var agent1 = db.hlsysagent().AddRowWithValues(712, "InternetAgent", null, null, 1); //  see 'DF_hlsysagent_active'

            agent1.agentid = 710;
            agent1.description = agent1.name + ":" + agent1.agentid;

            // NULL defaults?
            // DEFAULT values?
            var agent2 = db.hlsysagent().AddRowWithValues(712, "InternetAgent", null, null, 1); //e.active = 1; see 'DF_hlsysagent_active'

            Console.WriteLine("db.hlsysagent.Count:" + db.hlsysagent().Count);
        }
    }
}
