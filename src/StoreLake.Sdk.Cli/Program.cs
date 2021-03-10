using System;

using Dibix.TestStore.Database;

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
            string databaseName = "DemoTestDataX";

            string inputdir = @"D:\Helpline\Current\bin\Debug\output\database";
            //string dacName = "Helpline";
            string dacName = "SLM.Database.Data";

            string dacpacFileName = dacName + ".dacpac";
            string dacpacFullFileName = System.IO.Path.Combine(inputdir, dacpacFileName);
            var ds = SchemaImportDacPac.ImportDacPac(databaseName, dacpacFullFileName);

            SchemaExportCode.ExportTypedDataSetCode(ds, inputdir, dacName);
        }
    }
}
