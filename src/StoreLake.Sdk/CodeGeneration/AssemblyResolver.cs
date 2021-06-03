using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace StoreLake.Sdk.CodeGeneration
{
    internal sealed class AssemblyResolver
    {
        private static readonly TraceSource s_tracer = SchemaExportCode.CreateTraceSource();
        private readonly string _libdir;
        private readonly string _outputdir;
        public AssemblyResolver(string libdir, string outputdir)
        {
            _libdir = libdir ?? string.Empty;
            _outputdir = outputdir ?? string.Empty;
        }
        internal System.Reflection.Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            s_tracer.TraceInformation("OnAssemblyResolve : " + args.Name);

            AssemblyName asmName = new AssemblyName(args.Name); // Dibix.Http.Server, Version=1.0.0.0, Culture=neutral, PublicKeyToken=5b039a7bf8dc383e
            string fileName = Path.Combine(_libdir, asmName.Name + ".dll");
            if (File.Exists(fileName))
            {
                s_tracer.TraceInformation("OnAssemblyResolve (load) : " + fileName);
                return Assembly.Load(AssemblyName.GetAssemblyName(fileName));
            }
            return null;
        }
        internal System.Reflection.Assembly OnReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            return OnReflectionOnlyAssemblyResolveImpl(args.Name);
        }
        private System.Reflection.Assembly OnReflectionOnlyAssemblyResolveImpl(string args_Name)
        {
            s_tracer.TraceInformation("OnReflectionOnlyAssemblyResolve : " + args_Name);

            AssemblyName asmName = new AssemblyName(args_Name); // Dibix.Http.Server, Version=1.0.0.0, Culture=neutral, PublicKeyToken=5b039a7bf8dc383e
            string fileName = Path.Combine(_libdir, asmName.Name + ".dll");
            if (File.Exists(fileName))
            {
                s_tracer.TraceInformation("OnReflectionOnlyAssemblyResolve (load) : " + fileName);
                return Assembly.ReflectionOnlyLoadFrom(fileName);
            }

            fileName = Path.Combine(_outputdir, asmName.Name + ".dll");
            if (File.Exists(fileName))
            {
                s_tracer.TraceInformation("OnReflectionOnlyAssemblyResolve (load) : " + fileName);
                return Assembly.ReflectionOnlyLoadFrom(fileName);
            }
            return null;
        }

        private readonly IDictionary<string, string> name_location = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly IDictionary<string, string> location_name = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly IDictionary<string, Assembly> name_asm = new SortedDictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        internal Assembly ResolveAssembyByLocation(string dacpacDllFullFileName)
        {
            if (location_name.TryGetValue(dacpacDllFullFileName, out string cached_name))
            {
                if (name_asm.TryGetValue(cached_name, out Assembly cached_asm))
                {
                    return cached_asm;
                }
            }
            AssemblyName asmName = AssemblyName.GetAssemblyName(dacpacDllFullFileName);
            Assembly asm = OnReflectionOnlyAssemblyResolveImpl(asmName.ToString());
            if (asm == null)
            {
                asm = Assembly.ReflectionOnlyLoad(asmName.ToString());// Assembly.Load(asmName); // ReflectionOnly?
            }

            return CacheAssembly(asmName, asm);
        }
        internal string ResolveLocationByName(AssemblyName assemblyName)
        {
            return ResolveAssembyByName(assemblyName).Location;
        }

        internal Assembly ResolveAssembyByName(AssemblyName asmName)
        {

            if (name_asm.TryGetValue(asmName.FullName, out Assembly cached_asm))
            {
                return cached_asm;
            }

            Assembly asm = OnReflectionOnlyAssemblyResolveImpl(asmName.ToString());

            if (asm == null)
            {
                asm = Assembly.ReflectionOnlyLoad(asmName.ToString()); // Assembly.Load(asmName); // ReflectionOnly?
            }

            return CacheAssembly(asmName, asm);
        }

        internal Assembly CacheType(Type type)
        {
            CacheAssembly(type.Assembly.GetName(), type.Assembly);
            return type.Assembly;
        }
        private Assembly CacheAssembly(AssemblyName asmName, Assembly asm)
        {
            if (!name_asm.ContainsKey(asmName.FullName))
            {
                name_asm.Add(asmName.FullName, asm);
            }
            if (!name_location.ContainsKey(asmName.FullName))
            {
                name_location.Add(asmName.FullName, asm.Location);
            }

            if (!location_name.ContainsKey(asm.Location))
            {
                location_name.Add(asm.Location, asmName.FullName);
            }

            return asm;
        }

        internal void VerifyAssemblyLocation(string asm_location)
        {
            if (!location_name.ContainsKey(asm_location))
            {
                throw new StoreLakeSdkException("Location not registered:" + asm_location);
            }
        }
    }
}
