using System;
using System.Reflection;
using StoreLake.Sdk.CodeGeneration;

namespace StoreLake.Sdk.Cli
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
                DumpException(ex);
                return -1;
            }
        }

        private static void DumpException(Exception ex)
        {
            Console.WriteLine(ex);
            if (ex.InnerException != null)
            {
                DumpException(ex.InnerException);
            }
            ReflectionTypeLoadException rtlex = ex as ReflectionTypeLoadException;
            if (rtlex != null)
            {
                Console.WriteLine("LoaderExceptions:" + rtlex.LoaderExceptions.Length);
                foreach (var err in rtlex.LoaderExceptions)
                {
                    DumpException(err);
                }
            }
        }

        private static void Test01()
        {
            string databaseName = "DemoTestDataX";

            string inputdir = @"D:\Helpline\Current\bin\Debug\output\database";
            //StoreAccessorCodeGenerator.Initialize(inputdir);

            string dacpacFileName = "Helpline.dacpac";
            //dacpacFileName = "SLM.Database.Data.dacpac";
            //dacpacFileName = "NewsManagement.Database.dacpac";
            string dacpacFullFileName = System.IO.Path.Combine(inputdir, dacpacFileName);
            var ds = SchemaImportDacPac.ImportDacPac(databaseName, inputdir, dacpacFullFileName);

            string filter = null;
            //filter = "HelplineData";
            //filter = "SLM.Database.Data";
            SchemaExportCode.ExportTypedDataSetCode(ds, inputdir, inputdir, filter);// "HelplineData");
        }
    }
}
