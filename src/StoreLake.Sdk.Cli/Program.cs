using System;
using System.IO;
using System.Reflection;
using StoreLake.Sdk.CodeGeneration;

namespace StoreLake.Sdk.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("CurrentDirectory: " + Environment.CurrentDirectory);
            Console.WriteLine("FileVersion:" + typeof(Program).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version);
            
            try
            {
                ToolArguments targs = ParseArguments(args);
                if (targs == null)
                {
                    Console.Error.WriteLine("USAGE:");
                    Console.Error.WriteLine(" StoreLake.Sdk.Cli.exe inputdir=... dacpac=sample.dacpac");
                    return -2;
                }
                else
                {
                    Test01(targs);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                DumpException(ex);
                return -1;
            }
        }

        private sealed class ToolArguments
        {
            public string InputDirectory { get; set; }
            public string OutputDirectory { get; set; }
            public string LibraryDirectory { get; set; }
            public string DacpacFileName { get; set; }
            public string StoreNameAssemblySuffix { get; set; }
            public bool? GenerateSchema { get; set; }
            public string TempDirectory { get; set; }
        }

        private static ToolArguments ParseArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Console.Error.WriteLine("No arguments specified.");
                return null;
            }
            ToolArguments targs = new ToolArguments();
            foreach (string arg in args)
            {
                Console.WriteLine(arg);
                string[] kv = arg.Split('=');
                if (kv.Length != 2)
                {
                    Console.Error.WriteLine("Invalid argument specified:" + arg);
                    return null;
                }
                if (string.IsNullOrEmpty(kv[0]))
                {
                    Console.Error.WriteLine("Invalid argument key specified:" + arg);
                    return null;
                }
                if (string.IsNullOrEmpty(kv[1]))
                {
                    Console.Error.WriteLine("Invalid argument value specified:" + arg);
                    return null;
                }

                if (string.Equals(kv[0], "inputdir", StringComparison.Ordinal))
                {
                    targs.InputDirectory = kv[1];
                }
                else
                {
                    if (string.Equals(kv[0], "libdir", StringComparison.Ordinal))
                    {
                        targs.LibraryDirectory = kv[1];
                    }
                    else
                    {
                        if (string.Equals(kv[0], "dacpac", StringComparison.Ordinal))
                        {
                            targs.DacpacFileName = kv[1];
                        }
                        else
                        {
                            if (string.Equals(kv[0], "outputdir", StringComparison.Ordinal))
                            {
                                targs.OutputDirectory = kv[1];
                            }
                            else
                            {
                                if (string.Equals(kv[0], "storesuffix", StringComparison.Ordinal))
                                {
                                    targs.StoreNameAssemblySuffix = kv[1];
                                }
                                else
                                {
                                    if (string.Equals(kv[0], "generateschema", StringComparison.Ordinal))
                                    {
                                        targs.GenerateSchema = bool.Parse(kv[1]);
                                    }
                                    else
                                    {
                                        if (string.Equals(kv[0], "tempdir", StringComparison.Ordinal))
                                        {
                                            targs.TempDirectory = kv[1];
                                        }
                                        else
                                        {
                                            Console.Error.WriteLine("Unknown argument specified:" + arg);
                                            return null;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

            }

            return targs;
        }

        private static void DumpException(Exception ex)
        {
            Console.Error.WriteLine(ex);
            if (ex.InnerException != null)
            {
                DumpException(ex.InnerException);
            }
            ReflectionTypeLoadException rtlex = ex as ReflectionTypeLoadException;
            if (rtlex != null)
            {
                Console.Error.WriteLine("LoaderExceptions:" + rtlex.LoaderExceptions.Length);
                foreach (var err in rtlex.LoaderExceptions)
                {
                    DumpException(err);
                }
            }
        }

        private static void Test01(ToolArguments targs)
        {
            if (string.IsNullOrEmpty(targs.InputDirectory))
            {
                throw new InvalidOperationException("'InputDirectory' is not specified");
            }
            if (string.IsNullOrEmpty(targs.OutputDirectory))
            {
                throw new InvalidOperationException("'OutputDirectory' is not specified");
            }
            if (string.IsNullOrEmpty(targs.LibraryDirectory))
            {
                throw new InvalidOperationException("'LibraryDirectory' is not specified");
            }

            if (string.IsNullOrEmpty(targs.DacpacFileName))
            {
                throw new InvalidOperationException("'DacpacFileName' is not specified");
            }
            if (string.IsNullOrEmpty(targs.StoreNameAssemblySuffix))
            {
                targs.StoreNameAssemblySuffix = "DataStore";
            }
            if (!targs.GenerateSchema.HasValue)
            {
                targs.GenerateSchema = true;
            }
            if (string.IsNullOrEmpty(targs.TempDirectory))
            {
                targs.TempDirectory = Path.Combine(targs.OutputDirectory, "TempFiles");
            }
            targs.InputDirectory = ExpandPath(targs.InputDirectory);
            targs.OutputDirectory = ExpandPath(targs.OutputDirectory);
            targs.LibraryDirectory = ExpandPath(targs.LibraryDirectory);
            targs.TempDirectory = ExpandPath(targs.TempDirectory);

            Console.WriteLine("InputDirectory=" + targs.InputDirectory);
            Console.WriteLine("OutputDirectory=" + targs.OutputDirectory);
            Console.WriteLine("LibraryDirectory=" + targs.LibraryDirectory);
            Console.WriteLine("DacpacFileName=" + targs.DacpacFileName);
            Console.WriteLine("StoreNameAssemblySuffix=" + targs.StoreNameAssemblySuffix);
            Console.WriteLine("GenerateSchema=" + targs.StoreNameAssemblySuffix);
            Console.WriteLine("TempDirectory=" + targs.TempDirectory);

            ////string databaseName = "DemoTestDataX";
            //string databaseName = targs.DatabaseName;

            //string inputdir = @"D:\Helpline\Current\bin\Debug\output\database";
            string inputdir = targs.InputDirectory;
            //StoreAccessorCodeGenerator.Initialize(inputdir);

            //string dacpacFileName = "Helpline.dacpac";
            //dacpacFileName = "SLM.Database.Data.dacpac";
            //dacpacFileName = "NewsManagement.Database.dacpac";
            //string dacpacFileName = targs.DacpacFileName;
            string dacpacFullFileName = System.IO.Path.Combine(inputdir, targs.DacpacFileName);
            var ds = SchemaImportDacPac.ImportDacPac(inputdir, dacpacFullFileName);

            string filter = null;
            //filter = "HelplineData";
            //filter = "SLM.Database.Data";
            SchemaExportCode.ExportTypedDataSetCode(ds, targs.LibraryDirectory, inputdir, targs.OutputDirectory, filter, targs.StoreNameAssemblySuffix, targs.GenerateSchema.Value, targs.TempDirectory);
        }

        private static string ExpandPath(string dir)
        {
            if (dir.Contains("..") || dir.StartsWith(".\\"))
            {
                return Path.GetFullPath(dir);
            }

            return Path.GetFullPath(dir);
        }
    }
}
