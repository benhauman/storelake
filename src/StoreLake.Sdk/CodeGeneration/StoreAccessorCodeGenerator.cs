using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace StoreLake.Sdk.CodeGeneration
{
    internal static class StoreAccessorCodeGenerator
    {
        internal static void GeneratorAccessors(AssemblyResolver assemblyResolver, DacPacRegistration dacpac, CompilerParameters comparam, CodeCompileUnit ccu, string inputdir)
        {

            string dacpacDllFileName = Path.GetFileName(dacpac.DacPacAssemblyFileName);
            string dacpacDllFullFileName = Path.Combine(inputdir, dacpacDllFileName);
            Console.WriteLine("Load '" + dacpacDllFullFileName + "'...");
            if (!File.Exists(dacpacDllFullFileName))
            {
                throw new NotSupportedException("File could not be found:" + dacpacDllFullFileName);
            }

            Assembly asm = assemblyResolver.ResolveAssembyByLocation(dacpacDllFullFileName);


            CollectIndirectAssemblies(assemblyResolver, dacpac, (string asm_location) =>
            {
                AddReferencedAssemblies(assemblyResolver, comparam, asm_location);
            });

            foreach (var ref_asm_name in asm.GetReferencedAssemblies())
            {
                if (ref_asm_name.FullName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase)
                    || ref_asm_name.FullName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                    || ref_asm_name.FullName.StartsWith("System,", StringComparison.OrdinalIgnoreCase)
                    || ref_asm_name.FullName.StartsWith("Newtonsoft.", StringComparison.OrdinalIgnoreCase)
                    )
                {

                    // ignore

                }
                else
                {

                    Console.WriteLine(ref_asm_name.FullName);
                    if (dacpac.referenced_assemblies.TryGetValue(ref_asm_name.FullName, out string ref_asm_location))
                    {

                    }
                    else
                    {
                        Assembly ref_asm = assemblyResolver.ResolveAssembyByName(ref_asm_name);
                        ref_asm_location = ref_asm.Location;


                        dacpac.referenced_assemblies.Add(ref_asm.GetName().FullName, ref_asm.Location);
                    }

                    AddReferencedAssemblies(assemblyResolver, comparam, ref_asm_location);
                }
            }
            AddReferencedAssemblies(assemblyResolver, comparam, asm.Location);
            dacpac.referenced_assemblies.Add(asm.GetName().FullName, asm.Location);
            ;
            AddReferencedAssemblies(assemblyResolver, comparam, assemblyResolver.CacheType(typeof(System.Xml.Linq.XElement)).Location); // System.Xml.Linq



            Type databaseAccessorAttributeType = typeof(Dibix.DatabaseAccessorAttribute);

            foreach (Type type in asm.GetTypes())
            {
                if (type.IsDefined(databaseAccessorAttributeType))
                {
                    GenerateCommandHandlerFacade(ccu, type);
                }
            }

        }

        private static void AddReferencedAssemblies(AssemblyResolver assemblyResolver, CompilerParameters comparam, string asm_location)
        {
            assemblyResolver.VerifyAssemblyLocation(asm_location);
            comparam.ReferencedAssemblies.Add(asm_location);
        }

        private static void CollectIndirectAssemblies(AssemblyResolver assemblyResolver, DacPacRegistration dacpac, Action<string> collector)
        {
            foreach (DacPacRegistration ref_pac in dacpac.referenced_dacpacs.Values)
            {
                foreach (string ref_asm in ref_pac.referenced_assemblies.Keys)
                {
                    string asm_location = assemblyResolver.ResolveLocationByName(new AssemblyName(ref_asm));
                    collector(asm_location);
                }
                if (!string.IsNullOrEmpty(ref_pac.DacPacTestStoreAssemblyFileName))
                {
                    collector(ref_pac.DacPacTestStoreAssemblyFileName);
                }

                CollectIndirectAssemblies(assemblyResolver, ref_pac, collector);
            }

        }

        private static void GenerateCommandHandlerFacade(CodeCompileUnit ccu, Type databaseAccessorType)
        {
            Console.WriteLine("" + databaseAccessorType.FullName);
            CodeNamespace ns = EnsureNamespace(ccu, databaseAccessorType);
            CodeTypeDeclaration typedecl = BuildCommandHandlerFacadeType(databaseAccessorType);
            ns.Types.Add(typedecl);
        }


        private static CodeNamespace EnsureNamespace(CodeCompileUnit ccu, Type databaseAccessorType)
        {
            CodeNamespace ns = null;
            foreach (CodeNamespace ccu_ns in ccu.Namespaces)
            {
                if (ccu_ns.Name == databaseAccessorType.Namespace)
                {
                    ns = ccu_ns;
                    break;
                }
            }
            if (ns == null)
            {
                ns = new CodeNamespace(databaseAccessorType.Namespace);
                ccu.Namespaces.Add(ns);
            }

            return ns;
        }


        private static CodeTypeDeclaration BuildCommandHandlerFacadeType(Type databaseAccessorType)
        {
            string typeName = databaseAccessorType.Name + "CommandHandlerFacade";
            CodeTypeDeclaration typedecl = new CodeTypeDeclaration() { Name = typeName, IsClass = true, Attributes = MemberAttributes.Public };

            //typedecl.Comments.Add(new CodeCommentStatement("Generated (at:" + DateTime.UtcNow + ")", true));
            typedecl.CustomAttributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof(System.ComponentModel.DescriptionAttribute)), new CodeAttributeArgument(new CodePrimitiveExpression("Generated (at:" + DateTime.UtcNow + ")"))));

            foreach (MethodInfo accessMethod in databaseAccessorType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] acessMethodParameters = accessMethod.GetParameters();
                if (acessMethodParameters.Length == 0)
                {
                    throw new InvalidOperationException("Access method signature not correct. Method:" + accessMethod.Name + ", Type:" + databaseAccessorType.FullName);
                }

                // first parameter must be of type 'IDatabaseAccessorFactory'
                if (acessMethodParameters[0].ParameterType.Name != "IDatabaseAccessorFactory")
                {
                    throw new InvalidOperationException("Access method parameter signature not correct. Parameter:" + acessMethodParameters[0].Name + ", Method:" + accessMethod.Name + ", Type:" + databaseAccessorType.FullName);
                }

                CodeMemberMethod code_method = new CodeMemberMethod() { Name = accessMethod.Name, Attributes = MemberAttributes.Public };
                typedecl.Members.Add(code_method);
                code_method.ReturnType = new CodeTypeReference(accessMethod.ReturnType);
                code_method.Statements.Add(new CodeThrowExceptionStatement(new CodeObjectCreateExpression(new CodeTypeReference(typeof(NotImplementedException)))));

                code_method.Parameters.Add(new CodeParameterDeclarationExpression(typeof(DataSet), "db"));

                if (acessMethodParameters.Length > 1)
                {
                    // second parameter can be 'long actioncontextid'
                    for (int parameter_index = 1; parameter_index < acessMethodParameters.Length; parameter_index++)
                    {
                        ParameterInfo accessMethodParameter = acessMethodParameters[parameter_index];
                        Type parameterType = accessMethodParameter.ParameterType.IsByRef ? accessMethodParameter.ParameterType.GetElementType() : accessMethodParameter.ParameterType;
                        string accessParameterTypeName = TypeNameAsText(parameterType);

                        CodeParameterDeclarationExpression prm_decl = new CodeParameterDeclarationExpression(new CodeTypeReference(parameterType), accessMethodParameter.Name);
                        code_method.Parameters.Add(prm_decl);
                        prm_decl.Direction = accessMethodParameter.IsOut
                                                    ? FieldDirection.Out
                                                    : (accessMethodParameter.ParameterType.IsByRef) ? FieldDirection.Ref : FieldDirection.In;

                        if (accessMethodParameter.IsOut)
                        {
                        }
                    }
                }
            }

            return typedecl;
        }

        private static string TypeNameAsText(Type t)
        {
            string text;
            if (SchemaExportCode._builtinTypeAlias.TryGetValue(t.FullName, out text))
            {
                return text;
            }

            if (t.IsGenericType)
            {
                if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
                {
                    if (t.GetGenericArguments().Length == 1)
                    {
                        var typeT = t.GetGenericArguments()[0];
                        return "IEnumerable<" + TypeNameAsText(typeT) + ">";
                    }
                    throw new NotImplementedException(t.FullName);
                }
            }
            return t.FullName;
        }

        private static FieldDirection GetRefValueType(ParameterInfo accessMethodParameter)
        {
            if (accessMethodParameter.IsOut)
                return FieldDirection.Out;
            else if (accessMethodParameter.ParameterType.IsByRef)
                return FieldDirection.Ref;
            else
                return FieldDirection.In;
        }
    }
}
