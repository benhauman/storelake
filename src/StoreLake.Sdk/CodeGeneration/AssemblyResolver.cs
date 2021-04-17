using System;
using System.Collections.Generic;
using System.Reflection;

namespace StoreLake.Sdk.CodeGeneration
{
    internal sealed class AssemblyResolver
    {
        private readonly IDictionary<string, string> name_location = new SortedDictionary<string, string>();
        private readonly IDictionary<string, string> location_name = new SortedDictionary<string, string>();
        private readonly IDictionary<string, Assembly> name_asm = new SortedDictionary<string, Assembly>();
        internal Assembly ResolveAssembyByLocation(string dacpacDllFullFileName)
        {
            if (location_name.TryGetValue(dacpacDllFullFileName.ToUpperInvariant(), out string cached_name))
            {
                if (name_asm.TryGetValue(cached_name, out Assembly cached_asm))
                {
                    return cached_asm;
                }
            }
            AssemblyName asmName = AssemblyName.GetAssemblyName(dacpacDllFullFileName);
            Assembly asm = Assembly.Load(asmName);

            return CacheAssembly(asmName, asm);
        }
        internal string ResolveLocationByName(AssemblyName assemblyName)
        {
            return ResolveAssembyByName(assemblyName).Location;
        }

        internal Assembly ResolveAssembyByName(AssemblyName asmName)
        {

            if (name_asm.TryGetValue(asmName.FullName.ToUpperInvariant(), out Assembly cached_asm))
            {
                return cached_asm;
            }

            Assembly asm = Assembly.Load(asmName);

            return CacheAssembly(asmName, asm);
        }

        internal Assembly CacheType(Type type)
        {
            CacheAssembly(type.Assembly.GetName(), type.Assembly);
            return type.Assembly;
        }
        private Assembly CacheAssembly(AssemblyName asmName, Assembly asm)
        {
            if (!name_asm.ContainsKey(asmName.FullName.ToUpperInvariant()))
            {
                name_asm.Add(asmName.FullName.ToUpperInvariant(), asm);
            }
            if (!name_location.ContainsKey(asmName.FullName.ToUpperInvariant()))
            {
                name_location.Add(asmName.FullName.ToUpperInvariant(), asm.Location);
            }

            if (!location_name.ContainsKey(asm.Location.ToUpperInvariant()))
            {
                location_name.Add(asm.Location.ToUpperInvariant(), asmName.FullName);
            }

            return asm;
        }

        internal void VerifyAssemblyLocation(string asm_location)
        {
            if (!location_name.ContainsKey(asm_location.ToUpperInvariant()))
            {
                throw new StoreLakeSdkException("Location not registered:" + asm_location);
            }
        }
    }
}
