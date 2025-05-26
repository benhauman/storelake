namespace StoreLake.Sdk.Cli
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using StoreLake.Sdk.CodeGeneration;

    internal class Program
    {
        private static readonly TraceSource s_tracer = SchemaExportCode.CreateTraceSource();

        private static int Main(string[] args)
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
                ToolArguments targs = StoreLakeSdkTool.ParseArguments(args);
                if (targs == null)
                {
                    s_tracer.TraceEvent(TraceEventType.Information, 0, "USAGE:");
                    s_tracer.TraceEvent(TraceEventType.Information, 0, " StoreLake.Sdk.Cli.exe inputdir=... dacpac=sample.dacpac");
                    return -2;
                }
                else
                {
                    StoreLakeSdkTool.Run(targs);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                DumpException(ex);
                return -1;
            }
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
    }
}
