using Microsoft.SqlServer.Server;
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
        internal static void GenerateAccessors(KnownDibixTypes dbx, AssemblyResolver assemblyResolver, DacPacRegistration dacpac, bool doGenerate, CompilerParameters comparam, CodeCompileUnit ccu, string inputdir)
        {
            string dacpacDllFileName = Path.GetFileName(dacpac.DacPacAssemblyFileName);
            string dacpacDllFullFileName = Path.Combine(inputdir, dacpacDllFileName);
            Console.WriteLine("Load '" + dacpacDllFullFileName + "'...");
            if (!File.Exists(dacpacDllFullFileName))
            {
                throw new StoreLakeSdkException("File could not be found:" + dacpacDllFullFileName);
            }

            Assembly asm = assemblyResolver.ResolveAssembyByLocation(dacpacDllFullFileName);
            if (doGenerate)
            {

                CollectIndirectAssemblies(assemblyResolver, dacpac, (string asm_location) =>
                {
                    AddReferencedAssemblies(assemblyResolver, comparam, asm_location);
                });

                foreach (AssemblyName ref_asm_name in asm.GetReferencedAssemblies())
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

                dbx.Dibix_DatabaseAccessorAttribute.ToString().GetHashCode();

                Type databaseAccessorAttributeType = dbx.Dibix_DatabaseAccessorAttribute;

                foreach (Type type in asm.GetTypes())
                {
                    if (type.IsDefined(databaseAccessorAttributeType))
                    {
                        GenerateCommandHandlerFacade(dbx, ccu, type);
                    }
                    else if (type.IsDefined(dbx.Dibix_StructuredTypeAttribute))
                    {
                        GenerateStructureTypeRow(ccu, type);
                    }
                }
            } // doGenerate
        }

        internal static KnownDibixTypes LoadKnownDibixTypes(Assembly asm_Dibix, AssemblyResolver assemblyResolver)
        {
            KnownDibixTypes dbx = new KnownDibixTypes();
            dbx.Dibix_DatabaseAccessorAttribute = asm_Dibix.GetType("Dibix.DatabaseAccessorAttribute", true);
            dbx.Dibix_StructuredTypeAttribute = asm_Dibix.GetType("Dibix.StructuredTypeAttribute", true);
            dbx.Dibix_StructuredType = asm_Dibix.GetType("Dibix.StructuredType", true);
            return dbx;
        }

        private static void GenerateStructureTypeRow(CodeCompileUnit ccu, Type udtType)
        {
            //Console.WriteLine("" + udtType.FullName);
            CodeNamespace ns = EnsureNamespace(ccu, udtType);
            CodeTypeDeclaration typedecl = BuildeStructureTypeRowType(udtType);
            ns.Types.Add(typedecl);

        }

        private static void AddReferencedAssemblies(AssemblyResolver assemblyResolver, CompilerParameters comparam, string asm_location)
        {
            if (comparam.ReferencedAssemblies.Contains(asm_location))
            {
                // already referenced
            }
            else
            {
                assemblyResolver.VerifyAssemblyLocation(asm_location);
                comparam.ReferencedAssemblies.Add(asm_location);
            }
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

        private static void GenerateCommandHandlerFacade(KnownDibixTypes dbx, CodeCompileUnit ccu, Type databaseAccessorType)
        {
            //Console.WriteLine("" + databaseAccessorType.FullName);
            CodeNamespace ns = EnsureNamespace(ccu, databaseAccessorType);
            CodeTypeDeclaration typedecl = BuildCommandHandlerFacadeType(dbx, databaseAccessorType);
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

        private static CodeTypeDeclaration BuildeStructureTypeRowType(Type udtType)
        {
            string typeName = udtType.Name + "Row";
            CodeTypeDeclaration typedecl = new CodeTypeDeclaration() { Name = typeName, IsClass = true, Attributes = MemberAttributes.Public | MemberAttributes.Final };

            MethodInfo mi = udtType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly); // public void Add(int va, int vb, int vc) 'Helpline.Data.IntThreeSet'
            if (mi == null)
            {
                throw new StoreLakeSdkException("UDT method 'Add' could not be found:" + udtType.AssemblyQualifiedName);
            }

            CodeMemberField field_record = new CodeMemberField(typeof(IDataRecord), "record");
            typedecl.Members.Add(field_record);

            CodeConstructor ctor = new CodeConstructor();
            ctor.Attributes = MemberAttributes.Public;
            ctor.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IDataRecord), "record"));
            typedecl.Members.Add(ctor);

            CodeAssignStatement assign_record = new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "record"), new CodeVariableReferenceExpression("record"));
            ctor.Statements.Add(assign_record);

            foreach (var pi in mi.GetParameters())
            {
                CodeMemberProperty member_property = new CodeMemberProperty()
                {
                    Name = pi.Name,
                    Type = new CodeTypeReference(pi.ParameterType),
                    Attributes = MemberAttributes.Public | MemberAttributes.Final
                };

                typedecl.Members.Add(member_property);

                CodeIndexerExpression indexer = new CodeIndexerExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "record"), new_CodePrimitiveExpression(pi.Name));

                CodeExpression var_value_ref;
                if (pi.ParameterType.IsValueType)
                {
                    var_value_ref = indexer;
                }
                else
                {
                    CodeVariableDeclarationStatement var_value_decl = new CodeVariableDeclarationStatement(typeof(object), "raw_value", indexer);
                    var_value_ref = new CodeVariableReferenceExpression(var_value_decl.Name);
                    var conditionExpr =
                    new CodeBinaryOperatorExpression(var_value_ref,
                        CodeBinaryOperatorType.ValueEquality,
                        new CodePropertyReferenceExpression(new CodeTypeReferenceExpression(typeof(DBNull)), "Value"));
                    CodeConditionStatement ifNull = new CodeConditionStatement(conditionExpr, new CodeMethodReturnStatement(new_CodePrimitiveExpression(null)));

                    member_property.GetStatements.Add(var_value_decl);
                    member_property.GetStatements.Add(ifNull);
                }


                member_property.GetStatements.Add(new CodeMethodReturnStatement(new CodeCastExpression(member_property.Type, var_value_ref)));
            }

            return typedecl;
        }

        private static CodeExpression new_CodePrimitiveExpression(object value)
        {
            if (value != null && value is byte[])
            {
                throw new NotSupportedException();
            }
            return new CodePrimitiveExpression(value);
        }
        private static CodeTypeDeclaration BuildCommandHandlerFacadeType(KnownDibixTypes dbx, Type databaseAccessorType)
        {
            string typeName = databaseAccessorType.Name + "CommandHandlerFacade";
            CodeTypeDeclaration typedecl = new CodeTypeDeclaration() { Name = typeName, IsClass = true, Attributes = MemberAttributes.Public };

            //typedecl.Comments.Add(new CodeCommentStatement("Generated (at:" + DateTime.UtcNow + ")", true));
            typedecl.CustomAttributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof(System.ComponentModel.DescriptionAttribute)), new CodeAttributeArgument(new_CodePrimitiveExpression("Generated (at:" + DateTime.UtcNow + ")"))));

            foreach (MethodInfo accessMethod in databaseAccessorType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] acessMethodParameters = accessMethod.GetParameters();
                if (acessMethodParameters.Length == 0)
                {
                    throw new StoreLakeSdkException("Access method signature not correct. Method:" + accessMethod.Name + ", Type:" + databaseAccessorType.FullName);
                }

                // first parameter must be of type 'IDatabaseAccessorFactory'
                if (acessMethodParameters[0].ParameterType.Name != "IDatabaseAccessorFactory")
                {
                    throw new StoreLakeSdkException("Access method parameter signature not correct. Parameter:" + acessMethodParameters[0].Name + ", Method:" + accessMethod.Name + ", Type:" + databaseAccessorType.FullName);
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

                        CodeTypeReference parameterTypeRef;
                        if (IsUDT(dbx, parameterType))
                        {
                            //parameterType = typeof(IEnumerable<Microsoft.SqlServer.Server.SqlDataRecord>);
                            parameterTypeRef = CreateUdtParameterType(parameterType);
                        }
                        else
                        {
                            string accessParameterTypeName = TypeNameAsText(parameterType);
                            parameterTypeRef = new CodeTypeReference(parameterType);
                        }

                        CodeParameterDeclarationExpression prm_decl = new CodeParameterDeclarationExpression(parameterTypeRef, accessMethodParameter.Name);
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
                var genericType = t.GetGenericTypeDefinition();
                var genericTypeName = GetGenericTypeNameWithoutGenericArity(genericType);
                Type[] genericArguments = t.GetGenericArguments();

                if (typeof(Nullable<>) == genericType)
                {
                    //return genericTypeName + "<" + TypeNameAsText(genericArguments[0]) + ">";
                }
                else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
                {
                }
                else
                {

                }
                {

                    StringBuilder buffer = new StringBuilder();
                    buffer.Append(genericTypeName);
                    buffer.Append("<");
                    for (int ix = 0; ix < genericArguments.Length; ix++)
                    //if (genericArguments.Length == 1)
                    {
                        var typeT = genericArguments[ix];
                        if (ix > 0)
                        {
                            buffer.Append(", ");
                        }
                        buffer.Append(TypeNameAsText(typeT));
                    }
                    buffer.Append(">");
                    return buffer.ToString();

                }
            }
            return t.FullName;
        }
        private static string GetGenericTypeNameWithoutGenericArity(Type t)
        {
            string name = t.Name;
            int index = name.IndexOf('`');
            return index == -1 ? name : name.Substring(0, index);
        }

        private static bool IsUDT(KnownDibixTypes dbx, Type parameterType)
        {
            if (dbx.Dibix_StructuredType.IsAssignableFrom(parameterType))
                return true;
            return false;
        }

        private static CodeTypeReference CreateUdtParameterType(Type parameterType)
        {
            return new CodeTypeReference("System.Collections.Generic.IEnumerable<" + parameterType.FullName + "Row" + ">");
        }

        private static Type CreateUdtParameterTypeX(Type parameterType)
        {
            //  IntThreeSet : StructuredType<IntThreeSet, int, int, int>
            Type[] genericArguments = parameterType.BaseType.GetGenericArguments();
            if (genericArguments.Length < 2)
            {
                throw new StoreLakeSdkException("NotImplemented:" + "generic_prms.Length:" + genericArguments.Length);
            }

            Type itemType;
            if (genericArguments.Length == 2)
            {
                itemType = genericArguments[1];
            }
            else
            {
                List<Type> arg_types = new List<Type>();

                for (int ix = 1; ix < genericArguments.Length; ix++)
                {
                    Type typeT = genericArguments[ix];
                    arg_types.Add(typeT);
                }

                //var xx2 = typeof(Tuple<,>).FullName; // System.Tuple`2
                //var xx3 = typeof(Tuple<,,>).FullName; // System.Tuple`3
                Type tupleDefinitionType = Type.GetType("System.Tuple`" + arg_types.Count);
                itemType = tupleDefinitionType.MakeGenericType(arg_types.ToArray());
            }


            Type resultType = typeof(IEnumerable<>).MakeGenericType(itemType);

            var xxx = TypeNameAsText(resultType);
            return resultType;
        }


    }
}
