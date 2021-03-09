using Dibix.TestStore.Demo;
using Dibix.TestStore.Database;
using System;
using System.Data;

namespace ConsoleApp3
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Test01();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return -1;
            }
        }

        private static void Test01()
        {
            string databaseName = "DemoTestData";
            var ds = new DataSet(databaseName) { Namespace = "[dbo]" }; // see 'https://www.codeproject.com/articles/30490/how-to-manually-create-a-typed-datatable'

            string inputdir = @"D:\Helpline\Current\bin\Debug\output\database";
            //string dacName = "Helpline";
            string dacName = "SLM.Database.Data";

            string dacpacFileName = dacName + ".dacpac";
            string dacpacFullFileName = System.IO.Path.Combine(inputdir, dacpacFileName);
            SchemaImportDacPac.ImportDacPac(ds, dacpacFullFileName);

            SchemaExportCode.ExportTypedDataSetCode(ds, inputdir, dacName);
        }
    }
}
