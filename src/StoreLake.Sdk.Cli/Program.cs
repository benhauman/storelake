using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using StoreLake.Sdk.CodeGeneration;

namespace StoreLake.Sdk.Cli
{
    class Program
    {
        private static readonly TraceSource s_tracer = SchemaExportCode.CreateTraceSource();

        static int Main(string[] args)
        {
            ConsoleTraceListener listener = new ColoredConsoleTraceListener(false);
            Trace.Listeners.Clear();
            Trace.Listeners.Add(listener); // SharedListeners
            s_tracer.Listeners.Add(listener);

            //s_tracer.Listeners.Add(listener);

            s_tracer.TraceEvent(TraceEventType.Information, 0, "CurrentDirectory: " + Environment.CurrentDirectory);
            s_tracer.TraceEvent(TraceEventType.Information, 0, "FileVersion:" + typeof(Program).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version);
            try
            {
                ToolArguments targs = ParseArguments(args);
                if (targs == null)
                {
                    s_tracer.TraceEvent(TraceEventType.Information, 0, "USAGE:");
                    s_tracer.TraceEvent(TraceEventType.Information, 0, " StoreLake.Sdk.Cli.exe inputdir=... dacpac=sample.dacpac");
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
                s_tracer.TraceEvent(TraceEventType.Error, 0, "No arguments specified.");
                return null;
            }
            ToolArguments targs = new ToolArguments();
            foreach (string arg in args)
            {
                //Console.WriteLine(arg);
                string[] kv = arg.Split('=');
                if (kv.Length != 2)
                {
                    s_tracer.TraceEvent(TraceEventType.Error, 0, "Invalid argument specified:" + arg);
                    return null;
                }
                if (string.IsNullOrEmpty(kv[0]))
                {
                    s_tracer.TraceEvent(TraceEventType.Error, 0, "Invalid argument key specified:" + arg);
                    return null;
                }
                if (string.IsNullOrEmpty(kv[1]))
                {
                    s_tracer.TraceEvent(TraceEventType.Error, 0, "Invalid argument value specified:" + arg);
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
                                            s_tracer.TraceEvent(TraceEventType.Error, 0, "Unknown argument specified:" + arg);
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
            s_tracer.TraceEvent(TraceEventType.Error, 0, "" + ex);
            if (ex.InnerException != null)
            {
                DumpException(ex.InnerException);
            }
            ReflectionTypeLoadException rtlex = ex as ReflectionTypeLoadException;
            if (rtlex != null)
            {
                s_tracer.TraceEvent(TraceEventType.Error, 0, "LoaderExceptions:" + rtlex.LoaderExceptions.Length);
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

            s_tracer.TraceInformation("InputDirectory=" + targs.InputDirectory);
            s_tracer.TraceInformation("OutputDirectory=" + targs.OutputDirectory);
            s_tracer.TraceInformation("LibraryDirectory=" + targs.LibraryDirectory);
            s_tracer.TraceInformation("DacpacFileName=" + targs.DacpacFileName);
            s_tracer.TraceInformation("StoreNameAssemblySuffix=" + targs.StoreNameAssemblySuffix);
            s_tracer.TraceInformation("GenerateSchema=" + targs.StoreNameAssemblySuffix);
            s_tracer.TraceInformation("TempDirectory=" + targs.TempDirectory);

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
