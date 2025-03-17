using Microsoft.CSharp;
using StoreLake.Sdk.SqlDom;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace StoreLake.Sdk.CodeGeneration
{
    public static class SchemaExportCode
    {

        internal static readonly TraceSource s_tracer = new TraceSource("StoreLake.Sdk") { Switch = new SourceSwitch("TraceAll") { Level = SourceLevels.All } };
        public static TraceSource CreateTraceSource()
        {
            return s_tracer;
        }

        internal static void ExportTypedDataSetCode(AssemblyResolver assemblyResolver, RegistrationResult rr, string[] libdirs, string inputdir, string outputdir, string dacNameFilter, string storeSuffix, bool writeSchemaFile, string tempdir)
        {
            AssemblyName an_Dibix = new AssemblyName("Dibix"); // no version
            //AssemblyName an_Dibix = AssemblyName.GetAssemblyName(Path.Combine(libdir, "Dibix.dll"));
            //AssemblyName an_DibixHttpServer = AssemblyName.GetAssemblyName(Path.Combine(libdir, "Dibix.Http.Server.dll"));
            //AssemblyName an_DibixHttpClient = AssemblyName.GetAssemblyName(Path.Combine(libdir, "Dibix.Http.Client.dll"));
            assemblyResolver.CacheAssembly(Assembly.Load("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51")); // load the assembly and cache its location for OnReflectionOnlyAssemblyResolve
            Assembly asm_Dibix = assemblyResolver.ResolveAssembyByName(an_Dibix);
            //assemblyResolver.ResolveAssembyByName(an_DibixHttpServer);
            //assemblyResolver.ResolveAssembyByName(an_DibixHttpClient);
            assemblyResolver.ResolveAssembyByName(typeof(System.Data.IDbCommand).Assembly.GetName());

            KnownDibixTypes dbx = StoreAccessorCodeGenerator.LoadKnownDibixTypes(asm_Dibix, assemblyResolver);

            var level_count = rr.registered_dacpacs.Values.Max(x => x.DacPacDependencyLevel);
            for (int level = 1; level <= level_count; level++)
            {
                Console.WriteLine("Level:" + level);

                foreach (DacPacRegistration dacpac in rr.registered_dacpacs.Values)
                {
                    if (dacpac.DacPacDependencyLevel == level)
                    {
                        Console.WriteLine(dacpac.DacPacAssemblyLogicalName);

                        string dacName = Path.GetFileNameWithoutExtension(dacpac.DacPacAssemblyLogicalName);

                        if (!string.IsNullOrEmpty(dacNameFilter) && !string.Equals(dacNameFilter, dacName, StringComparison.OrdinalIgnoreCase))
                        {
                            // ignore 
                            // Console.WriteLine("skip:not in filter.");
                        }
                        else
                        {
                            string schemaFileName = System.IO.Path.Combine(outputdir, dacName + ".Schema.xsd");
                            string filenameNoExtension = dacName + "." + storeSuffix; // .TestStore

                            string schemaContent;
                            using (StringWriter xsdWriter = new StringWriter())
                            {
                                rr.ds.WriteXmlSchema(xsdWriter);
                                //SchemaExportCode.ExportSchemaXsd(rr.ds, schemaFileName);
                                schemaContent = xsdWriter.GetStringBuilder().ToString();
                            }
                            if (writeSchemaFile)
                            {
                                if (dacpac.IsReferencedPackage && File.Exists(schemaFileName) && !rr.ForceReferencePackageRegeneration)
                                {
                                    // skip 
                                }
                                else
                                {
                                    File.WriteAllText(schemaFileName, schemaContent);
                                }
                                //string schemaContent = File.ReadAllText(schemaFileName);
                            }

                            ImportSchemasAsDataSets(dbx, assemblyResolver, rr, dacpac, schemaContent, inputdir, outputdir, filenameNoExtension, dacName, storeSuffix, tempdir, libdirs);
                        }
                    }

                }
            }

            if (level_count < 100)
            {
                return;
            }

        }


        private static void ImportSchemasAsDataSets(KnownDibixTypes dbx, AssemblyResolver assemblyResolver, RegistrationResult rr, DacPacRegistration dacpac, string schemaContent, string inputdir, string outputdir, string fileName, string namespaceName, string storeSuffix, string tempdir, string[] libdirs)
        {
            string fullFileName_dll = System.IO.Path.Combine(outputdir, fileName + ".dll");
            InitializeStoreNamespaceName(dacpac, storeSuffix);

            bool doGenerate;
            if (dacpac.IsReferencedPackage && File.Exists(fullFileName_dll) && !rr.ForceReferencePackageRegeneration)
            {
                // skip
                doGenerate = false;
            }
            else
            {
                doGenerate = true;
            }

            string subtemp_stamp = "";
            if (fullFileName_dll == null)
            {
                subtemp_stamp = DateTimeNow() + "_";
            }

            DirectoryInfo tempDirInfo = new DirectoryInfo(Path.Combine(tempdir, subtemp_stamp + fileName));
            if (doGenerate)
            {
                if (tempDirInfo.Exists)
                {
                    tempDirInfo.Delete(true);
                }
                tempDirInfo.Create();

            }

            Microsoft.CSharp.CSharpCodeProvider codeProvider = new Microsoft.CSharp.CSharpCodeProvider();// //CodeDomProvider.CreateProvider(language);

            CodeCompileUnit ccu_main = new CodeCompileUnit();
            CodeCompileUnit ccu_tables = new CodeCompileUnit();
            CodeCompileUnit ccu_tabletypes = new CodeCompileUnit();
            CodeCompileUnit ccu_procedures = new CodeCompileUnit();
            CodeCompileUnit ccu_accessors = new CodeCompileUnit();

            CompilerParameters comparam = new CompilerParameters(new string[] { });
            if (doGenerate)
            {
                assemblyResolver.ResolveAssembyByName(typeof(System.Data.DataTable).Assembly.GetName());
                assemblyResolver.ResolveAssembyByName(typeof(System.Data.TypedTableBase<>).Assembly.GetName());
                assemblyResolver.ResolveAssembyByName(typeof(System.Xml.Linq.XElement).Assembly.GetName());
                AddReferencedAssembly(assemblyResolver, comparam, typeof(System.ComponentModel.MarshalByValueComponent).Assembly);
                AddReferencedAssembly(assemblyResolver, comparam, typeof(System.Data.DataTable).Assembly);
                AddReferencedAssembly(assemblyResolver, comparam, typeof(System.Data.TypedTableBase<>).Assembly);
                AddReferencedAssembly(assemblyResolver, comparam, typeof(System.Xml.Serialization.IXmlSerializable).Assembly);
                AddReferencedAssembly(assemblyResolver, comparam, typeof(Enumerable).Assembly);

                AddAssemblyAttributes(ccu_main);

                GenerateDataSetClasses(ccu_tables, schemaContent, namespaceName, codeProvider);

                //=================================================================================
                //string codeFileName = Path.Combine(tempDirInfo.FullName, fileName + ".RawMSCode.cs");
                //using (TextWriter textWriter = new StreamWriter(codeFileName, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                //{
                //    codeProvider.GenerateCodeFromCompileUnit(ccu, textWriter, null);
                //}
                //=================================================================================


                CodeNamespace ns_tabletypes = new CodeNamespace() { Name = dacpac.TestStoreAssemblyNamespace };
                ccu_tabletypes.Namespaces.Add(ns_tabletypes);
                BuildUdts(rr, dacpac, ns_tabletypes);

                ExtensionsClass exttype = Adjust_CCU(assemblyResolver, comparam, rr, dacpac, ccu_tables, ccu_tabletypes, ccu_procedures, storeSuffix);

                CodeNamespace ns_procedures = new CodeNamespace() { Name = dacpac.TestStoreAssemblyNamespace };
                ccu_procedures.Namespaces.Add(ns_procedures);
                ProcedureBuilder.BuildProcedures(rr, dacpac, ns_procedures, exttype);
            }
            else
            {
                CodeNamespace ns_tabletypes = new CodeNamespace() { Name = dacpac.TestStoreAssemblyNamespace };
                BuildUdts(rr, dacpac, ns_tabletypes);
            }
            //dacpac.TestStoreAssemblyNamespace

            StoreAccessorCodeGenerator.GenerateAccessors(dbx, assemblyResolver, dacpac, doGenerate, comparam, ccu_accessors, inputdir);

            if (!doGenerate)
            {
                s_tracer.TraceEvent(TraceEventType.Information, 0, "SKIP Assembly generation '" + fullFileName_dll + "'. Reason: already exists.");
            }
            else
            {
                if (dacpac.IsReferencedPackage)
                {
                    if (File.Exists(fullFileName_dll))
                    {
                        s_tracer.TraceEvent(TraceEventType.Warning, 0, "Regenerate reference package:" + fullFileName_dll);
                    }
                    else
                    {
                        if (rr.GenerateMissingReferences)
                        {
                            s_tracer.TraceEvent(TraceEventType.Information, 0, "Generate missing referenced package:" + fullFileName_dll);
                        }
                        else
                        {
                            throw new StoreLakeSdkException("Referenced package assembly '" + fullFileName_dll + "' could not be found. To genenerate mising references use option 'GenerateMissingReferences'.");
                        }
                    }
                }
                else
                {
                    s_tracer.TraceEvent(TraceEventType.Information, 0, "Generate package:" + fullFileName_dll);
                }

                if (!Directory.Exists(outputdir))
                {
                    Directory.CreateDirectory(outputdir);
                }
                if (!Directory.Exists(tempdir))
                {
                    Directory.CreateDirectory(tempdir);
                }
                if (!Directory.Exists(tempDirInfo.FullName))
                {
                    Directory.CreateDirectory(tempDirInfo.FullName);
                }

                List<string> codeFileNames = new List<string>();
                {
                    GenerateCodeFile(tempDirInfo, codeProvider, codeFileNames, fileName, ccu_main, "Main");
                    GenerateCodeFile(tempDirInfo, codeProvider, codeFileNames, fileName, ccu_tables, "Tables");
                    GenerateCodeFile(tempDirInfo, codeProvider, codeFileNames, fileName, ccu_tabletypes, "Udts");
                    GenerateCodeFile(tempDirInfo, codeProvider, codeFileNames, fileName, ccu_procedures, "Procedures");
                    GenerateCodeFile(tempDirInfo, codeProvider, codeFileNames, fileName, ccu_accessors, "Accessors");
                }

                int count_of_types = ccu_main.CountOfType()
                    + ccu_tables.CountOfType()
                    + ccu_tabletypes.CountOfType()
                    + ccu_procedures.CountOfType()
                    + ccu_accessors.CountOfType()
                    ;

                CompileCode(dacpac, comparam, libdirs, outputdir, fileName, fullFileName_dll, tempDirInfo, codeFileNames.ToArray(), count_of_types);
            }
            assemblyResolver.ResolveAssembyByLocation(fullFileName_dll);
            dacpac.TestStoreAssemblyFullFileName = fullFileName_dll;

            s_tracer.TraceEvent(TraceEventType.Information, 0, fullFileName_dll);
        }

        private static void GenerateCodeFile(DirectoryInfo tempDirInfo, CSharpCodeProvider codeProvider, List<string> codeFileNames, string fileName, CodeCompileUnit ccu, string area)
        {
            string codeFileName = Path.Combine(tempDirInfo.FullName, fileName + "." + area + ".cs");
            using (TextWriter textWriter = new StreamWriter(codeFileName, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            //using (TextWriter textWriter = CreateOutputWriter(Path.Combine(outputdir, path), fileName, "cs"))
            {
                codeProvider.GenerateCodeFromCompileUnit(ccu, textWriter, null);
            }
            s_tracer.TraceEvent(TraceEventType.Verbose, 0, "codeFileName:" + codeFileName);
            codeFileNames.Add(codeFileName);
        }

        private static void AddReferencedAssembly(AssemblyResolver assemblyResolver, CompilerParameters comparam, Assembly asm)
        {
            if (comparam.ReferencedAssemblies.Contains(asm.Location))
            {
                // already referenced
            }
            else
            {
                assemblyResolver.ResolveAssembyByName(asm.GetName());
                assemblyResolver.VerifyAssemblyLocation(asm.Location);
                comparam.ReferencedAssemblies.Add(asm.Location);
            }
        }

        private static string DateTimeNow()
        {
            return DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        }

        internal static void GenerateDataSetClasses(CodeCompileUnit ccu, string schemaContent, string namespaceName, CodeDomProvider codeProvider)
        {
            CodeNamespace codeNamespace = new CodeNamespace(namespaceName);
            ccu.Namespaces.Add(codeNamespace);
            GenerateVersionComment(codeNamespace);

            System.Data.Design.TypedDataSetGenerator.Generate(schemaContent, ccu, codeNamespace, codeProvider, System.Data.Design.TypedDataSetGenerator.GenerateOption.LinqOverTypedDatasets, "zzzzzzz");

        }

        private static CodeTypeDeclaration CreateStaticClass(string name)
        {
            // https://stackoverflow.com/questions/6308310/creating-extension-method-using-codedom
            CodeTypeDeclaration type = new CodeTypeDeclaration(name);
            type.Attributes = MemberAttributes.Public | MemberAttributes.Static;
            type.StartDirectives.Add(new CodeRegionDirective(CodeRegionMode.Start, Environment.NewLine + "\tstatic"));
            type.EndDirectives.Add(new CodeRegionDirective(CodeRegionMode.End, String.Empty));
            return type;
        }

        class NestedTypeDeclaration
        {
            internal CodeTypeDeclaration Owner;
            internal CodeTypeDeclaration Member;
        }

        private static ExtensionsClass Adjust_CCU(AssemblyResolver assemblyResolver, CompilerParameters comparam, RegistrationResult rr, DacPacRegistration dacpac
            , CodeCompileUnit ccu_tables
            , CodeCompileUnit ccu_tabletypes
            , CodeCompileUnit ccu_procedures
            , string storeSuffix)
        {
            if (ccu_tables.Namespaces.Count > 1)
            {
                throw new StoreLakeSdkException("Multiple namespaces");
            }


            ExtensionsClass exttype = new ExtensionsClass();
            CodeNamespace ns_old = ccu_tables.Namespaces[0];
            ccu_tables.Namespaces.Clear();
            CodeNamespace ns_tables = new CodeNamespace() { Name = dacpac.TestStoreAssemblyNamespace };
            ccu_tables.Namespaces.Add(ns_tables);

            {
                exttype.extensions_type_decl = CreateStaticClass(dacpac.TestStoreExtensionSetName + "Extensions");

                // Create 'GetTable'
                exttype.extensions_method_GetTable = new CodeMemberMethod() { Name = "GetTable", Attributes = MemberAttributes.Private | MemberAttributes.Static };
                CodeTypeParameter ctp_Table = new CodeTypeParameter("TTable");
                ctp_Table.Constraints.Add(new CodeTypeReference(typeof(DataTable)));
                exttype.extensions_method_GetTable.TypeParameters.Add(ctp_Table);
                exttype.extensions_method_GetTable.Parameters.Add(new CodeParameterDeclarationExpression(typeof(DataSet), "ds"));
                exttype.extensions_method_GetTable.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string), "tableName"));
                exttype.extensions_method_GetTable.ReturnType = new CodeTypeReference("TTable");
                var var_decl_table = new CodeVariableDeclarationStatement("TTable", "table");
                exttype.extensions_method_GetTable.Statements.Add(var_decl_table);
                var var_ref_table = new CodeVariableReferenceExpression("table");

                CodePropertyReferenceExpression ds_Tables = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("ds"), "Tables");
                CodeArrayIndexerExpression indexer = new CodeArrayIndexerExpression(ds_Tables, new CodeExpression[] { new CodeVariableReferenceExpression("tableName") });
                CodeCastExpression cast_expr = new CodeCastExpression("TTable", indexer);
                var_decl_table.InitExpression = cast_expr;

                var ifTableNull = new CodeBinaryOperatorExpression(var_ref_table, CodeBinaryOperatorType.ValueEquality, new_CodePrimitiveExpression(null));
                var throwExpr = new CodeThrowExceptionStatement(new CodeObjectCreateExpression(typeof(ArgumentException), new CodeSnippetExpression("\"Table [\" + tableName + \"] could not be found.\""), new_CodePrimitiveExpression("tableName")));
                exttype.extensions_method_GetTable.Statements.Add(new CodeConditionStatement(ifTableNull, throwExpr));

                //method_gettable.Statements.Add(new CodeAssignStatement(var_ref_table, cast_expr));

                exttype.extensions_method_GetTable.Statements.Add(new CodeMethodReturnStatement(var_ref_table));
                exttype.extensions_type_decl.Members.Add(exttype.extensions_method_GetTable);
                ns_tables.Types.Add(exttype.extensions_type_decl);
            }

            {
                List<NestedTypeDeclaration> nestedTypes = new List<NestedTypeDeclaration>();

                foreach (CodeTypeDeclaration type_decl in ns_old.Types)
                {
                    bool isSetClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType == typeof(System.Data.DataSet).FullName;
                    bool isTableClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType.Contains("System.Data.TypedTableBase");
                    bool isRowClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType == typeof(System.Data.DataRow).FullName;

                    //nsTypes.Add(type_decl);
                    bool isOwnedByDacPac = false;
                    if (isSetClassDeclaration)
                    {
                        isOwnedByDacPac = true;
                    }
                    else if (isRowClassDeclaration)
                    {
                        // extract table name from row class name
                        string tableName = type_decl.Name.Remove(type_decl.Name.Length - 3, 3);
                        isOwnedByDacPac = IsTableOwnedByDacPac(rr, dacpac, tableName);
                    }
                    else
                    {
                        throw new StoreLakeSdkException("NotImplemented:" + type_decl.Name);
                    }


                    if (!isOwnedByDacPac)
                    {
                        // not part of this dacpac
                    }
                    else
                    {
                        Adjust_TypeDecl(rr, dacpac, exttype, ns_tables.Name, type_decl);
                        if (isSetClassDeclaration)
                        {
                            // skip it : 'NewDataSet'
                        }
                        else
                        {
                            ns_tables.Types.Add(type_decl);
                        }

                        foreach (CodeTypeMember member_decl in type_decl.Members)
                        {
                            if (member_decl.GetType() == typeof(CodeTypeDeclaration))
                            {
                                bool vvvv = member_decl.Attributes.IsPublic();
                                nestedTypes.Add(new NestedTypeDeclaration() { Owner = type_decl, Member = (CodeTypeDeclaration)member_decl });
                            }
                        }
                    }
                }

                // add foreign keys between tables
                exttype.extensions_method_InitDataSet = TryGetMemberMethodByName(exttype.extensions_type_decl, "InitDataSetClass");
                if (exttype.extensions_method_InitDataSet == null)
                {
                    throw new InvalidOperationException("Method 'InitDataSet' on DataSet extensions type could not be found.");
                }

                foreach (string table_name in dacpac.registered_tables.Keys)
                {
                    AddTableForeignKeys(assemblyResolver, comparam, rr, dacpac, exttype, exttype.extensions_method_InitDataSet, table_name);
                }

                // MoveNestedTypesToNamespace
                foreach (NestedTypeDeclaration nested_type in nestedTypes)
                {
                    nested_type.Owner.Members.Remove(nested_type.Member);
                    ns_tables.Types.Add(nested_type.Member);
                }
            }

            return exttype;
        }

        private static void BuildUdts(RegistrationResult rr, DacPacRegistration dacpac, CodeNamespace ns_tabletypes)
        {
            foreach (var udt_reg in dacpac.registered_tabletypes.Values)
            {
                string udtRowClassName = PrepareUdtRowName(udt_reg);

                TableTypeRow udtRow = new TableTypeRow();
                udtRow.udt_row_type_decl = new CodeTypeDeclaration(udtRowClassName);
                udtRow.ClrFullTypeName = ns_tabletypes.Name + "." + udtRowClassName;
                ns_tabletypes.Types.Add(udtRow.udt_row_type_decl);

                BuildUdtRow(udt_reg, udtRow);

                rr.udt_rows.Add(udt_reg.TableTypeSqlFullName, udtRow);
            }
        }

        private static void BuildUdtRow(StoreLakeTableTypeRegistration udt_reg, TableTypeRow udtRow)
        {
            //MethodInfo mi = udtType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly); // public void Add(int va, int vb, int vc) 'Helpline.Data.IntThreeSet'
            //if (mi == null)
            //{
            //    throw new StoreLakeSdkException("UDT method 'Add' could not be found:" + udtType.AssemblyQualifiedName);
            //}

            CodeMemberField field_record = new CodeMemberField(typeof(IDataRecord), "record");
            udtRow.udt_row_type_decl.Members.Add(field_record);

            CodeConstructor ctor = new CodeConstructor() { Attributes = MemberAttributes.Private };
            ctor.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IDataRecord), "record"));
            udtRow.udt_row_type_decl.Members.Add(ctor);
            CodeAssignStatement assign_record = new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "record"), new CodeVariableReferenceExpression("record"));
            ctor.Statements.Add(assign_record);

            CodeMemberMethod method_create = new CodeMemberMethod()
            {
                Name = "Create",
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                ReturnType = new CodeTypeReference(udtRow.udt_row_type_decl.Name)
            };
            method_create.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IDataRecord), "record"));
            var invoke_ctor = new CodeObjectCreateExpression(new CodeTypeReference(udtRow.udt_row_type_decl.Name), new CodeVariableReferenceExpression("record"));
            method_create.Statements.Add(new CodeMethodReturnStatement(invoke_ctor));
            udtRow.udt_row_type_decl.Members.Add(method_create);

            //foreach (var pi in mi.GetParameters())
            foreach (var pi in udt_reg.Columns)
            {
                CodeMemberProperty member_property = new CodeMemberProperty()
                {
                    Name = pi.ColumnName,
                    Attributes = MemberAttributes.Public | MemberAttributes.Final
                };

                var clrTypes = TypeMap.GetParameterClrType(pi.ColumnDbType, null);

                bool isNullable = pi.IsNullable;
                if (isNullable)
                {
                    if (udt_reg.PrimaryKey != null && udt_reg.PrimaryKey.ColumnNames.Contains(pi.ColumnName))
                    {
                        isNullable = false;
                    }
                }

                Type columnClrType = pi.IsNullable ? clrTypes.TypeNull : clrTypes.TypeNotNull;
                member_property.Type = new CodeTypeReference(columnClrType);

                udtRow.udt_row_type_decl.Members.Add(member_property);

                CodeIndexerExpression indexer = new CodeIndexerExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "record"), new_CodePrimitiveExpression(pi.ColumnName));

                CodeExpression var_value_ref;
                if (columnClrType.IsValueType)
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
        }

        private static string PrepareUdtRowName(StoreLakeTableTypeRegistration udt_reg)
        {
            string tableTypeDefinitionName = string.IsNullOrEmpty(udt_reg.TableTypeDefinitionName)
                ? udt_reg.TableTypeName
                : udt_reg.TableTypeDefinitionName;
            return tableTypeDefinitionName + "Row";
        }



        private static void InitializeStoreNamespaceName(DacPacRegistration dacpac, string storeSuffix)
        {
            string dacpacName = Path.GetFileNameWithoutExtension(dacpac.DacPacAssemblyLogicalName);
            string dacpacSetName = dacpacName;

            if (dacpacName.StartsWith("Helpline"))
            {
                dacpacName = dacpacName.Remove(0, 8);
                if (dacpacName.StartsWith("."))
                {
                    dacpacName = dacpacName.Remove(0, 1);
                }
            }

            string setName = dacpacSetName.Replace(".", "").Replace("-", ""); // type_decl.Name;
            dacpac.TestStoreExtensionSetName = setName;

            if (dacpacName == "")
            {
                dacpac.TestStoreAssemblyNamespace = "Helpline" + "." + storeSuffix; // .TestStore
            }
            else
            {
                dacpac.TestStoreAssemblyNamespace = "Helpline" + "." + dacpacName + "." + storeSuffix; // .TestStore
            }
        }

        private static void AddTableForeignKeys(AssemblyResolver assemblyResolver, CompilerParameters comparam, RegistrationResult rr, DacPacRegistration dacpac, ExtensionsClass exttype, CodeMemberMethod extensions_method_InitDataSet, string tableName)
        {
            DataTable type_decl_table = rr.ds.Tables[tableName];
            if (type_decl_table == null)
            {
                throw new StoreLakeSdkException("Table [" + tableName + "] could not be found.");
            }

            int count = type_decl_table.Constraints.Count;
            for (int i = 0; i < count; i++)
            {
                ForeignKeyConstraint fk = type_decl_table.Constraints[i] as ForeignKeyConstraint;
                if (fk != null)
                {
                    AddTableForeignKey(assemblyResolver, comparam, rr, dacpac, exttype, extensions_method_InitDataSet, type_decl_table, fk);
                }
            }
        }

        private static void AddTableForeignKey(AssemblyResolver assemblyResolver, CompilerParameters comparam, RegistrationResult rr, DacPacRegistration dacpac, ExtensionsClass exttype, CodeMemberMethod extensions_method_InitDataSet, DataTable type_decl_table, ForeignKeyConstraint fk)
        {
            //if (string.Equals(fk.Table.TableName, fk.RelatedTable.TableName))
            //{
            //    Console.WriteLine("SKIP:" + fk.ConstraintName);
            //    return;
            //}

            string foreignTable_Namespace;
            if (!IsTableOwnedByDacPac(rr, dacpac, fk.RelatedTable.TableName))
            {
                if (rr.registered_tables.TryGetValue(fk.RelatedTable.TableName, out DacPacRegistration foreign_dacpac))
                {
                    Assembly foreign_asm = TryReferencedTestStoreAssembly(foreign_dacpac);
                    AddReferencedAssembly(assemblyResolver, comparam, foreign_asm);

                    foreignTable_Namespace = foreign_dacpac.TestStoreAssemblyNamespace + ".";
                }
                else
                {
                    throw new NotSupportedException("" + fk.RelatedTable.TableName);
                }
            }
            else
            {
                // same as defining
                foreignTable_Namespace = "";
            }

            CodeTypeReference typeref_ForeignTable = new CodeTypeReference(foreignTable_Namespace + fk.RelatedTable.TableName + "DataTable");


            var method_GetTable_defining = new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(exttype.extensions_type_decl.Name),
                             exttype.extensions_method_GetTable.Name, new CodeTypeReference[] { new CodeTypeReference(fk.Table.TableName + "DataTable") });
            var method_GetTable_foreign = new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(exttype.extensions_type_decl.Name),
                            exttype.extensions_method_GetTable.Name, new CodeTypeReference[] { typeref_ForeignTable });

            var invoke_GetTable_defining = new CodeMethodInvokeExpression(method_GetTable_defining, new CodeExpression[] {
                new CodeVariableReferenceExpression("ds")
                , new CodePrimitiveExpression(fk.Table.TableName)
            });

            var invoke_GetTable_foreign = new CodeMethodInvokeExpression(method_GetTable_foreign, new CodeExpression[] {
                new CodeVariableReferenceExpression("ds")
                , new CodePrimitiveExpression(fk.RelatedTable.TableName)
            });

            var prop_If_Constraints = new CodePropertyReferenceExpression(invoke_GetTable_defining, "Constraints");
            var invoke_IndexOf = new CodeMethodInvokeExpression(prop_If_Constraints, "IndexOf", new CodeExpression[] { new CodePrimitiveExpression(fk.ConstraintName) });

            var ifTableExists = new CodeBinaryOperatorExpression(invoke_IndexOf, CodeBinaryOperatorType.LessThan, new_CodePrimitiveExpression(0));
            CodeConditionStatement if_not_Exists_Add = new CodeConditionStatement(ifTableExists);

            var decl_table_parent = new CodeVariableDeclarationStatement(typeref_ForeignTable, "table_parent");
            decl_table_parent.InitExpression = invoke_GetTable_foreign;
            if_not_Exists_Add.TrueStatements.Add(decl_table_parent);

            var ref_table_parent = new CodeVariableReferenceExpression(decl_table_parent.Name);
            var ref_table_parent_Columns = new CodePropertyReferenceExpression(ref_table_parent, "Columns");

            var decl_table_child = new CodeVariableDeclarationStatement(new CodeTypeReference(fk.Table.TableName + "DataTable"), "table_child");
            decl_table_child.InitExpression = invoke_GetTable_defining;
            if_not_Exists_Add.TrueStatements.Add(decl_table_child);

            List<CodeExpression> defining_columns = new List<CodeExpression>();
            List<CodeExpression> foreign_columns = new List<CodeExpression>();
            for (int ix = 0; ix < fk.Columns.Length; ix++)
            {
                var defining_column = fk.Columns[ix];
                var foreign_column = fk.RelatedColumns[ix];

                defining_columns.Add(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(decl_table_child.Name), defining_column.ColumnName + "Column"));
                //foreign_columns.Add(new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(decl_table_parent.Name), foreign_column.ColumnName + "Column"));

                var ref_table_parent_Columns_col = new CodeIndexerExpression(ref_table_parent_Columns, new CodeExpression[] { new CodePrimitiveExpression(foreign_column.ColumnName) });

                //var foreign_columnd_decl = new CodeVariableDeclarationStatement(typeof(DataColumn), "table_parent_" + foreign_column.ColumnName + "Column");
                //foreign_columnd_decl.InitExpression = ref_table_parent_Columns_col;
                //if_not_Exists_Add.TrueStatements.Add(foreign_columnd_decl);

                foreign_columns.Add(ref_table_parent_Columns_col);
            }

            CodeArrayCreateExpression definint_arr = new CodeArrayCreateExpression(typeof(DataColumn), defining_columns.ToArray());
            CodeArrayCreateExpression foreign_arr = new CodeArrayCreateExpression(typeof(DataColumn), foreign_columns.ToArray());
            var decl_fk = new CodeVariableDeclarationStatement(new CodeTypeReference(typeof(ForeignKeyConstraint)), "fk");

            decl_fk.InitExpression = new CodeObjectCreateExpression(decl_fk.Type, new CodeExpression[] {
                    new CodePrimitiveExpression(fk.ConstraintName),
                    foreign_arr,
                    definint_arr
            });

            if_not_Exists_Add.TrueStatements.Add(decl_fk);


            var prop_Constraints = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(decl_table_child.Name), "Constraints");

            var invoke_Add = new CodeMethodInvokeExpression(prop_Constraints, "Add", new CodeExpression[] { new CodeVariableReferenceExpression(decl_fk.Name) });


            if_not_Exists_Add.TrueStatements.Add(invoke_Add);

            extensions_method_InitDataSet.Statements.Add(if_not_Exists_Add);
        }

        private static Assembly TryReferencedTestStoreAssembly(DacPacRegistration foreign_dacpac)
        {
            if (string.IsNullOrWhiteSpace(foreign_dacpac.TestStoreAssemblyFullFileName))
            {
                throw new StoreLakeSdkException("Invalid file name for generated store of package : " + foreign_dacpac.DacPacAssemblyLogicalName);
            }

            AssemblyName asmName = AssemblyName.GetAssemblyName(foreign_dacpac.TestStoreAssemblyFullFileName);
            return Assembly.ReflectionOnlyLoad(asmName.ToString()); // Assembly.Load(asmName); // ReflectionOnly?
        }

        private static CodeExpression new_CodePrimitiveExpression(object value)
        {
            if (value != null && value is byte[])
            {
                throw new NotSupportedException();
            }
            return new CodePrimitiveExpression(value);
        }

        private static bool IsTableOwnedByDacPac(RegistrationResult rr, DacPacRegistration dacpac, string tableName)
        {
            if (dacpac.registered_tables.ContainsKey(tableName))
                return true;
            if (!rr.registered_tables.ContainsKey(tableName))
            {
                throw new StoreLakeSdkException("Table [" + tableName + "] could not be found.");
            }
            return false;
        }

        private static void Adjust_TypeDecl(RegistrationResult rr, DacPacRegistration dacpac, ExtensionsClass exttype, string fullNamespaceOrOwnerTypeName, CodeTypeDeclaration type_decl)
        {
            string fullClassName = fullNamespaceOrOwnerTypeName + "." + type_decl.Name;
            //Console.WriteLine("class " + fullClassName);

            //System.Data.DataSet
            bool isSetClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType == typeof(System.Data.DataSet).FullName;
            bool isTableClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType.Contains("System.Data.TypedTableBase");
            bool isRowClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType == typeof(System.Data.DataRow).FullName;

            if (isSetClassDeclaration)
            {
                type_decl.CustomAttributes.Clear();
                //type_decl.BaseTypes.Clear();
            }

            DataTable type_decl_table;
            string type_decl_rowName = null;
            if (isTableClassDeclaration)
            {
                string type_decl_tableName = type_decl.Name.Remove(type_decl.Name.Length - 9, 9); // suffix "DataTable"
                type_decl_table = rr.ds.Tables[type_decl_tableName];
                if (type_decl_table == null)
                {
                    throw new StoreLakeSdkException("Table [" + type_decl_tableName + "] could not be found.");
                }
            }
            else if (isRowClassDeclaration)
            {
                // extract table name from row class name
                string type_decl_tableName = type_decl.Name.Remove(type_decl.Name.Length - 3, 3); // suffix "Row"
                type_decl_table = rr.ds.Tables[type_decl_tableName];
                if (type_decl_table == null)
                {
                    throw new StoreLakeSdkException("Table [" + type_decl_tableName + "] could not be found.");
                }

                type_decl_rowName = type_decl.Name.Remove(type_decl.Name.Length - 3, 3); // suffix "Row"
            }
            else
            {
                type_decl_table = null;
            }

            List<CodeTypeMember> membersToRemove = new List<CodeTypeMember>();
            List<CodeTypeMember> membersToInsert = new List<CodeTypeMember>();
            foreach (CodeTypeMember member_decl in type_decl.Members)
            {
                //Console.WriteLine("--> " + fullClassName + " # (" + member_decl.GetType().Name + ") " + member_decl.Name + "  > " + AsTraceText(member_decl.Attributes));
                RemoveNoiseAttributes(member_decl);
                CodeTypeDeclaration member_type = member_decl as CodeTypeDeclaration;
                CodeMemberMethod member_method = member_decl as CodeMemberMethod;
                CodeConstructor member_ctor = member_decl as CodeConstructor;
                CodeMemberField member_field = member_decl as CodeMemberField;
                CodeMemberProperty member_property = member_decl as CodeMemberProperty;
                CodeMemberEvent member_event = member_decl as CodeMemberEvent;
                CodeTypeDelegate member_delegate = member_decl as CodeTypeDelegate; // hlmsgwebrequestRowChangeEventHandler
                if (member_type != null && member_delegate == null)
                {
                    //  
                    if (member_type.Name.EndsWith("RowChangeEvent"))
                    {
                        membersToRemove.Add(member_type);
                    }
                    else
                    {
                        bool isOwnedByDacPac = false;
                        if (isSetClassDeclaration)
                        {
                            if (member_type.Name.EndsWith("DataTable"))
                            {
                                string tableName = member_type.Name.Remove(member_type.Name.Length - 9, 9); // suffix "DataTable"
                                isOwnedByDacPac = IsTableOwnedByDacPac(rr, dacpac, tableName);
                            }
                            else
                            {
                                string tableName = member_type.Name.Remove(member_type.Name.Length - 3, 3); // suffix "Row"
                                isOwnedByDacPac = IsTableOwnedByDacPac(rr, dacpac, tableName);
                            }
                        }
                        else if (isTableClassDeclaration)
                        {
                            string tableName = member_type.Name.Remove(member_type.Name.Length - 9, 9); // suffix "DataTable"
                            isOwnedByDacPac = IsTableOwnedByDacPac(rr, dacpac, tableName);
                            throw new StoreLakeSdkException("NotImplemented:" + tableName);
                        }
                        else if (isRowClassDeclaration)
                        {
                            // extract table name from row class name
                            string tableName = member_type.Name.Remove(member_type.Name.Length - 3, 3);
                            isOwnedByDacPac = IsTableOwnedByDacPac(rr, dacpac, tableName);
                        }
                        else
                        {
                            throw new StoreLakeSdkException("NotImplemented:" + member_decl.Name + " - " + member_type.Name);
                        }

                        if (!isOwnedByDacPac)
                        {
                            // not part of this dacpac
                            membersToRemove.Add(member_decl);
                        }
                        else
                        {

                            Adjust_TypeDecl(rr, dacpac, exttype, fullClassName, member_type);
                        }
                    }
                }
                if (member_delegate != null)
                {
                    //Console.WriteLine(fullClassName + " # (" + member_decl.GetType().Name + ") " + member_decl.Name + "  > " + AsTraceText(member_decl.Attributes));
                    membersToRemove.Add(member_delegate);
                }
                if (member_ctor != null)
                {
                    if (isSetClassDeclaration && member_ctor.Parameters.Count == 0)
                    {
                        // default ctor => static method on 'DataSet ds'
                        CodeTypeMember member_decl_x = Adjust_DataSet_Constructor(exttype.extensions_type_decl.Name, member_ctor);
                        if (member_decl_x != null)
                        {
                            exttype.extensions_type_decl.Members.Add(member_decl_x);
                            //membersToInsert.Add(member_decl_x);
                            membersToRemove.Add(member_decl);
                        }
                    }
                    if (!isSetClassDeclaration && member_ctor.Attributes.IsPublic())
                    {
                        member_ctor.Attributes = MemberAttributes.Assembly;
                    }
                    if (member_ctor.Parameters.Count == 1 && member_ctor.Attributes.IsPublic())
                    {
                        membersToRemove.Add(member_ctor);
                    }
                    if (member_ctor.Parameters.Count > 1)
                    {
                        membersToRemove.Add(member_ctor);
                    }

                    if (isTableClassDeclaration)
                    {
                        if (member_ctor.Parameters.Count > 0)
                        {
                            //  hlbiattributeconfigDataTable(DataTable table)
                            if (!membersToRemove.Contains(member_ctor))
                            {
                                membersToRemove.Add(member_ctor);
                            }
                        }
                        else
                        {
                            Adjust_Table_Constructor(member_ctor);
                        }
                    }
                }
                if (member_method != null)
                {
                    //Console.WriteLine(fullClassName + " # (" + member_decl.GetType().Name + ") " + member_decl.Name + "  > " + AsTraceText(member_decl.Attributes));
                    // ? ShouldSerialize
                    // ? GetTypedDataSetSchema
                    // ? SchemaChanged
                    if (member_method.Attributes.IsPublic() && !member_method.Attributes.IsOverride() && !(member_ctor != null))
                    {
                        if (member_method.Attributes.IsStatic())
                        {
                            member_method.Attributes = MemberAttributes.Final | MemberAttributes.Private | MemberAttributes.Static;
                        }
                        else
                        {
                            member_method.Attributes = MemberAttributes.Final | MemberAttributes.Private;
                        }

                    }

                    if (member_method.Name.EndsWith("RowChanged")
                        || member_method.Name.EndsWith("RowChanging")
                        || member_method.Name.EndsWith("RowDeleted")
                        || member_method.Name.EndsWith("RowDeleting")
                        || member_method.Name.EndsWith("GetTypedTableSchema")
                        //|| member_method.Name.EndsWith("Clone")
                        || member_method.Name.EndsWith("ReadXmlSerializable")
                        || member_method.Name.EndsWith("GetSchemaSerializable")
                        || member_method.Name.EndsWith("GetTypedDataSetSchema")
                        || member_method.Name.EndsWith("InitializeDerivedDataSet")
                        || member_method.Name.EndsWith("SchemaChanged") // used in ctor 
                        || (member_method.Name.EndsWith("Clone") && isSetClassDeclaration)
                        || (member_method.Name.EndsWith("Clone") && isTableClassDeclaration)
                        || member_method.Name.EndsWith("InitVars")
                        )
                    {
                        //Console.WriteLine(fullClassName + " # (" + member_decl.GetType().Name + ") " + member_decl.Name + "  > " + AsTraceText(member_decl.Attributes));
                        membersToRemove.Add(member_method);
                    }

                    if (isRowClassDeclaration)
                    {
                        if (member_method.Name.StartsWith("Is") && member_method.Name.EndsWith("Null") && member_method.ReturnType != null && member_method.ReturnType.BaseType == typeof(System.Boolean).FullName)
                        {
                            // return IsNull(xxx_table.yyy_Column);
                            membersToRemove.Add(member_method);
                        }
                        else if (member_method.Name.StartsWith("Set") && member_method.Name.EndsWith("Null") && member_method.ReturnType != null && member_method.ReturnType.BaseType == typeof(void).FullName)
                        {
                            // base[xxx_table.yyy_Column] = Convert.DBNull;
                            membersToRemove.Add(member_method);
                        }
                    }
                    if (isTableClassDeclaration)
                    {
                        if (member_method.Name == "CreateInstance")
                        {
                            // protected override DataTable CreateInstance() : called by 'DataTable.Clone'
                            membersToRemove.Add(member_method);
                        }
                        if (member_method.Name.StartsWith("New") && member_method.Name.EndsWith("Row") && member_method.ReturnType.BaseType != typeof(void).FullName && member_method.Attributes.IsPrivate() && member_method.Parameters.Count == 0)
                        {
                            // public void AddhlbiattributeconfigRow(hlbiattributeconfigRow row)
                            membersToRemove.Add(member_method);
                        }

                        if (member_method.Name.StartsWith("Add") && member_method.Name.EndsWith("Row") && member_method.ReturnType != null && member_method.ReturnType.BaseType == typeof(void).FullName)
                        {
                            // public void AddhlbiattributeconfigRow(hlbiattributeconfigRow row)
                            membersToRemove.Add(member_method);
                        }

                        if (member_method.Name.StartsWith("Add") && member_method.Name.EndsWith("Row") && member_method.ReturnType != null && member_method.ReturnType.BaseType != typeof(void).FullName)
                        {
                            // public hlcmdatamodelassociationsearchRow AddhlcmdatamodelassociationsearchRow(int associationid, int searchid)
                            Adjust_Table_AddRowWithValues(rr, dacpac, type_decl_table, member_method);
                        }

                        if (member_method.Name.StartsWith("FindBy") && member_method.ReturnType.BaseType.EndsWith("Row"))
                        {
                            // private hlcmdatamodelassociationsearchRow FindByassociationid(int associationid) => FindRowByPrimaryKey
                            member_method.Name = "FindRowByPrimaryKey";
                            member_method.Attributes = MemberAttributes.Public | MemberAttributes.Final;
                        }

                        if (member_method.Name == "InitClass")
                        {
                            Adjust_Table_InitClass(rr, dacpac, type_decl_table, member_method);
                        }

                    }

                    if (isSetClassDeclaration && member_method.Name.StartsWith("ShouldSerialize") && member_method.Parameters.Count == 0 && member_method.ReturnType != null && member_method.ReturnType.BaseType == typeof(bool).FullName)
                    {
                        // bool ShouldSerializehlsysrecentsearch()
                        membersToRemove.Add(member_method);
                    }

                    if (isSetClassDeclaration && member_method.Name == "InitClass")
                    {
                        Adjust_DataSet_InitClass(rr, dacpac, member_method);
                        exttype.extensions_type_decl.Members.Add(member_method);
                        membersToRemove.Add(member_method);
                    }
                }
                else if (member_property != null)
                {
                    // ? Tables
                    // ? Relations
                    if (member_property.Type != null && member_property.Type.BaseType == typeof(DataColumn).FullName && member_property.Attributes.IsPublic())
                    {
                        // 24578
                        member_property.Attributes = MemberAttributes.Assembly | MemberAttributes.Final; // make the Table.Column properties internal(visible only for ...Row).
                        member_property.CustomAttributes.Clear();
                    }

                    if (isTableClassDeclaration && member_property.HasGet && !member_property.HasSet && member_property.Name == "Item") // indexer
                    {
                        membersToRemove.Add(member_property);
                    }

                    if (isSetClassDeclaration)
                    {
                        if (member_property.Name == "Tables"
                         || member_property.Name == "Relations"
                         || member_property.Name == "SchemaSerializationMode"
                       )
                        {
                            membersToRemove.Add(member_property);
                        }
                    }

                    if (isSetClassDeclaration && member_property.HasGet && !member_property.HasSet && member_property.Type.BaseType.Contains(member_property.Name) && member_property.Type.BaseType.EndsWith("DataTable"))
                    {
                        string tableName = member_property.Name;
                        bool isOwnedByDacPac = IsTableOwnedByDacPac(rr, dacpac, member_property.Name);
                        if (!isOwnedByDacPac)
                        {
                            // not part of this dacpac
                            membersToRemove.Add(member_property);
                        }
                        else
                        {
                            CodeMemberMethod member_method_x = Adjust_DataSet_TableAccessor(exttype, member_property);
                            //if (member_method_x != null)
                            {
                                exttype.extensions_type_decl.Members.Add(member_method_x);
                                membersToRemove.Add(member_property);
                            }
                        }
                    }

                    if (isRowClassDeclaration && member_property.Attributes.IsPublic())
                    {
                        Adjust_Row_Column_Accessor(type_decl_table, member_property);
                    }
                }
                else if (member_event != null)
                {
                    // parent: Table
                    if (member_event.Name.EndsWith("RowChanged")
                        || member_event.Name.EndsWith("RowChanging")
                        || member_event.Name.EndsWith("RowDeleted")
                        || member_event.Name.EndsWith("RowDeleting")
                        )
                    {
                        //Console.WriteLine(fullClassName + " # (" + member_decl.GetType().Name + ") " + member_decl.Name + "  > " + AsTraceText(member_decl.Attributes));
                        membersToRemove.Add(member_event);
                    }
                }
                else if (member_field != null)
                {

                    if (isSetClassDeclaration)
                    {
                        if (member_field.Name == "_schemaSerializationMode"
                       )
                        {
                            membersToRemove.Add(member_field);
                        }
                        else
                        {
                            membersToRemove.Add(member_field);
                        }
                    }
                }


            }

            if (isRowClassDeclaration)
            {
                AddRowMethod_ValidateCheckConstraints(rr, dacpac, type_decl_table, type_decl);
            }

            if (isTableClassDeclaration)
            {
                AddTableMethod_DeleteRowByPrimaryKey(rr, dacpac, type_decl_table, type_decl);
                AddTableMethod_ValidateCheckConstraints(rr, dacpac, type_decl_table, type_decl);
            }

            foreach (var memberToRemove in membersToRemove)
            {
                if (type_decl.Members.IndexOf(memberToRemove) < 0)
                {
                    // already removed
                }
                else
                {
                    type_decl.Members.Remove(memberToRemove);
                }
            }

            foreach (var memberToInsert in membersToInsert)
            {
                type_decl.Members.Add(memberToInsert);
            }
        }
        private static void AddRowMethod_ValidateCheckConstraints(RegistrationResult rr, DacPacRegistration dacpac, DataTable type_decl_table, CodeTypeDeclaration row_type_decl)
        {
            CodeMemberMethod method_validate_row = new CodeMemberMethod()
            {
                Name = row_type_method_ValidateRow_Name,
                Attributes = MemberAttributes.Assembly | MemberAttributes.Final
            };
            row_type_decl.Members.Add(method_validate_row);

            var cks = dacpac.registered_CheckConstraints.Values.Where(x => x.DefiningTableName == type_decl_table.TableName).ToArray();
            if (cks.Length == 0)
            {
            }
            else
            {
                foreach (var ck in cks)
                {
                    RegisterRowCheckConstraint(type_decl_table.DataSet.Namespace, type_decl_table, row_type_decl, method_validate_row, ck);
                }
            }
        }

        private static void RegisterRowCheckConstraint(string schemaName, DataTable table, CodeTypeDeclaration row_type_decl, CodeMemberMethod method_validate_row, StoreLakeCheckConstraintRegistration ck)
        {
            var ck_method = AddRowMethodValidateCheckConstraint(schemaName, table, ck);
            row_type_decl.Members.Add(ck_method);

            CodeMethodInvokeExpression invoke_ck = new CodeMethodInvokeExpression();
            invoke_ck.Method = new CodeMethodReferenceExpression() { MethodName = ck_method.Name, TargetObject = new CodeThisReferenceExpression() };

            CodeConditionStatement if_ck = new CodeConditionStatement(new CodeBinaryOperatorExpression(new CodePrimitiveExpression(false), CodeBinaryOperatorType.ValueEquality, invoke_ck));
            if_ck.TrueStatements.Add(new CodeThrowExceptionStatement(new CodeObjectCreateExpression(typeof(ConstraintException), new CodePrimitiveExpression(ck.CheckConstraintName))));

            method_validate_row.Statements.Add(if_ck);
        }

        private const string row_type_method_ValidateRow_Name = "ValidateRow";
        private static void AddTableMethod_ValidateCheckConstraints(RegistrationResult rr, DacPacRegistration dacpac, DataTable type_decl_table, CodeTypeDeclaration table_type_decl)
        {
            CodeMemberMethod method_decl_OnColumnChanging = new CodeMemberMethod()
            {
                Name = "OnColumnChanging",
                Attributes = MemberAttributes.Override | MemberAttributes.Family,
                ReturnType = new CodeTypeReference(typeof(void))
            };
            method_decl_OnColumnChanging.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(DataColumnChangeEventArgs)), "e"));

            CodeMemberMethod method_decl_OnRowChanging = new CodeMemberMethod()
            {
                Name = "OnRowChanging",
                Attributes = MemberAttributes.Override | MemberAttributes.Family,
                ReturnType = new CodeTypeReference(typeof(void))
            };
            method_decl_OnRowChanging.Parameters.Add(new CodeParameterDeclarationExpression(typeof(DataRowChangeEventArgs), "e"));

            string row_type_decl_Name = type_decl_table.TableName + "Row";


            var e_RowBase = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("e"), "Row");
            CodeCastExpression e_RowT = new CodeCastExpression(new CodeTypeReference(row_type_decl_Name), e_RowBase);
            var invoke_row_validate = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(e_RowT, row_type_method_ValidateRow_Name));
            method_decl_OnRowChanging.Statements.Add(invoke_row_validate);


            var cks = dacpac.registered_CheckConstraints.Values.Where(x => x.DefiningTableName == type_decl_table.TableName).ToArray();
            if (cks.Length == 0)
            {
                method_decl_OnColumnChanging.Statements.Add(new CodeCommentStatement("No check contraints defined."));
                table_type_decl.Members.Add(method_decl_OnColumnChanging);
            }
            else
            {
                foreach (var ck in cks)
                {
                    //AddTableValidateCheckConstraints(method_decl_OnColumnChanging, ck);
                }

                //type_decl.Members.Add(method_decl_OnColumnChanging);
                table_type_decl.Members.Add(method_decl_OnRowChanging);
            }
        }

        private static CodeMemberMethod AddRowMethodValidateCheckConstraint(string schemaName, DataTable table, StoreLakeCheckConstraintRegistration ck)
        {
            CodeExpression codeExpr = null;
            try
            {

                codeExpr = SqlDom.BooleanExpressionGenerator.BuildFromCheckConstraintDefinition(schemaName, table, ck.CheckConstraintName, ck.CheckExpressionScript, out bool hasError, out string errorText);
                if (hasError)
                {
                    //s_tracer.TraceEvent(TraceEventType.Warning, 0, "CHECK CONSTRAINT [" + ck.CheckConstraintName + "] generation failed." + errorText);
                    //Console.WriteLine("DEFINITION: " + ck.CheckExpressionScript);
                    codeExpr = new CodePrimitiveExpression(true);
                }
                //if (codeExpr == null)
                {
                    codeExpr = new CodePrimitiveExpression(true);
                }
            }
            catch (Exception ex)
            {
                s_tracer.TraceEvent(TraceEventType.Warning, 0, "CHECK CONSTRAINT [" + ck.CheckConstraintName + "] generation failed." + ex);
                Console.WriteLine("DEFINITION: " + ck.CheckExpressionScript);
                codeExpr = new CodePrimitiveExpression(true);
            }


            //codeExpr = new CodePrimitiveExpression(true);

            CodeMemberMethod ck_method = new CodeMemberMethod()
            {
                Name = ck.CheckConstraintName,
                Attributes = MemberAttributes.Private,
                ReturnType = new CodeTypeReference(typeof(bool))
            };
            ck_method.Statements.Add(new CodeMethodReturnStatement(codeExpr));

            return ck_method;
        }

        /*private static void AddTableValidateCheckConstraints(CodeMemberMethod method_decl_OnColumnChanging, StoreLakeCheckConstraintRegistration ck)
        {
            method_decl_OnColumnChanging.Statements.Add(new CodeCommentStatement(ck.CheckConstraintName + "  " + ck.CheckExpressionScript));

            var e_Column = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("e"), "Columns");
            var e_Column_ColumnName = new CodePropertyReferenceExpression(e_Column, "ColumnName");

            List<CodeBinaryOperatorExpression> conditions = new List<CodeBinaryOperatorExpression>();
            foreach (var col in ck.DefiningColumns)
            {
                //method_decl_OnColumnChanging.Statements.Add(new CodeCommentStatement(" + " + col.ColumnName));

                var colref = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "column" + col.ColumnName);
                var conditionExpr = new CodeBinaryOperatorExpression(
                        e_Column,
                        CodeBinaryOperatorType.ValueEquality,
                        colref);
                conditions.Add(conditionExpr);
            }
            if (conditions.Count == 1)
            {
                //CodeConditionStatement if_Columns = new CodeConditionStatement();
                // throw ConstraintException
                //throw new NotImplementedException(ck.CheckConstraintName + " : " + ck.CheckExpressionScript);
            }
            else
            {
                //throw new NotImplementedException(ck.CheckConstraintName + " : " + ck.CheckExpressionScript);
            }
        }*/

        private static void AddTableMethod_DeleteRowByPrimaryKey(RegistrationResult rr, DacPacRegistration dacpac, DataTable table, CodeTypeDeclaration type_decl)
        {
            CodeMemberMethod member_FindRowByPrimaryKey = TryGetMemberMethodByName(type_decl, "FindRowByPrimaryKey");
            if (member_FindRowByPrimaryKey == null)
            {
                //throw new StoreLakeSdkException("Member not found 'FindRowByPrimaryKey' for type:" + type_decl.Name);
                // no primary key
            }
            else
            {




                CodeMemberMethod member_DeleteRowByPrimaryKey = new CodeMemberMethod();
                member_DeleteRowByPrimaryKey.Name = "DeleteRowByPrimaryKey";
                member_DeleteRowByPrimaryKey.Attributes = MemberAttributes.Public | MemberAttributes.Final;
                member_DeleteRowByPrimaryKey.ReturnType = new CodeTypeReference(type_decl.Name);

                // parameters
                CodeMethodInvokeExpression invokeFind = new CodeMethodInvokeExpression(
                    new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), member_FindRowByPrimaryKey.Name)
                    );

                for (int ix = 0; ix < member_FindRowByPrimaryKey.Parameters.Count; ix++)
                {
                    CodeParameterDeclarationExpression prm = member_FindRowByPrimaryKey.Parameters[ix];

                    member_DeleteRowByPrimaryKey.Parameters.Add(prm);

                    invokeFind.Parameters.Add(new CodeVariableReferenceExpression(prm.Name));
                }

                // body : FindRowByPrimaryKey(identifier).Delete();

                CodeMethodInvokeExpression invokeDelete = new CodeMethodInvokeExpression(
                    new CodeMethodReferenceExpression(invokeFind, "Delete")
                    );
                member_DeleteRowByPrimaryKey.Statements.Add(invokeDelete);

                // return
                member_DeleteRowByPrimaryKey.Statements.Add(new CodeMethodReturnStatement(new CodeThisReferenceExpression()));
                // dont added it - not so useful type_decl.Members.Add(member_DeleteRowByPrimaryKey);
            }
        }

        private static CodeMemberMethod TryGetMemberMethodByName(CodeTypeDeclaration type_decl, string methodName)
        {
            for (int ix = 0; ix < type_decl.Members.Count; ix++)
            {
                CodeTypeMember member = type_decl.Members[ix];
                if (member.Name == methodName)
                {
                    return (CodeMemberMethod)member;
                }
            }

            return null;
        }

        private static void Adjust_Table_Constructor(CodeConstructor member_ctor)
        {
            foreach (var stmt in member_ctor.Statements)
            {
                CodeExpressionStatement stmt_expr = stmt as CodeExpressionStatement;
                if (stmt_expr != null)
                {
                    CodeMethodInvokeExpression invoke_expr = stmt_expr.Expression as CodeMethodInvokeExpression;
                    if (invoke_expr != null)
                    {
                        if (invoke_expr.Method.MethodName == "InitClass")
                        {
                            invoke_expr.Method.MethodName = "InitTableClass";
                        }
                    }
                }
            }
        }

        private static void Adjust_Row_Column_Accessor(DataTable table, CodeMemberProperty member_property)
        {
            string column_name = member_property.Name;
            DataColumn column = table.Columns[column_name];
            if (column == null)
            {
                if (column_name == "_namespace")
                {
                    column_name = "namespace"; // see [hlsysnamespace].
                }
                else if (column_name == "_operator")
                {
                    column_name = "operator"; // see [hlbpmruleconditionclause].
                }
                else if (column_name == "_readonly")
                {
                    column_name = "readonly"; // see [hlbpmtaskmngmtattr].
                }
                column = table.Columns[column_name];
                if (column == null)
                {
                    throw new StoreLakeSdkException("Column [" + table.TableName + "] '" + member_property.Name + "' could not be found.");
                }
            }

            if (column.AllowDBNull)
            {
                if (column.DataType == typeof(byte[]))
                {
                }
                if (column.DataType.IsValueType || column.DataType == typeof(string) || column.DataType == typeof(byte[]))
                {
                    Adjust_Row_Column_Accessor_Nullable_Get(table.TableName, column, member_property);
                    Adjust_Row_Column_Accessor_Nullable_Set(table.TableName, column, member_property);
                } // IsValueType
                else
                {
                    throw new NotImplementedException(column.DataType.Name);
                }
            }// AllowDBNull
        }

        private static void Adjust_Row_Column_Accessor_Nullable_Set(string tableName, DataColumn column, CodeMemberProperty member_property)
        {
            var DBNull_Value = new CodePropertyReferenceExpression(new CodeTypeReferenceExpression(typeof(DBNull)), "Value");

            CodeAssignStatement assign_base_ColumnValue_to_ParameterValue = (CodeAssignStatement)member_property.SetStatements[0];
            CodeAssignStatement assign_base_ColumnValue_to_DBNull = new CodeAssignStatement(assign_base_ColumnValue_to_ParameterValue.Left, DBNull_Value);

            CodePropertySetValueReferenceExpression prm_value_ref = new CodePropertySetValueReferenceExpression();

            var null_expr = new_CodePrimitiveExpression(null);
            var conditionExpr =
                new CodeBinaryOperatorExpression(prm_value_ref,
                    CodeBinaryOperatorType.ValueEquality,
                    null_expr);

            CodeConditionStatement ifNull_set_DBNull = new CodeConditionStatement(condition: conditionExpr
                , trueStatements: new CodeStatement[] {
                    assign_base_ColumnValue_to_DBNull
                }
                , falseStatements: new CodeStatement[] {
                    assign_base_ColumnValue_to_ParameterValue
                }
                );

            member_property.SetStatements.Clear();
            member_property.SetStatements.Add(ifNull_set_DBNull);

            /*
                        CodeFieldReferenceExpression field_table_ref = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "table" + tableName);
                        CodePropertyReferenceExpression table_col_ref = new CodePropertyReferenceExpression(field_table_ref, member_property.Name + "Column");

                        CodeIndexerExpression indexer = new CodeIndexerExpression(new CodeBaseReferenceExpression(), table_col_ref);

                        CodeVariableDeclarationStatement var_value_decl = new CodeVariableDeclarationStatement(typeof(object), "raw_value", indexer);
                        CodeVariableReferenceExpression var_value_ref = new CodeVariableReferenceExpression(var_value_decl.Name);
                        var conditionExpr =
                            new CodeBinaryOperatorExpression(prm_value_ref,
                                CodeBinaryOperatorType.ValueEquality,
                                new CodePropertyReferenceExpression(new CodeTypeReferenceExpression(typeof(DBNull)), "Value"));

                        CodeConditionStatement ifNull_return_Null = new CodeConditionStatement(conditionExpr, new CodeMethodReturnStatement(new_CodePrimitiveExpression(null)));
                        member_property.GetStatements.Clear();

                        member_property.GetStatements.Add(var_value_decl);
                        member_property.GetStatements.Add(ifNull_return_Null);
                        member_property.GetStatements.Add(new CodeMethodReturnStatement(new CodeCastExpression(value_type, var_value_ref)));
            */
            //throw new NotImplementedException("[" + tableName + "] (" + column.ColumnName + ") " + column.DataType.Name);
        }

        private static void Adjust_Row_Column_Accessor_Nullable_Get(string tableName, DataColumn column, CodeMemberProperty member_property)
        {
            CodeTypeReference value_type;
            if (column.DataType == typeof(byte[]) || column.DataType == typeof(string))
            {
                //old_prop_type = column.DataType;
                value_type = new CodeTypeReference(column.DataType);
            }
            else
            {
                if (TypeMap.notnull_nullable_map.TryGetValue(column.DataType.FullName, out string nullableType))
                {
                    //Adjust_Row_Column_Accessor_Nullable_Get(table.TableName, column.DataType, nullableTypeName, member_property);
                }
                else
                {
                    throw new StoreLakeSdkException("NotImplemented:" + "Row property type for nullable column  [" + tableName + "] '" + column.ColumnName + "' (" + column.DataType.Name + ")=[" + member_property.Type.BaseType + "]");
                }

                member_property.Type = new CodeTypeReference(nullableType);

                value_type = new CodeTypeReference(member_property.Type.BaseType);
            }

            CodeFieldReferenceExpression field_table_ref = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "table" + tableName);
            CodePropertyReferenceExpression table_col_ref = new CodePropertyReferenceExpression(field_table_ref, member_property.Name + "Column");

            CodeIndexerExpression indexer = new CodeIndexerExpression(new CodeBaseReferenceExpression(), table_col_ref);

            CodeVariableDeclarationStatement var_value_decl = new CodeVariableDeclarationStatement(typeof(object), "raw_value", indexer);
            CodeVariableReferenceExpression var_value_ref = new CodeVariableReferenceExpression(var_value_decl.Name);
            var conditionExpr =
                new CodeBinaryOperatorExpression(var_value_ref,
                    CodeBinaryOperatorType.ValueEquality,
                    new CodePropertyReferenceExpression(new CodeTypeReferenceExpression(typeof(DBNull)), "Value"));

            ///var conditionExpr2 = new CodeBinaryOperatorExpression(var_value_ref,
            ///        CodeBinaryOperatorType.ValueEquality,
            ///        new_CodePrimitiveExpression("null));
            ///

            CodeConditionStatement ifNull_return_Null = new CodeConditionStatement(conditionExpr, new CodeMethodReturnStatement(new_CodePrimitiveExpression(null)));
            member_property.GetStatements.Clear();

            member_property.GetStatements.Add(var_value_decl);
            member_property.GetStatements.Add(ifNull_return_Null);
            member_property.GetStatements.Add(new CodeMethodReturnStatement(new CodeCastExpression(value_type, var_value_ref)));
        }

        private static void Adjust_Table_AddRowWithValues(RegistrationResult rr, DacPacRegistration dacpac, DataTable table, CodeMemberMethod member_method)
        {
            member_method.Name = "AddRowWithValues";
            member_method.Attributes = MemberAttributes.Public | MemberAttributes.Final;
            // put the columns with defaults at the end
            // 1. columns_pk (ASC)
            // 2. columns_required (ASC)
            // 3. columns_nullable no defaults (ASC)
            // 4. columns_nullable no defaults (ASC)
            // no value
            // with default value
            List<CodeParameterDeclarationExpression> parameters_nval = new List<CodeParameterDeclarationExpression>();
            List<CodeParameterDeclarationExpression> parameters_dval = new List<CodeParameterDeclarationExpression>();
            foreach (CodeParameterDeclarationExpression prm_decl in member_method.Parameters)
            {
                string column_name = prm_decl.Name;
                DataColumn column = table.Columns[column_name];
                if (column == null)
                {
                    if (column_name == "_namespace")
                    {
                        column_name = "namespace"; // see [hlsysnamespace].
                    }
                    else if (column_name == "_operator")
                    {
                        column_name = "operator"; // see [hlbpmruleconditionclause].
                    }
                    else if (column_name == "_readonly")
                    {
                        column_name = "readonly"; // see [hlbpmtaskmngmtattr].
                    }
                    column = table.Columns[column_name];
                    if (column == null)
                    {
                        throw new StoreLakeSdkException("Column [" + table.TableName + "] '" + prm_decl.Name + "' could not be found.");
                    }
                }

                if (column.AllowDBNull)
                {
                    if (column.DataType.IsValueType)
                    {
                        if (column.DataType == typeof(int) && prm_decl.Type.BaseType == typeof(int).FullName)
                        {
                            // [hlhistorychange](datalistitemid)
                            prm_decl.Type = new CodeTypeReference("int?");
                        }
                        else if (column.DataType == typeof(long) && prm_decl.Type.BaseType == typeof(long).FullName)
                        {
                            prm_decl.Type = new CodeTypeReference("long?");
                        }
                        else if (column.DataType == typeof(short) && prm_decl.Type.BaseType == typeof(short).FullName)
                        {
                            prm_decl.Type = new CodeTypeReference("short?");
                        }
                        else if (column.DataType == typeof(byte) && prm_decl.Type.BaseType == typeof(byte).FullName)
                        {
                            prm_decl.Type = new CodeTypeReference("byte?");
                        }
                        else if (column.DataType == typeof(bool) && prm_decl.Type.BaseType == typeof(bool).FullName)
                        {
                            prm_decl.Type = new CodeTypeReference("bool?");
                        }
                        else if (column.DataType == typeof(DateTime) && prm_decl.Type.BaseType == typeof(DateTime).FullName)
                        {
                            prm_decl.Type = new CodeTypeReference("System.DateTime?");
                        }
                        else if (column.DataType == typeof(decimal) && prm_decl.Type.BaseType == typeof(decimal).FullName)
                        {
                            prm_decl.Type = new CodeTypeReference("decimal?");
                        }
                        else if (column.DataType == typeof(Single) && prm_decl.Type.BaseType == typeof(Single).FullName)
                        {
                            // [hlsysbaselineattr] 'numbermin
                            prm_decl.Type = new CodeTypeReference("System.Single?");
                        }
                        else if (column.DataType == typeof(Guid) && prm_decl.Type.BaseType == typeof(Guid).FullName)
                        {
                            prm_decl.Type = new CodeTypeReference("System.Guid?");
                        }
                        else if (
                           (column.DataType == typeof(int) && prm_decl.Type.BaseType == typeof(int?).FullName)
                        || (column.DataType == typeof(string) && prm_decl.Type.BaseType == typeof(string).FullName)
                                )
                        {
                            // ok
                        }
                        else
                        {
                            throw new StoreLakeSdkException("NotImplemented:" + "Parameter type for nullable column  [" + table.TableName + "] '" + column.ColumnName + "' (" + column.DataType.Name + ")=[" + prm_decl.Type.BaseType + "]");
                        }
                    }
                }


                bool defaultParameterValue_HasValue = false;
                object defaultParameterValue = null; // null means dont specified anything for this parameter (DBNull.Value)
                if (column.DefaultValue != null)
                {
                    defaultParameterValue_HasValue = true;
                    if (column.DefaultValue == DBNull.Value)
                    {
                        if (!column.AllowDBNull)
                        {
                            defaultParameterValue_HasValue = false;
                        }
                        else
                        {
                            defaultParameterValue = null;
                        }
                    }
                    else if (column.DefaultValue is bool)
                    {
                        defaultParameterValue = (bool)column.DefaultValue;
                    }
                    else if (column.DefaultValue is long)
                    {
                        defaultParameterValue = (long)column.DefaultValue;
                    }
                    else if (column.DefaultValue is int)
                    {
                        defaultParameterValue = (int)column.DefaultValue;
                    }
                    else if (column.DefaultValue is short)
                    {
                        defaultParameterValue = (short)column.DefaultValue;
                    }
                    else if (column.DefaultValue is byte)
                    {
                        defaultParameterValue = (byte)column.DefaultValue;
                    }
                    else if (column.DefaultValue is decimal)
                    {
                        defaultParameterValue = (decimal)column.DefaultValue;
                    }
                    else if (column.DefaultValue is DateTime)
                    {
                        //var dt = (DateTime)column.DefaultValue;
                        //var kind = new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(dt.Kind.GetType()), Enum.GetName(dt.Kind.GetType(), dt.Kind));
                        //defaultParameterValue = new CodeObjectCreateExpression(typeof(System.DateTime), new_CodePrimitiveExpression(dt.Ticks), kind);
                        defaultParameterValue = (DateTime)column.DefaultValue;
                    }
                    else if (column.DefaultValue is string)
                    {
                        defaultParameterValue = (string)column.DefaultValue;
                    }
                    else if (column.DefaultValue is byte[])
                    {
                        defaultParameterValue = (byte[])column.DefaultValue;
                    }
                    else
                    {
                        throw new StoreLakeSdkException("NotImplemented:" + "Column [" + table.TableName + "] '" + prm_decl.Name + " (" + column.DefaultValue.GetType().Name + ")=[" + column.DefaultValue + "]");
                    }
                }
                else
                {
                    //table.Constraints
                }

                if (defaultParameterValue_HasValue && defaultParameterValue == null && column.AllowDBNull)
                {
                    if (column.DataType == typeof(int) && prm_decl.Type.BaseType == typeof(int).FullName)
                    {
                        // [hlhistorychange](datalistitemid)
                    }
                }

                if (defaultParameterValue_HasValue)
                {
                    bool hasDefaultValue = false;
                    //CodeExpression expr = defaultParameterValue as CodeExpression;
                    //if (expr != null)
                    //{
                    //    // DateTime   
                    //}
                    //else
                    //{
                    //if (defaultParameterValue == null)
                    //{
                    //    expr = new_CodePrimitiveExpression(null);
                    //}
                    //else
                    //{
                    //expr = new_CodePrimitiveExpression(defaultParameterValue);

                    string codeText = null;
                    if (defaultParameterValue == null)
                    {
                        codeText = "null";
                    }
                    else if (defaultParameterValue is string)
                    {
                        codeText = "\"" + defaultParameterValue + "\"";
                    }
                    else if (defaultParameterValue is bool)
                    {
                        codeText = ((bool)defaultParameterValue) ? "true" : "false";
                    }
                    else if (defaultParameterValue is int)
                    {
                        codeText = ((int)defaultParameterValue).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (defaultParameterValue is short)
                    {
                        codeText = ((short)defaultParameterValue).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (defaultParameterValue is long)
                    {
                        codeText = ((long)defaultParameterValue).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (defaultParameterValue is byte)
                    {
                        codeText = ((byte)defaultParameterValue).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (defaultParameterValue is decimal)
                    {
                        codeText = ((decimal)defaultParameterValue).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (defaultParameterValue is DateTime)
                    {
                        var dt = (DateTime)column.DefaultValue;
                        var kind = Enum.GetName(dt.Kind.GetType(), dt.Kind);
                        //codeText = string.Format(CultureInfo.InvariantCulture, "new System.DateTime({0}, System.DateTimeKind.{1})", dt.Ticks, dt.Kind);
                    }
                    else if (defaultParameterValue is byte[])
                    {
                        //byte[] valueBytes = (byte[])defaultParameterValue;
                        //if (valueBytes.Length == 0)
                        //{
                        //    codeText = "new byte[0]";
                        //}
                        //else
                        //{
                        //    throw new StoreLakeSdkException("NotImplemented DefaultValue is not empty byte array:" + "Column [" + table.TableName + "] '" + prm_decl.Name + " (" + column.DefaultValue.GetType().Name + ")=[" + column.DefaultValue + "]");
                        //}
                        codeText = null;
                    }
                    else
                    {
                        throw new StoreLakeSdkException("NotImplemented DefaultValue :" + "Column [" + table.TableName + "] '" + prm_decl.Name + " (" + column.DefaultValue.GetType().Name + ")=[" + column.DefaultValue + "]");
                    }

                    if (!string.IsNullOrEmpty(codeText))
                    {
                        prm_decl.Name = prm_decl.Name + " = " + codeText;
                        hasDefaultValue = true;
                    }

                    // do not use 'DefaultParameterValueAttribute' but 'overloaded method' see https://stackoverflow.com/questions/8215541/default-value-for-nullable-value-in-c-sharp-2-0
                    //prm_decl.CustomAttributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof(System.Runtime.InteropServices.DefaultParameterValueAttribute)), new CodeAttributeArgument(expr)));

                    if (hasDefaultValue)
                    {
                        parameters_dval.Add(prm_decl);
                    }
                    else
                    {
                        parameters_nval.Add(prm_decl);
                    }
                }
                else
                {
                    parameters_nval.Add(prm_decl);
                }

            }

            member_method.Parameters.Clear();
            foreach (var prm in parameters_nval)
            {
                member_method.Parameters.Add(prm);
            }
            foreach (var prm in parameters_dval)
            {
                member_method.Parameters.Add(prm);
            }
        }

        private static void Adjust_Table_InitClass(RegistrationResult rr, DacPacRegistration dacpac, DataTable table, CodeMemberMethod member_method)
        {
            member_method.Name = "InitTableClass";
        }

        private static void Adjust_DataSet_InitClass(RegistrationResult rr, DacPacRegistration dacpac, CodeMemberMethod member_method)
        {
            /*
    tablehlspdefinition = new hlspdefinitionDataTable();
    base.Tables.Add(tablehlspdefinition);

            => 
            base.Tables.Add(new hlspdefinitionDataTable());
             */
            member_method.Name = "InitDataSetClass";

            member_method.Attributes = member_method.Attributes | MemberAttributes.Static;
            member_method.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(DataSet)), "ds"));
            CodeVariableReferenceExpression prm_ref_ds = new CodeVariableReferenceExpression("ds");

            //CodeAssignStatement assign_ds = new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_ds"), prm_ref_ds);

            List<CodeStatement> new_statements = new List<CodeStatement>();

            Dictionary<string, CodeAssignStatement> field_assign_stmts = new Dictionary<string, CodeAssignStatement>();
            foreach (CodeStatement stmt in member_method.Statements)
            {
                CodeAssignStatement stmt_assign = stmt as CodeAssignStatement;
                if (stmt_assign != null)
                {
                    /* SAMPLE:
                        this.tablehlbiattributeconfig = new hlbiattributeconfigDataTable();
                    */

                    CodeFieldReferenceExpression fieldRefExpr = stmt_assign.Left as CodeFieldReferenceExpression;
                    CodeObjectCreateExpression createObjectExpr = stmt_assign.Right as CodeObjectCreateExpression;
                    if (fieldRefExpr != null && createObjectExpr != null)
                    {
                        string tableName = createObjectExpr.CreateType.BaseType.Remove(createObjectExpr.CreateType.BaseType.Length - 9, 9); // suffix "DataTable"
                        bool isOwnedByDacPac = IsTableOwnedByDacPac(rr, dacpac, tableName);
                        if (isOwnedByDacPac)
                        {
                            field_assign_stmts.Add(fieldRefExpr.FieldName, stmt_assign);
                        }
                        else
                        {
                            // ignore from this 'InitDataSetClass'
                        }
                        ///skip_old = true;
                    }
                    else
                    {
                        // use it?
                        /*
    base.DataSetName = "DemoTestData";
    base.Prefix = "";
    base.Namespace = "[dbo]";
    base.EnforceConstraints = true;
    SchemaSerializationMode = SchemaSerializationMode.IncludeSchema;                         
                         */

                        ///skip_old = true;
                    }
                }
                else
                {

                    CodeExpressionStatement stmt_expr = stmt as CodeExpressionStatement;
                    if (stmt_expr != null)
                    {
                        CodeMethodInvokeExpression invoke_AddTable = stmt_expr.Expression as CodeMethodInvokeExpression;
                        if (invoke_AddTable != null)
                        {
                            var prop_Table = invoke_AddTable.Method.TargetObject as CodePropertyReferenceExpression;
                            CodeFieldReferenceExpression fieldRefExpr = invoke_AddTable.Parameters.Count == 1 ? invoke_AddTable.Parameters[0] as CodeFieldReferenceExpression : null;
                            if (fieldRefExpr != null && prop_Table != null && prop_Table.TargetObject is CodeBaseReferenceExpression && prop_Table.PropertyName == "Tables")
                            {
                                CodeAssignStatement field_assign_stmt;
                                if (!field_assign_stmts.TryGetValue(fieldRefExpr.FieldName, out field_assign_stmt))
                                {
                                    // the assignment statement was removed because the table does not belong to this dacpac
                                    // =>  remove the table initialization as well
                                    ///skip_old = true;
                                }
                                else
                                {
                                    CodeObjectCreateExpression createObjectExpr = field_assign_stmt.Right as CodeObjectCreateExpression;
                                    string tableName = createObjectExpr.CreateType.BaseType.Remove(createObjectExpr.CreateType.BaseType.Length - 9, 9); // suffix "DataTable"

                                    prop_Table.TargetObject = prm_ref_ds;

                                    CodeObjectCreateExpression createObj = (CodeObjectCreateExpression)field_assign_stmt.Right;
                                    invoke_AddTable.Parameters.Clear();
                                    invoke_AddTable.Parameters.Add(createObj);

                                    // if (ds.Tables.IndexOf("tableName") < 0)
                                    // { AddTable(new ...) }


                                    CodeMethodInvokeExpression invoke_IndexOf = new CodeMethodInvokeExpression(prop_Table, "IndexOf", new CodeExpression[] {
                                        new_CodePrimitiveExpression(tableName)
                                    });

                                    var ifTableNotExists = new CodeBinaryOperatorExpression(invoke_IndexOf, CodeBinaryOperatorType.LessThan, new_CodePrimitiveExpression(0));

                                    CodeConditionStatement if_table_not_Exists_Add = new CodeConditionStatement(ifTableNotExists, stmt_expr);

                                    //new_statements.Add(stmt_expr);
                                    new_statements.Add(if_table_not_Exists_Add);
                                }
                            }
                        }
                    }
                    else
                    {
                        // ????
                    }
                }
            }

            member_method.Statements.Clear();
            foreach (var stmt in new_statements)
            {
                member_method.Statements.Add(stmt);
            }
        }

        private static CodeMemberMethod Adjust_DataSet_TableAccessor(ExtensionsClass exttype, CodeMemberProperty member_property)
        {
            CodeMemberMethod member_method = new CodeMemberMethod();
            CodeTypeParameter ctp = new CodeTypeParameter("TDataSet");
            ctp.Constraints.Add(new CodeTypeReference(typeof(DataSet)));
            member_method.TypeParameters.Add(ctp);
            var param_decl = new CodeParameterDeclarationExpression("this TDataSet", "ds");
            member_method.Parameters.Add(param_decl);

            member_method.Name = member_property.Name;
            member_method.ReturnType = member_property.Type;
            member_method.Attributes = MemberAttributes.Public | MemberAttributes.Static;

            //CodePropertyReferenceExpression base_Tables = new CodePropertyReferenceExpression(new CodeBaseReferenceExpression(), "Tables");
            //CodeArrayIndexerExpression indexer = new CodeArrayIndexerExpression(base_Tables, new CodeExpression[] { new_CodePrimitiveExpression(member_property.Name) });
            //CodeCastExpression cast_expr = new CodeCastExpression(member_property.Type, indexer);
            //member_method.Statements.Add(new CodeMethodReturnStatement(cast_expr));

            member_method.Statements.Add(new CodeMethodReturnStatement(
                             new CodeMethodInvokeExpression(
                                  new CodeMethodReferenceExpression(
                                     new CodeTypeReferenceExpression(exttype.extensions_type_decl.Name),
                                     exttype.extensions_method_GetTable.Name,
                                             new CodeTypeReference[] { member_property.Type }),
                                              new CodeVariableReferenceExpression("ds"),
                                                       new_CodePrimitiveExpression(member_property.Name))));

            return member_method;
        }

        private static CodeTypeMember Adjust_DataSet_Constructor(string typeName, CodeConstructor member_ctor)
        {
            CodeMemberMethod member_method = new CodeMemberMethod();
            member_method.Name = "RegisterDataSetModel";
            member_method.Attributes = MemberAttributes.Public | MemberAttributes.Static;

            CodeTypeParameter ctp = new CodeTypeParameter("TDataSet");
            ctp.Constraints.Add(new CodeTypeReference(typeof(DataSet)));
            member_method.TypeParameters.Add(ctp);



            var param_decl = new CodeParameterDeclarationExpression("TDataSet", "ds"); // "this TDataSet"
            member_method.Parameters.Add(param_decl);
            member_method.ReturnType = new CodeTypeReference("TDataSet");
            //member_ctor.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(DataSet)), "ds"));
            CodeVariableReferenceExpression prm_ref_ds = new CodeVariableReferenceExpression("ds");

            List<CodeStatement> membersToRemove = new List<CodeStatement>();
            foreach (CodeStatement stmt in member_ctor.Statements)
            {
                CodeExpressionStatement stmt_expr = stmt as CodeExpressionStatement;
                if (stmt_expr == null)
                {
                    membersToRemove.Add(stmt);
                }
                else
                {
                    // InitClass(); => InitClass(this)
                    CodeMethodInvokeExpression invoke_expr = stmt_expr.Expression as CodeMethodInvokeExpression;
                    if (invoke_expr != null)
                    {
                        if (invoke_expr.Method.MethodName == "InitClass")
                        {
                            invoke_expr.Method.MethodName = "InitDataSetClass";
                            invoke_expr.Method.TargetObject = new CodeTypeReferenceExpression(typeName);
                            invoke_expr.Parameters.Add(prm_ref_ds);
                        }
                        else
                        {
                            invoke_expr.Method.TargetObject = prm_ref_ds;
                        }

                        member_method.Statements.Add(stmt);
                    }

                }
            }

            foreach (var memberToRemove in membersToRemove)
            {
                member_ctor.Statements.Remove(memberToRemove);
            }

            member_method.Statements.Add(new CodeMethodReturnStatement(prm_ref_ds));
            return member_method;
        }

        private static void RemoveNoiseAttributes(CodeTypeMember member_decl)
        {
            List<CodeAttributeDeclaration> toRemoveSet = new List<CodeAttributeDeclaration>();
            foreach (CodeAttributeDeclaration attribute_decl in member_decl.CustomAttributes)
            {
                if (attribute_decl.AttributeType.BaseType == typeof(System.Diagnostics.DebuggerNonUserCodeAttribute).FullName)
                {
                    toRemoveSet.Add(attribute_decl);
                }
                else if (attribute_decl.AttributeType.BaseType == typeof(GeneratedCodeAttribute).FullName)
                {
                    toRemoveSet.Add(attribute_decl);
                }
                else if (attribute_decl.AttributeType.BaseType == typeof(BrowsableAttribute).FullName)
                {
                    toRemoveSet.Add(attribute_decl);
                }
                else if (attribute_decl.AttributeType.BaseType == "System.ComponentModel.Browsable")
                {
                    toRemoveSet.Add(attribute_decl);
                }
                else if (attribute_decl.AttributeType.BaseType == typeof(DesignerSerializationVisibilityAttribute).FullName)
                {
                    toRemoveSet.Add(attribute_decl);
                }
                else if (attribute_decl.AttributeType.BaseType == "System.ComponentModel.DesignerSerializationVisibility")
                {
                    toRemoveSet.Add(attribute_decl);
                }
                else if (attribute_decl.AttributeType.BaseType == typeof(SerializableAttribute).FullName)
                {
                    toRemoveSet.Add(attribute_decl);
                }
                else if (attribute_decl.AttributeType.BaseType == "System.Serializable")
                {
                    toRemoveSet.Add(attribute_decl);
                }
                else if (attribute_decl.AttributeType.BaseType == typeof(System.Xml.Serialization.XmlSchemaProviderAttribute).FullName)
                {
                    toRemoveSet.Add(attribute_decl);
                }
                else
                {
                    // 
                }
            }


            foreach (var memberToRemove in toRemoveSet)
            {
                member_decl.CustomAttributes.Remove(memberToRemove);
            }
        }

        private static string GetSnkPath(string snkPath)
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            //string[] names = asm.GetManifestResourceNames();
            string resourceName = "StoreLake.Sdk.CodeGeneration.GeneratedModel.snk";
            //System.Reflection.ManifestResourceInfo rrr = asm.GetManifestResourceInfo(resourceName);
            Stream streamSnk = asm.GetManifestResourceStream(resourceName);//"model.snk");

            if (streamSnk == null)
            {
                throw new StoreLakeSdkException("NotImplemented:" + "No SNK resource.");
            }

            using (FileStream outputFileStream = new FileStream(snkPath, FileMode.CreateNew))
            {
                streamSnk.CopyTo(outputFileStream);
            }

            return snkPath;
        }


        internal static void AddAssemblyAttributes(CodeCompileUnit ccu)
        {
            CodeAttributeDeclaration attributeAssemblyVersion = new CodeAttributeDeclaration(typeof(AssemblyVersionAttribute).FullName);
            attributeAssemblyVersion.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("1.0.0.0")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyVersion);

            CodeAttributeDeclaration attributeAssemblyFileVersion = new CodeAttributeDeclaration(typeof(AssemblyFileVersionAttribute).FullName);
            attributeAssemblyFileVersion.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("1.0.0.0")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyFileVersion);

            CodeAttributeDeclaration attributeAssemblyTitle = new CodeAttributeDeclaration(typeof(AssemblyTitleAttribute).FullName);
            attributeAssemblyTitle.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("generated")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyTitle);

            CodeAttributeDeclaration attributeAssemblyDescription = new CodeAttributeDeclaration(typeof(AssemblyDescriptionAttribute).FullName);
            attributeAssemblyDescription.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("generated")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyDescription);

            //CodeAttributeDeclaration attributeAssemblyGuid = new CodeAttributeDeclaration(typeof(System.Runtime.InteropServices.GuidAttribute).FullName);
            //attributeAssemblyGuid.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression(ModelAssemblyGuid.ToString())));
            //ccu.AssemblyCustomAttributes.Add(attributeAssemblyGuid);

            CodeAttributeDeclaration attributeComVisible = new CodeAttributeDeclaration(typeof(ComVisibleAttribute).FullName);
            attributeComVisible.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression(false)));
            ccu.AssemblyCustomAttributes.Add(attributeComVisible);

            CodeAttributeDeclaration attributeAssemblyProduct = new CodeAttributeDeclaration(typeof(AssemblyProductAttribute).FullName);
            attributeAssemblyProduct.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("Model")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyProduct);

            CodeAttributeDeclaration attributeAssemblyCompany = new CodeAttributeDeclaration(typeof(AssemblyCompanyAttribute).FullName);
            attributeAssemblyCompany.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("By StoreLake")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyCompany);

            CodeAttributeDeclaration attributeAssemblyCopyright = new CodeAttributeDeclaration(typeof(AssemblyCopyrightAttribute).FullName);
            attributeAssemblyCopyright.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("Copyright © 20'21")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyCopyright);

            CodeAttributeDeclaration attributeAssemblyTrademark = new CodeAttributeDeclaration(typeof(AssemblyTrademarkAttribute).FullName);
            attributeAssemblyTrademark.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyTrademark);

            CodeAttributeDeclaration attributeAssemblyConfiguration = new CodeAttributeDeclaration(typeof(AssemblyConfigurationAttribute).FullName);
            attributeAssemblyConfiguration.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyConfiguration);

            CodeAttributeDeclaration attributeAssemblyCulture = new CodeAttributeDeclaration(typeof(AssemblyCultureAttribute).FullName);
            attributeAssemblyCulture.Arguments.Add(new CodeAttributeArgument(new_CodePrimitiveExpression("")));
            ccu.AssemblyCustomAttributes.Add(attributeAssemblyCulture);

        }

        private static void CompileCode(DacPacRegistration dacpac, CompilerParameters comparam, string[] libdirs, string outputFolder, string fileName, string outputAssemblyFullFileName, DirectoryInfo tempDirInfo, string[] codeFileNames, int count_of_types)
        {
            string errorsFullFileName = System.IO.Path.Combine(tempDirInfo.FullName, fileName + ".errors.txt");
            string tmpDllFullFileName = System.IO.Path.Combine(tempDirInfo.FullName, fileName + ".dll");

            string snkPath = GetSnkPath(Path.Combine(tempDirInfo.FullName, "GeneratedModel.snk"));

            StringBuilder compilerOpt = new StringBuilder();
            compilerOpt.Append("/target:library ");
            foreach (var libdir in libdirs)
            {
                compilerOpt.AppendFormat("/lib:{0} ", libdir);
            }
            compilerOpt.AppendFormat("/keyfile:\"{0}\" ", snkPath);
            //CompilerParameters comparam = new CompilerParameters(new string[] { });
            comparam.CompilerOptions = compilerOpt.ToString(); // "/optimize";
            s_tracer.TraceEvent(TraceEventType.Verbose, 0, "CompilerOptions:" + comparam.CompilerOptions);
            comparam.WarningLevel = 4; // max
            comparam.GenerateInMemory = false;
            //Indicates whether the output is an executable.  
            comparam.GenerateExecutable = false;
            comparam.IncludeDebugInformation = true;
            comparam.TempFiles.KeepFiles = true;
            comparam.TempFiles = new TempFileCollection(tempDirInfo.FullName, true);
            s_tracer.TraceEvent(TraceEventType.Verbose, 0, "ReferencedAssemblies:" + comparam.ReferencedAssemblies.Count);
            for (int ix = 0; ix < comparam.ReferencedAssemblies.Count; ix++)
            {
                s_tracer.TraceEvent(TraceEventType.Verbose, 0, "  ReferencedAssembly (" + (ix + 1) + "/" + comparam.ReferencedAssemblies.Count + ") : " + comparam.ReferencedAssemblies[ix]);
            }

            //  compilerOptions.ReferencedAssemblies.Add(new FileInfo(typeof(System.ComponentModel.DescriptionAttribute).Assembly.Location).Name); // system.dll
            //provide the name of the class which contains the Main Entry //point method  
            //comparam.MainClass = "mynamespace.CMyclass";
            //provide the path where the generated assembly would be placed  
            comparam.OutputAssembly = tmpDllFullFileName;
            //Create an instance of the c# compiler and pass the assembly to //compile  
            Microsoft.CSharp.CSharpCodeProvider ccp = new Microsoft.CSharp.CSharpCodeProvider();
#pragma warning disable CS0618 // Type or member is obsolete
            ICodeCompiler icc = ccp.CreateCompiler();
#pragma warning restore CS0618 // Type or member is obsolete
            //The CompileAssemblyFromDom would either return the list of  
            //compile time errors (if any), or would create the  
            //assembly in the respective path in case of successful //compilation  
            //CompilerResults compres = icc.CompileAssemblyFromDom(comparam, codeCompileUnit);

            s_tracer.TraceEvent(TraceEventType.Verbose, 0, "Compiling assembly (ref:" + dacpac.IsReferencedPackage + "):" + outputAssemblyFullFileName + ", Types:" + count_of_types);
            CompilerResults compres = icc.CompileAssemblyFromFileBatch(comparam, codeFileNames);

            if (compres == null || compres.Errors.Count > 0)
            {
                StringBuilder errorFileContent = new StringBuilder();
                errorFileContent.AppendLine("Errors: " + compres.Errors.Count);
                for (int i = 0; i < compres.Errors.Count; i++)
                {
                    errorFileContent.AppendLine("Error === " + i + "/ " + compres.Errors.Count + "=======================");
                    //Console.WriteLine(compres.Errors[i]);
                }

                File.WriteAllText(errorsFullFileName, errorFileContent.ToString());
                Console.WriteLine(errorsFullFileName);

                var err = compres.Errors[0];
                string fn = Path.GetFileName(err.FileName);
                throw new StoreLakeSdkException("Compile failed: " + fn + " (" + err.Line + "," + err.Column + "): error " + err.ErrorNumber + " : " + err.ErrorText + "   generated file location:" + err.FileName);
            }

            s_tracer.TraceEvent(TraceEventType.Verbose, 0, "  Copy generated assembly : " + outputAssemblyFullFileName);
            File.Copy(tmpDllFullFileName, outputAssemblyFullFileName, true);
        }

        private static void GenerateVersionComment(CodeNamespace codeNamespace)
        {
            codeNamespace.Comments.Add(new CodeCommentStatement(""));
            codeNamespace.Comments.Add(new CodeCommentStatement(""));
        }

    }
}
