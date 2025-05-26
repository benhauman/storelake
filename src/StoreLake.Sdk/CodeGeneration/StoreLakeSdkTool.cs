namespace StoreLake.Sdk.CodeGeneration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Microsoft.SqlServer.TransactSql.ScriptDom;

    public sealed class ToolArguments
    {
        public string InputDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public string[] LibraryDirectories { get; set; }
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
                        if (kv[1] != null)
                        {
                            targs.LibraryDirectories = kv[1].Split(';');
                        }
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
            EnsureScriptDom();
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
            if ((targs.LibraryDirectories == null) || (targs.LibraryDirectories.Length == 0))
            {
                throw new StoreLakeSdkException("'LibraryDirectories' is not specified");
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
            targs.TempDirectory = ExpandPath(targs.TempDirectory);
            targs.LibraryDirectories = targs.LibraryDirectories.Select(x => ExpandPath(x)).ToArray();

            s_tracer.TraceInformation("InputDirectory=" + targs.InputDirectory);
            s_tracer.TraceInformation("OutputDirectory=" + targs.OutputDirectory);
            s_tracer.TraceInformation("LibraryDirectories=" + string.Join(";", targs.LibraryDirectories));
            s_tracer.TraceInformation("DacpacFileName=" + targs.DacpacFileName);
            s_tracer.TraceInformation("StoreNameAssemblySuffix=" + targs.StoreNameAssemblySuffix);
            s_tracer.TraceInformation("GenerateSchema=" + targs.GenerateSchema);
            s_tracer.TraceInformation("TempDirectory=" + targs.TempDirectory);
            s_tracer.TraceInformation("ForceReferencePackageRegeneration=" + targs.ForceReferencePackageRegeneration);
            s_tracer.TraceInformation("GenerateMissingReferences=" + targs.GenerateMissingReferences);

            AssemblyResolver assemblyResolver = new AssemblyResolver(targs.LibraryDirectories, targs.OutputDirectory);
            AppDomain.CurrentDomain.AssemblyResolve += assemblyResolver.OnAssemblyResolve;
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += assemblyResolver.OnReflectionOnlyAssemblyResolve;
            try
            {
                string dacpacFullFileName = System.IO.Path.Combine(targs.InputDirectory, targs.DacpacFileName);
                var rr = SchemaImportDacPac.ImportDacPac(targs.InputDirectory, dacpacFullFileName, targs.ForceReferencePackageRegeneration, targs.GenerateMissingReferences);

                string filter = null;
                //filter = "HelplineData";
                //filter = "SLM.Database.Data";
                SchemaExportCode.ExportTypedDataSetCode(assemblyResolver, rr, targs.LibraryDirectories, targs.InputDirectory, targs.OutputDirectory, filter, targs.StoreNameAssemblySuffix, targs.GenerateSchema.Value, targs.TempDirectory);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolver.OnAssemblyResolve;
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= assemblyResolver.OnReflectionOnlyAssemblyResolve;
            }
        }

        private static void EnsureScriptDom()
        {
            string check_definition = "([ismultiple]=(1))";
            string sql_statement = "SELECT colx = IIF((" + check_definition + "), 1, 0)";
            TSqlParser parser = new TSql140Parser(true);
            using (var reader = new StringReader(sql_statement))
            {
                TSqlFragment fragment = parser.Parse(reader, out IList<ParseError> errors);
                if (errors.Any())
                {
                    throw new InvalidOperationException($@"Error parsing SQL statement
{String.Join(Environment.NewLine, errors.Select(x => $"{x.Message} at {x.Line},{x.Column}"))}");
                }

                //Console.WriteLine("SQL Fragment generated!");
                SqlScriptGenerator generator = new Sql140ScriptGenerator();
                generator.GenerateScript(fragment, out string output_sql);
                //Console.WriteLine(output_sql);
                //Console.WriteLine("SQL Parser prepared.");
            }
        }
    }
}
