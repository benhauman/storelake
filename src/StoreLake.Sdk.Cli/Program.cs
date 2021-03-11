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

            string dacpacFileName = "Helpline.dacpac";
            string dacpacFullFileName = System.IO.Path.Combine(inputdir, dacpacFileName);
            var ds = SchemaImportDacPac.ImportDacPac(databaseName, dacpacFullFileName);

            string filter = null;
            //filter = "HelplineData";
            //filter = "SLM.Database.Data";
            SchemaExportCode.ExportTypedDataSetCode(ds, inputdir, filter);// "HelplineData");
        }
    }
}
