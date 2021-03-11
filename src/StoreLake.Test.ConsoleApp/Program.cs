using Dibix.TestStore.Database;
using Helpline.Data.TestStore;
using Helpline.SLM.Database.Data.TestStore;
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
            var agent2 = db.hlsysagent().AddRowWithValues(712,  "InternetAgent2", null, null, 1); //e.active = 1; see 'DF_hlsysagent_active'

            Console.WriteLine("db.hlsysagent.Count:" + db.hlsysagent().Count);
        }
    }
}
