using System;
using System.Diagnostics;
using System.IO;

namespace StoreLake.Sdk.CodeGeneration
{

    public sealed class ToolArguments
    {
        public string InputDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public string LibraryDirectory { get; set; }
        public string DacpacFileName { get; set; }
        public string StoreNameAssemblySuffix { get; set; }
        public bool? GenerateSchema { get; set; }
        public string TempDirectory { get; set; }
        public bool ForceReferencePackageRegeneration { get; set; }

        public bool GenerateMissingReferences { get; set; }
    }
    public static class StoreLakeSdkTool
    {
        private static readonly TraceSource s_tracer = SchemaExportCode.CreateTraceSource();
        private static string ExpandPath(string dir)
        {
            if (dir.Contains("..") || dir.StartsWith(".\\"))
            {
                return Path.GetFullPath(dir);
            }

            return Path.GetFullPath(dir);
        }


        public static ToolArguments ParseArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                s_tracer.TraceEvent(TraceEventType.Error, 0, "No arguments specified.");
                return null;
            }
            s_tracer.TraceEvent(TraceEventType.Information, 0, "Arguments count:" + args.Length);
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
                                            if (string.Equals(kv[0], "regeneratereferences", StringComparison.Ordinal))
                                            {
                                                targs.ForceReferencePackageRegeneration = bool.Parse(kv[1]);
                                            }
                                            else
                                            {
                                                if (string.Equals(kv[0], "generatemissingreferences", StringComparison.Ordinal))
                                                {
                                                    targs.GenerateMissingReferences = bool.Parse(kv[1]);
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
                }

            }

            return targs;
        }


        public static void Run(ToolArguments targs)
        {
            //TSqlFragment
            //Microsoft.SqlServer.Server.SqlUserDefinedTypeAttribute
            if (targs == null)
            {
                throw new StoreLakeSdkException("Null argument specified:" + nameof(targs));
            }

            if (string.IsNullOrEmpty(targs.InputDirectory))
            {
                throw new StoreLakeSdkException("'InputDirectory' is not specified");
            }
            if (string.IsNullOrEmpty(targs.OutputDirectory))
            {
                throw new StoreLakeSdkException("'OutputDirectory' is not specified");
            }
            if (string.IsNullOrEmpty(targs.LibraryDirectory))
            {
                throw new StoreLakeSdkException("'LibraryDirectory' is not specified");
            }

            if (string.IsNullOrEmpty(targs.DacpacFileName))
            {
                throw new StoreLakeSdkException("'DacpacFileName' is not specified");
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
            s_tracer.TraceInformation("GenerateSchema=" + targs.GenerateSchema);
            s_tracer.TraceInformation("TempDirectory=" + targs.TempDirectory);
            s_tracer.TraceInformation("ForceReferencePackageRegeneration=" + targs.ForceReferencePackageRegeneration);
            s_tracer.TraceInformation("GenerateMissingReferences=" + targs.GenerateMissingReferences);

            AssemblyResolver assemblyResolver = new AssemblyResolver(targs.LibraryDirectory);
            AppDomain.CurrentDomain.AssemblyResolve += assemblyResolver.OnAssemblyResolve;
            try
            {
                string dacpacFullFileName = System.IO.Path.Combine(targs.InputDirectory, targs.DacpacFileName);
                var rr = SchemaImportDacPac.ImportDacPac(targs.InputDirectory, dacpacFullFileName, targs.ForceReferencePackageRegeneration, targs.GenerateMissingReferences);

                string filter = null;
                //filter = "HelplineData";
                //filter = "SLM.Database.Data";
                SchemaExportCode.ExportTypedDataSetCode(assemblyResolver, rr, targs.LibraryDirectory, targs.InputDirectory, targs.OutputDirectory, filter, targs.StoreNameAssemblySuffix, targs.GenerateSchema.Value, targs.TempDirectory);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve += assemblyResolver.OnAssemblyResolve;
            }
        }

    }
}

