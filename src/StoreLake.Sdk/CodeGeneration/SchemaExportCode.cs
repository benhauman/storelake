﻿using Microsoft.CSharp;
using StoreLake.Sdk.SqlDom;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
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
using System.Xml.Schema;

namespace StoreLake.Sdk.CodeGeneration
{
    public static class SchemaExportCode
    {

        private static readonly TraceSource s_tracer = new TraceSource("StoreLake.Sdk") { Switch = new SourceSwitch("TraceAll") { Level = SourceLevels.All } };
        public static TraceSource CreateTraceSource()
        {
            return s_tracer;
        }

        internal static void ExportTypedDataSetCode(AssemblyResolver assemblyResolver, RegistrationResult rr, string libdir, string inputdir, string outputdir, string dacNameFilter, string storeSuffix, bool writeSchemaFile, string tempdir)
        {
            AssemblyName an_Dibix = new AssemblyName("Dibix"); // no version
            //AssemblyName an_Dibix = AssemblyName.GetAssemblyName(Path.Combine(libdir, "Dibix.dll"));
            //AssemblyName an_DibixHttpServer = AssemblyName.GetAssemblyName(Path.Combine(libdir, "Dibix.Http.Server.dll"));
            //AssemblyName an_DibixHttpClient = AssemblyName.GetAssemblyName(Path.Combine(libdir, "Dibix.Http.Client.dll"));
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

                        if (!string.IsNullOrEmpty(dacNameFilter) && !string.Equals(dacNameFilter, dacName))
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

                            ImportSchemasAsDataSets(dbx, assemblyResolver, rr, dacpac, schemaContent, inputdir, outputdir, filenameNoExtension, dacName, storeSuffix, tempdir, libdir);
                        }
                    }

                }
            }

            if (level_count < 100)
            {
                return;
            }

        }


        private static void ImportSchemasAsDataSets(KnownDibixTypes dbx, AssemblyResolver assemblyResolver, RegistrationResult rr, DacPacRegistration dacpac, string schemaContent, string inputdir, string outputdir, string fileName, string namespaceName, string storeSuffix, string tempdir, string libdir)
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
            CodeCompileUnit ccu_tabletables = new CodeCompileUnit();
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

                Adjust_CCU(assemblyResolver, comparam, rr, dacpac, ccu_tables, ccu_tabletables, ccu_procedures, storeSuffix);
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
                    GenerateCodeFile(tempDirInfo, codeProvider, codeFileNames, fileName, ccu_tabletables, "Udts");
                    GenerateCodeFile(tempDirInfo, codeProvider, codeFileNames, fileName, ccu_procedures, "Procedures");
                    GenerateCodeFile(tempDirInfo, codeProvider, codeFileNames, fileName, ccu_accessors, "Accessors");
                }

                CompileCode(dacpac, comparam, libdir, outputdir, fileName, fullFileName_dll, tempDirInfo, codeFileNames.ToArray());
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

        private static void Adjust_CCU(AssemblyResolver assemblyResolver, CompilerParameters comparam, RegistrationResult rr, DacPacRegistration dacpac
            , CodeCompileUnit ccu_tables
            , CodeCompileUnit ccu_tabletypes
            , CodeCompileUnit ccu_procedures
            , string storeSuffix)
        {
            if (ccu_tables.Namespaces.Count > 1)
            {
                throw new StoreLakeSdkException("Multiple namespaces");
            }

            //InitializeStoreNamespaceName(dacpac, storeSuffix);

            ExtensionsClass exttype = new ExtensionsClass();
            CodeNamespace ns_old = ccu_tables.Namespaces[0];
            ccu_tables.Namespaces.Clear();
            CodeNamespace ns_tables = new CodeNamespace() { Name = dacpac.TestStoreAssemblyNamespace };
            ccu_tables.Namespaces.Add(ns_tables);
            CodeNamespace ns_tabletypes = new CodeNamespace() { Name = dacpac.TestStoreAssemblyNamespace };
            ccu_tabletypes.Namespaces.Add(ns_tabletypes);

            CodeNamespace ns_procedures = new CodeNamespace() { Name = dacpac.TestStoreAssemblyNamespace };
            ccu_procedures.Namespaces.Add(ns_procedures);

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
                                bool vvvv = IsPublic(member_decl.Attributes);
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

            //rr.udt_rows = new SortedDictionary<string, TableTypeRow>();
            BuildUdts(rr, dacpac, ns_tabletypes, rr.udt_rows);

            string extMethodAccess_CommandExecuteHandler = dacpac.TestStoreExtensionSetName + "Procedures" + "Handler";
            CodeTypeDeclaration procedures_handler_type_decl = new CodeTypeDeclaration(dacpac.TestStoreExtensionSetName + "Procedures" + "CommandExecuteHandler")
            {
                Attributes = MemberAttributes.Public
            };
            ns_procedures.Types.Add(procedures_handler_type_decl);

            string extMethodAccess_HandlerFacade = dacpac.TestStoreExtensionSetName + "Procedures" + "Facade";
            CodeTypeDeclaration procedures_facade_type_decl = new CodeTypeDeclaration(dacpac.TestStoreExtensionSetName + "Procedures" + "HandlerFacade")
            {
                Attributes = MemberAttributes.Public
            };
            ns_procedures.Types.Add(procedures_facade_type_decl);

            BuildStoreProceduresHandlerType(rr, dacpac, exttype.extensions_type_decl, ns_procedures, procedures_handler_type_decl, procedures_facade_type_decl, extMethodAccess_HandlerFacade, rr.udt_rows);
            BuildStoreProceduresProvider(rr, dacpac, ns_procedures, exttype, procedures_handler_type_decl, extMethodAccess_CommandExecuteHandler, procedures_facade_type_decl, extMethodAccess_HandlerFacade);
        }

        private static void BuildUdts(RegistrationResult rr, DacPacRegistration dacpac, CodeNamespace ns_tabletypes, IDictionary<string, TableTypeRow> udt_rows)
        {
            foreach (var udt_reg in dacpac.registered_tabletypes.Values)
            {
                string udtRowClassName = PrepareUdtRowName(udt_reg);

                TableTypeRow udtRow = new TableTypeRow();
                udtRow.udt_row_type_decl = new CodeTypeDeclaration(udtRowClassName);
                udtRow.ClrFullTypeName = ns_tabletypes.Name + "." + udtRowClassName;
                ns_tabletypes.Types.Add(udtRow.udt_row_type_decl);

                BuildUdtRow(udt_reg, udtRow);

                udt_rows.Add(udt_reg.TableTypeSqlFullName, udtRow);
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

                var clrTypes = GetParameterClrType(pi.ColumnDbType, null);

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

        class ExtensionsClass
        {
            internal CodeTypeDeclaration extensions_type_decl;
            internal CodeMemberMethod extensions_method_GetTable;
            internal CodeMemberMethod extensions_method_InitDataSet;
        }

        private static void BuildStoreProceduresProvider(RegistrationResult rr, DacPacRegistration dacpac, CodeNamespace ns, ExtensionsClass exttype, CodeTypeDeclaration procedures_handler_type_decl, string handlerMethod, CodeTypeDeclaration procedures_facade_type_decl, string facadeMethod)
        {
            // 1.DataTable
            // 2.extension method Get
            // 3.extension method Set

            CodeTypeDeclaration procedure_provider_DataTable_Type_decl = new CodeTypeDeclaration()
            {
                Name = procedures_handler_type_decl.Name + "DataTable", // ProceduresDataTable
            };
            procedure_provider_DataTable_Type_decl.TypeAttributes = (procedure_provider_DataTable_Type_decl.TypeAttributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPrivate;
            procedure_provider_DataTable_Type_decl.BaseTypes.Add(typeof(DataTable));
            CodeMemberField Table_TableName = new CodeMemberField()
            {
                Name = procedure_provider_DataTable_Type_decl.Name + "TableName",
                Type = new CodeTypeReference(typeof(string)),
                Attributes = MemberAttributes.Assembly | MemberAttributes.Static,
                InitExpression = new CodePrimitiveExpression("__" + procedures_handler_type_decl.Name + "__")
            };

            procedure_provider_DataTable_Type_decl.Members.Add(Table_TableName);

            CodeConstructor table_ctor = new CodeConstructor() { Attributes = MemberAttributes.Public };
            table_ctor.Statements.Add(new CodeAssignStatement()
            {
                Left = new CodePropertyReferenceExpression(new CodeThisReferenceExpression(), "TableName"),
                Right = new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(procedure_provider_DataTable_Type_decl.Name), Table_TableName.Name)
            }); ;
            procedure_provider_DataTable_Type_decl.Members.Add(table_ctor);

            CodeMemberField field_handlerInstanceCommandExecute = new CodeMemberField()
            {
                Name = "handlerInstanceCommandExecute",
                Type = new CodeTypeReference(procedures_handler_type_decl.Name),
                Attributes = MemberAttributes.Assembly,
                InitExpression = new CodeObjectCreateExpression(new CodeTypeReference(procedures_handler_type_decl.Name))
            };
            procedure_provider_DataTable_Type_decl.Members.Add(field_handlerInstanceCommandExecute);
            CodeMemberField field_handlerInstanceFacade = new CodeMemberField()
            {
                Name = "handlerInstanceFacade",
                Type = new CodeTypeReference(procedures_facade_type_decl.Name),
                Attributes = MemberAttributes.Assembly,
                InitExpression = new CodeObjectCreateExpression(new CodeTypeReference(procedures_facade_type_decl.Name))
            };
            procedure_provider_DataTable_Type_decl.Members.Add(field_handlerInstanceFacade);



            exttype.extensions_type_decl.Members
                .Add(procedure_provider_DataTable_Type_decl);

            var method_decl_get_handler = BuildExtensionMethodsForProcedures(exttype, procedures_handler_type_decl, handlerMethod, procedure_provider_DataTable_Type_decl, Table_TableName, field_handlerInstanceCommandExecute);
            var method_decl_get_facade = BuildExtensionMethodsForProcedures(exttype, procedures_facade_type_decl, facadeMethod, procedure_provider_DataTable_Type_decl, Table_TableName, field_handlerInstanceFacade);

        }

        private static CodeMemberMethod BuildExtensionMethodsForProcedures(ExtensionsClass exttype,
            CodeTypeDeclaration procedures_type_decl, string extensionMethodNameGet,
            CodeTypeDeclaration procedure_provider_DataTable_Type_decl,
            CodeMemberField Table_TableName,
            CodeMemberField field_handlerInstance)
        {
            CodeMemberMethod extensions_method_GetHandler = new CodeMemberMethod()
            {
                Name = extensionMethodNameGet,
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                ReturnType = new CodeTypeReference(procedures_type_decl.Name)
            };
            CodeTypeParameter ctp = new CodeTypeParameter("TDataSet");
            ctp.Constraints.Add(new CodeTypeReference(typeof(DataSet)));
            extensions_method_GetHandler.TypeParameters.Add(ctp);


            var param_decl_ds = new CodeParameterDeclarationExpression("this TDataSet", "ds");
            extensions_method_GetHandler.Parameters.Add(param_decl_ds);

            var method_GetTable_Handlers = new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(exttype.extensions_type_decl.Name),
                            exttype.extensions_method_GetTable.Name, new CodeTypeReference[] {
                            new CodeTypeReference( procedure_provider_DataTable_Type_decl.Name)
                            });

            var invoke_GetTable_Handlers = new CodeMethodInvokeExpression(method_GetTable_Handlers, new CodeExpression[] {
                new CodeVariableReferenceExpression("ds")
                , new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(procedure_provider_DataTable_Type_decl.Name), Table_TableName.Name)
            });

            var field_ref_handlerInstanceCommandExecute = new CodeFieldReferenceExpression(invoke_GetTable_Handlers, field_handlerInstance.Name);
            extensions_method_GetHandler.Statements.Add(new CodeMethodReturnStatement(field_ref_handlerInstanceCommandExecute));


            exttype.extensions_type_decl.Members.Add(extensions_method_GetHandler);

            CodeMemberMethod extensions_method_SetHandler = new CodeMemberMethod()
            {
                Name = "SetCommandExecuteHandlerInstanceFor" + extensionMethodNameGet, // SetHandlerFacadeInstanceDor
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                ReturnType = new CodeTypeReference("TDataSet")
            };
            CodeTypeParameter ctp_set_ds = new CodeTypeParameter("TDataSet");
            ctp_set_ds.Constraints.Add(new CodeTypeReference(typeof(DataSet)));
            extensions_method_SetHandler.TypeParameters.Add(ctp_set_ds);
            CodeTypeParameter ctp_set_hi = new CodeTypeParameter("THandler") { HasConstructorConstraint = true };
            ctp_set_hi.Constraints.Add(new CodeTypeReference(procedures_type_decl.Name));
            extensions_method_SetHandler.TypeParameters.Add(ctp_set_hi);

            extensions_method_SetHandler.Parameters.Add(param_decl_ds);

            var assign_handler = new CodeAssignStatement(field_ref_handlerInstanceCommandExecute, new CodeObjectCreateExpression("THandler"));

            extensions_method_SetHandler.Statements.Add(assign_handler);
            extensions_method_SetHandler.Statements.Add(new CodeMethodReturnStatement(new CodeVariableReferenceExpression("ds")));
            exttype.extensions_type_decl.Members.Add(extensions_method_SetHandler);
            return extensions_method_GetHandler;
        }

        private static void BuildStoreProceduresHandlerType(RegistrationResult rr, DacPacRegistration dacpac, CodeTypeDeclaration extensions_type_decl, CodeNamespace ns_procedures, CodeTypeDeclaration procedures_handler_type_decl, CodeTypeDeclaration procedures_facade_type_decl, string extMethodAccess_HandlerFacade, IDictionary<string, TableTypeRow> udt_rows)
        {
            AddReadParameterFunctions(procedures_handler_type_decl);

            Console.WriteLine("Procedures found:" + dacpac.registered_Procedures.Values.Count);
            foreach (var procedure in dacpac.registered_Procedures.Values)
            {
                //Console.WriteLine("" + procedure.ProcedureSchemaName + "." + procedure.ProcedureName);

                int? countOfResultSets;
                string procedureMethodName;
                SqlDom.ProcedureMetadata procedure_metadata;
                try
                {
                    procedure_metadata = SqlDom.ProcedureGenerator.ParseProcedureBody(procedure.ProcedureName, procedure.ProcedureBodyScript);
                    int? isQueryProcedure = SqlDom.ProcedureGenerator.IsQueryProcedure(rr.DoResolveColumnType, rr.SchemaMetadata(), procedure_metadata).Length;
                    countOfResultSets = procedure.Annotations.Count(x => x.AnnotationKey == "Return");
                    if (countOfResultSets > 0)
                    {
                        if (isQueryProcedure.GetValueOrDefault() == countOfResultSets)
                        {
                            // ok
                        }
                        else
                        {
                            // uups [hlsys_createactioncontext]
                            throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        if (isQueryProcedure.GetValueOrDefault() > 0)
                        {
                            // uups => no annotations =>
                            //[hlcmgetcontact]
                            countOfResultSets = 1; // or more?
                        }
                        else
                        {
                            // ok
                        }
                    }
                    var annotation_Name = procedure.Annotations.SingleOrDefault(x => x.AnnotationKey == "Name");
                    if (annotation_Name != null)
                    {
                        procedureMethodName = annotation_Name.AnnotationValue;
                    }
                    else
                    {
                        procedureMethodName = procedure.ProcedureName;
                    }
                }
                catch (Exception ex)
                {
                    s_tracer.TraceEvent(TraceEventType.Warning, 0, "Procedure  [" + procedure.ProcedureName + "] generation failed." + ex.Message);
                    countOfResultSets = null;
                    procedureMethodName = procedure.ProcedureName;
                    procedure_metadata = null;
                }

                if (countOfResultSets.HasValue)
                {
                    GenerateProcedureDeclaration(rr, extMethodAccess_HandlerFacade, procedures_handler_type_decl, procedure, countOfResultSets.Value, procedureMethodName, ns_procedures, procedures_facade_type_decl, procedure_metadata, udt_rows);
                }
                else
                {
                    s_tracer.TraceEvent(TraceEventType.Warning, 0, "SKIP procedure  [" + procedure.ProcedureName + "] generation failed.");
                }
            }
        }

        class ParameterTypeMap
        {
            public Type TypeNotNull;
            public Type TypeNull;
        }
        private static IDictionary<string, ParameterTypeMap> s_ParameterTypeMap = InitializeParameterTypeMap();
        private static IDictionary<string, ParameterTypeMap> AddParameterTypeMap<TNotNull, TNull>(IDictionary<string, ParameterTypeMap> dict)
        {
            dict.Add(typeof(TNotNull).AssemblyQualifiedName, new ParameterTypeMap() { TypeNotNull = typeof(TNotNull), TypeNull = typeof(TNull) });
            return dict;
        }
        private static IDictionary<string, ParameterTypeMap> InitializeParameterTypeMap()
        {
            IDictionary<string, ParameterTypeMap> dict = new SortedDictionary<string, ParameterTypeMap>();
            AddParameterTypeMap<string, string>(dict);
            AddParameterTypeMap<bool, bool?>(dict);
            AddParameterTypeMap<byte, byte?>(dict);
            AddParameterTypeMap<short, short?>(dict);
            AddParameterTypeMap<int, int?>(dict);
            AddParameterTypeMap<long, long?>(dict);
            AddParameterTypeMap<DateTime, DateTime?>(dict);
            AddParameterTypeMap<Guid, Guid?>(dict);
            AddParameterTypeMap<decimal, decimal?>(dict);
            AddParameterTypeMap<byte[], byte[]>(dict);
            return dict;
        }

        private static void AddReadParameterFunctions(CodeTypeDeclaration type_decl)
        {
            //type_decl.StartDirectives.Add(new CodeRegionDirective(CodeRegionMode.Start, "Readers"));


            bool startRegion = true;
            foreach (ParameterTypeMap prm_type in s_ParameterTypeMap.Values)
            {
                var mtd = AddReadParameterFunctions_Cast(type_decl, prm_type.TypeNotNull);
                if (startRegion)
                {
                    mtd.StartDirectives.Add(new CodeRegionDirective(CodeRegionMode.Start, "Readers"));
                    startRegion = false;
                }
                AddReadParameterFunctions_NullOrCast(type_decl, prm_type.TypeNotNull, prm_type.TypeNull);
            }

            AddReadParameterFunctions_UDT(type_decl);

            AddReadParameterFunctions_XElement(type_decl, false, typeof(System.Xml.Linq.XElement));
            var mtd_end = AddReadParameterFunctions_XElement(type_decl, true, typeof(System.Xml.Linq.XElement));

            mtd_end.EndDirectives.Add(new CodeRegionDirective(CodeRegionMode.End, String.Empty));
        }

        private static void AddReadParameterFunctions_UDT(CodeTypeDeclaration type_decl)
        {
            // "read_Records"
            string get_param_value_method_name = BuildReadCommandParameterMethodName(true, null, false);
            CodeMemberMethod read_method_decl = BuildReadCommandParameterMethodDeclaration(type_decl, get_param_value_method_name, typeof(IEnumerable<IDataRecord>));

            var cmd_Parameters = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("cmd"), "Parameters");
            var cmd_Parameter_prm = new CodeIndexerExpression(cmd_Parameters, new CodeExpression[] { new CodeVariableReferenceExpression("name") });
            var cmd_Parameter_prm_Value = new CodePropertyReferenceExpression(cmd_Parameter_prm, "Value");

            //Array.fr
            var cast_prm = new CodeCastExpression(typeof(IEnumerable<IDataRecord>), cmd_Parameter_prm_Value);

            //var invoke_ToArray = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(System.Linq.Enumerable)), "ToArray"));
            //invoke_ToArray.Parameters.Add(cast_prm);
            //read_method_decl.Statements.Add(new CodeMethodReturnStatement(invoke_ToArray));
            read_method_decl.Statements.Add(new CodeMethodReturnStatement(cast_prm));
        }

        private static CodeMemberMethod AddReadParameterFunctions_Cast(CodeTypeDeclaration type_decl, Type typeNotNull)
        {
            //System.Data.Common.DbCommand cmd = null;
            //(string)cmd.Parameters[""].Value;
            var get_param_value_method_name = BuildReadCommandParameterMethodName(false, typeNotNull, false);
            CodeMemberMethod read_method_decl = BuildReadCommandParameterMethodDeclaration(type_decl, get_param_value_method_name, typeNotNull);

            var cmd_Parameters = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("cmd"), "Parameters");
            var cmd_Parameter_prm = new CodeIndexerExpression(cmd_Parameters, new CodeExpression[] { new CodeVariableReferenceExpression("name") });
            var cmd_Parameter_prm_Value = new CodePropertyReferenceExpression(cmd_Parameter_prm, "Value");

            var cast_prm = new CodeCastExpression(typeNotNull, cmd_Parameter_prm_Value);
            read_method_decl.Statements.Add(new CodeMethodReturnStatement(cast_prm));
            return read_method_decl;
        }

        private static CodeMemberMethod AddReadParameterFunctions_XElement(CodeTypeDeclaration type_decl, bool allowNull, Type typeNotNull)
        {
            //System.Data.Common.DbCommand cmd = null;
            //object val = cmd.Parameters[""].Value;
            // if val is XElement return cast
            // return XElement.Parse();
            var get_param_value_method_name = BuildReadCommandParameterMethodName(false, typeNotNull, allowNull);
            CodeMemberMethod read_method_decl = BuildReadCommandParameterMethodDeclaration(type_decl, get_param_value_method_name, typeNotNull);

            var cmd_Parameters = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("cmd"), "Parameters");
            var cmd_Parameter_prm = new CodeIndexerExpression(cmd_Parameters, new CodeExpression[] { new CodeVariableReferenceExpression("name") });
            var cmd_Parameter_prm_Value = new CodePropertyReferenceExpression(cmd_Parameter_prm, "Value");

            var var_val_decl = new CodeVariableDeclarationStatement(typeof(object), "val")
            {
                InitExpression = cmd_Parameter_prm_Value
            };
            read_method_decl.Statements.Add(var_val_decl);

            var var_val_ref = new CodeVariableReferenceExpression(var_val_decl.Name);

            if (allowNull)
            {
                var DBNull_Value = new CodePropertyReferenceExpression(new CodeTypeReferenceExpression(typeof(DBNull)), "Value");

                var is_Null = new CodeBinaryOperatorExpression(var_val_ref, CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(null));
                var is_DBNull = new CodeBinaryOperatorExpression(var_val_ref, CodeBinaryOperatorType.ValueEquality, DBNull_Value);

                var if_is_Null_or_DBNull = new CodeConditionStatement();
                if_is_Null_or_DBNull.Condition = new CodeBinaryOperatorExpression(is_Null, CodeBinaryOperatorType.BooleanOr, is_DBNull);
                if_is_Null_or_DBNull.TrueStatements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(null)));

                read_method_decl.Statements.Add(if_is_Null_or_DBNull);
            }

            var cast_prm_XElement = new CodeCastExpression(typeNotNull, var_val_ref);
            var cast_prm_String = new CodeCastExpression(typeof(string), var_val_ref);
            var invoke_XElement_Parse = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(
                    new CodeTypeReferenceExpression(typeof(XElement)), "Parse"
                ), cast_prm_String);

            CodeExpression isAssignable = new CodeMethodInvokeExpression(
                new CodeTypeOfExpression(typeof(XElement)), "IsAssignableFrom",
                 new CodeMethodInvokeExpression(var_val_ref, "GetType"));

            var if_is_XElement = new CodeConditionStatement();
            if_is_XElement.Condition = isAssignable;
            if_is_XElement.TrueStatements.Add(new CodeMethodReturnStatement(cast_prm_XElement));
            if_is_XElement.FalseStatements.Add(new CodeMethodReturnStatement(invoke_XElement_Parse));


            read_method_decl.Statements.Add(if_is_XElement);
            return read_method_decl;
        }

        private static void AddReadParameterFunctions_NullOrCast(CodeTypeDeclaration type_decl, Type typeNotNull, Type typeNull)
        {
            //System.Data.Common.DbCommand cmd = null;
            //object val = cmd.Parameters[""].Value;
            // if val == null || val == DBNull.Value return null
            // return cast;
            var get_param_value_method_name = BuildReadCommandParameterMethodName(false, typeNotNull, true);
            CodeMemberMethod read_method_decl = BuildReadCommandParameterMethodDeclaration(type_decl, get_param_value_method_name, typeNull);

            var cmd_Parameters = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("cmd"), "Parameters");
            var cmd_Parameter_prm = new CodeIndexerExpression(cmd_Parameters, new CodeExpression[] { new CodeVariableReferenceExpression("name") });
            var cmd_Parameter_prm_Value = new CodePropertyReferenceExpression(cmd_Parameter_prm, "Value");

            var var_val_decl = new CodeVariableDeclarationStatement(typeof(object), "val")
            {
                InitExpression = cmd_Parameter_prm_Value
            };
            read_method_decl.Statements.Add(var_val_decl);


            var var_val_ref = new CodeVariableReferenceExpression(var_val_decl.Name);
            var cast_prm_XElement = new CodeCastExpression(typeNotNull, var_val_ref);

            var DBNull_Value = new CodePropertyReferenceExpression(new CodeTypeReferenceExpression(typeof(DBNull)), "Value");

            var is_Null = new CodeBinaryOperatorExpression(var_val_ref, CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(null));
            var is_DBNull = new CodeBinaryOperatorExpression(var_val_ref, CodeBinaryOperatorType.ValueEquality, DBNull_Value);

            var if_is_Null_or_DBNull = new CodeConditionStatement();
            if_is_Null_or_DBNull.Condition = new CodeBinaryOperatorExpression(is_Null, CodeBinaryOperatorType.BooleanOr, is_DBNull);
            if_is_Null_or_DBNull.TrueStatements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(null)));
            if_is_Null_or_DBNull.FalseStatements.Add(new CodeMethodReturnStatement(cast_prm_XElement));


            read_method_decl.Statements.Add(if_is_Null_or_DBNull);
        }

        private static CodeMemberMethod BuildReadCommandParameterMethodDeclaration(CodeTypeDeclaration type_decl, string name, Type typeReturn)
        {
            CodeMemberMethod read_method_decl = new CodeMemberMethod() { Name = name };
            read_method_decl.Attributes = MemberAttributes.Static | MemberAttributes.Private;
            read_method_decl.ReturnType = new CodeTypeReference(typeReturn);
            read_method_decl.Parameters.Add(new CodeParameterDeclarationExpression(typeof(System.Data.Common.DbCommand), "cmd"));
            read_method_decl.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string), "name"));

            type_decl.Members.Add(read_method_decl);
            return read_method_decl;
        }

        private static string BuildReadCommandParameterMethodName(bool isUDT, Type typeNotNull, bool allowNull)
        {
            if (isUDT)
            {
                return "read_Records";
            }
            string typeName = (typeNotNull == typeof(byte[])) ? "Bytes" : typeNotNull.Name;
            return "read_" + typeName + (allowNull ? "_OrNull" : "");
        }

        class CommandHandlerMethod
        {
            internal string handler_type_name;
            internal CodeMemberMethod handler_method_decl;
        }
        private static void GenerateProcedureDeclaration(RegistrationResult rr, string extMethodAccess_HandlerFacade, CodeTypeDeclaration procedures_type_decl, StoreLakeProcedureRegistration procedure, int countOfResultSets, string procedureMethodName, CodeNamespace ns_procedures, CodeTypeDeclaration procedures_facade_type_decl, SqlDom.ProcedureMetadata procedure_metadata, IDictionary<string, TableTypeRow> udt_rows)
        {
            bool? hasReturnStatements = ProcedureGenerator.HasReturnStatement(procedure_metadata);

            CommandHandlerMethod hm = new CommandHandlerMethod() { handler_type_name = procedures_type_decl.Name };
            hm.handler_method_decl = new CodeMemberMethod() { Name = procedureMethodName, Attributes = MemberAttributes.Public };
            procedures_type_decl.Members.Add(hm.handler_method_decl);
            hm.handler_method_decl.Parameters.Add(new CodeParameterDeclarationExpression(typeof(DataSet), "db"));
            hm.handler_method_decl.Parameters.Add(new CodeParameterDeclarationExpression(typeof(System.Data.Common.DbCommand), "cmd"));

            Type returnType = countOfResultSets > 0
                ? typeof(System.Data.Common.DbDataReader)
                : typeof(int);
            //: hasReturnStatements.GetValueOrDefault()
            //    ? typeof(int) : typeof(void);

            hm.handler_method_decl.ReturnType = new CodeTypeReference(returnType);

            hm.handler_method_decl.Statements.Add(new CodeCommentStatement("Parameters:" + procedure.Parameters.Count));

            for (int ix = 0; ix < procedure.Parameters.Count; ix++)
            {
                StoreLakeParameterRegistration parameter = procedure.Parameters[ix];
                string codeParameterName = parameter.ParameterName.Replace("@", "");
                ProcedureCodeParameter parameterType = GetParameterClrType(parameter);
                parameterType.ParameterCodeName = codeParameterName;
                procedure_metadata.parameters.Add(parameter.ParameterName, parameterType);

                if (parameter.ParameterDbType == SqlDbType.Structured)
                {
                    if (rr.registered_tabletypes.TryGetValue(parameter.ParameterTypeFullName, out DacPacRegistration type_dacpac))
                    {
                        if (type_dacpac.registered_tabletypes.TryGetValue(parameter.ParameterTypeFullName, out StoreLakeTableTypeRegistration type_reg))
                        {
                            if (udt_rows.TryGetValue(parameter.ParameterTypeFullName, out TableTypeRow ttrow))
                            {
                                // in this dacpac.
                            }
                            else
                            {
                                // not in this -> generation not needed
                            }
                        }
                        else
                        {
                            throw new InvalidProgramException("Type registration could not be found:" + parameter.ParameterTypeFullName);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException("Type registration could not be found:" + parameter.ParameterTypeFullName);
                    }
                    hm.handler_method_decl.Statements.Add(new CodeCommentStatement("  Parameter [" + ix + "] : " + parameter.ParameterName + " (" + parameter.ParameterTypeFullName + ") " + parameterType.UserDefinedTableTypeSqlFullName));
                }
            }

            CodeTypeDeclaration facade_output_type_decl;
            if (countOfResultSets > 0)
            {
                // read
                facade_output_type_decl = BuildCommandFacadeOutputType(procedureMethodName, countOfResultSets);
                ns_procedures.Types.Add(facade_output_type_decl);
            }
            else
            {
                facade_output_type_decl = null;
            }

            CommandFacadeMethod fm = BuildCommandFacadeMethod(procedures_facade_type_decl.Name, procedureMethodName, procedure, facade_output_type_decl, countOfResultSets, procedure_metadata, udt_rows);
            if (fm != null)
            {
                procedures_facade_type_decl.Members.Add(fm.facade_method_decl);

                if (!BuildInvokeFacadeMethod(extMethodAccess_HandlerFacade, fm, procedure, procedure_metadata, hm, udt_rows))
                {
                    hm.handler_method_decl.Statements.Add(new CodeThrowExceptionStatement(new CodeObjectCreateExpression(typeof(NotImplementedException))));
                }
            }
            else
            {
                hm.handler_method_decl.Statements.Add(new CodeThrowExceptionStatement(new CodeObjectCreateExpression(typeof(NotImplementedException))));
            }
        }

        private static CodeTypeDeclaration BuildCommandFacadeOutputType(string method_name, int countOfResultSets)
        {
            CodeTypeDeclaration output_type_decl = new CodeTypeDeclaration() { Name = method_name + "ResultSets" };

            CodeConstructor ctor = new CodeConstructor() { Attributes = MemberAttributes.Private };
            output_type_decl.Members.Add(ctor);

            // Create
            CodeMemberMethod method_create = new CodeMemberMethod()
            {
                Name = "Create",
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                ReturnType = new CodeTypeReference(output_type_decl.Name)
            };
            //method_create.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IDataRecord), "record"));
            var invoke_ctor = new CodeObjectCreateExpression(new CodeTypeReference(output_type_decl.Name));//, new CodeVariableReferenceExpression("record"));
            method_create.Statements.Add(new CodeMethodReturnStatement(invoke_ctor));
            output_type_decl.Members.Add(method_create);

            // CreateDataReader
            CodeMemberMethod method_reader = new CodeMemberMethod()
            {
                Name = "CreateDataReader",
                Attributes = MemberAttributes.Assembly | MemberAttributes.Final,
                ReturnType = new CodeTypeReference(typeof(System.Data.Common.DbDataReader))
            };
            //method_reader.Statements.Add(new CodeThrowExceptionStatement(new CodeObjectCreateExpression(typeof(NotImplementedException))));
            output_type_decl.Members.Add(method_reader);

            if (countOfResultSets > 1)
            {
                CodeMemberField field_tables_decl = new CodeMemberField() { Attributes = MemberAttributes.Private };
                field_tables_decl.Name = "tables";
                field_tables_decl.Type = new CodeTypeReference(new CodeTypeReference(typeof(DataTable)), 1);
                field_tables_decl.InitExpression = new CodeArrayCreateExpression(typeof(DataTable), countOfResultSets);
                output_type_decl.Members.Add(field_tables_decl);
                CodeFieldReferenceExpression field_tables_ref = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), field_tables_decl.Name);

                method_reader.Statements.Add(new CodeMethodReturnStatement(new CodeObjectCreateExpression(typeof(DataTableReader), field_tables_ref)));

                CodeVariableDeclarationStatement var_table_decl = new CodeVariableDeclarationStatement(new CodeTypeReference(typeof(DataTable)), "table");
                ctor.Statements.Add(var_table_decl);
                CodeVariableReferenceExpression var_table_ref = new CodeVariableReferenceExpression(var_table_decl.Name);
                for (int ix = 0; ix < countOfResultSets; ix++)
                {
                    CodeAssignStatement assign_table_ref = new CodeAssignStatement(var_table_ref, new CodeObjectCreateExpression(typeof(DataTable), new CodePrimitiveExpression("Output" + (1 + ix))));
                    ctor.Statements.Add(assign_table_ref);
                    // setup table....

                    // put into the array
                    CodeArrayIndexerExpression table_at = new CodeArrayIndexerExpression(field_tables_ref, new CodePrimitiveExpression(ix));
                    CodeAssignStatement assign_table_at = new CodeAssignStatement(table_at, var_table_ref);
                    ctor.Statements.Add(assign_table_at);

                    CodeMemberProperty prop_Table = new CodeMemberProperty() { Name = "OutputTable" + (1 + ix), Type = new CodeTypeReference(typeof(DataTable)), Attributes = MemberAttributes.Public | MemberAttributes.Final };
                    prop_Table.GetStatements.Add(new CodeMethodReturnStatement(table_at));
                    output_type_decl.Members.Add(prop_Table);

                }
            }
            else
            {
                CodeMemberField field_tables_decl = new CodeMemberField() { Attributes = MemberAttributes.Private };
                field_tables_decl.Name = "table";
                field_tables_decl.Type = new CodeTypeReference(typeof(DataTable));
                field_tables_decl.InitExpression = new CodeObjectCreateExpression(typeof(DataTable), new CodePrimitiveExpression("Output"));
                output_type_decl.Members.Add(field_tables_decl);
                CodeFieldReferenceExpression field_table_ref = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), field_tables_decl.Name);

                method_reader.Statements.Add(new CodeMethodReturnStatement(new CodeObjectCreateExpression(typeof(DataTableReader), field_table_ref)));

                CodeMemberProperty prop_Table = new CodeMemberProperty() { Name = "OutputTable", Type = new CodeTypeReference(typeof(DataTable)), Attributes = MemberAttributes.Public | MemberAttributes.Final };
                prop_Table.GetStatements.Add(new CodeMethodReturnStatement(field_table_ref));
                output_type_decl.Members.Add(prop_Table);
            }


            return output_type_decl;
        }

        private static bool BuildInvokeFacadeMethod(string extMethodAccess_HandlerFacade, CommandFacadeMethod fm, StoreLakeProcedureRegistration procedure, ProcedureMetadata procedure_metadata
            , CommandHandlerMethod hm
            , IDictionary<string, TableTypeRow> udt_rows
            )
        {
            var invoke_get_facade = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(
                new CodeVariableReferenceExpression("db"), extMethodAccess_HandlerFacade));

            var invoke_facade_method = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(invoke_get_facade, fm.facade_method_decl.Name));
            invoke_facade_method.Parameters.Add(new CodeVariableReferenceExpression("db"));

            for (int ix = 0; ix < procedure.Parameters.Count; ix++)
            {
                //prm_get_Int32_OrNull()
                StoreLakeParameterRegistration parameter = procedure.Parameters[ix];
                ProcedureCodeParameter parameterType = procedure_metadata.parameters[parameter.ParameterName];

                if (parameterType.IsUserDefinedTableType)
                {
                    var udtRow = udt_rows[parameter.ParameterTypeFullName];
                    string get_param_value_method_name = BuildReadCommandParameterMethodName(true, null, false);

                    var invoke_get_parameter_value = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(
                        new CodeTypeReferenceExpression(hm.handler_type_name), get_param_value_method_name));
                    invoke_get_parameter_value.Parameters.Add(new CodeVariableReferenceExpression("cmd"));
                    invoke_get_parameter_value.Parameters.Add(new CodePrimitiveExpression(parameter.ParameterName));

                    var prm_variable_rec_decl = new CodeVariableDeclarationStatement(typeof(IEnumerable<IDataRecord>), parameterType.ParameterCodeName + "_records");
                    prm_variable_rec_decl.InitExpression = invoke_get_parameter_value;
                    hm.handler_method_decl.Statements.Add(prm_variable_rec_decl);
                    var prm_variable_rec_ref = new CodeVariableReferenceExpression(prm_variable_rec_decl.Name);

                    var method_Create = new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(udtRow.ClrFullTypeName), "Create");
                    //CodeExpression method_Create = new CodeSnippetExpression(udtRow.udt_row_type_decl.Name + "." + "Create");
                    var invoke_Select = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(Enumerable)), "Select"), prm_variable_rec_ref, method_Create);
                    var invoke_ToArray = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(System.Linq.Enumerable)), "ToArray"));
                    invoke_ToArray.Parameters.Add(invoke_Select);

                    var prm_variable_decl_row = new CodeVariableDeclarationStatement(new CodeTypeReference(udtRow.ClrFullTypeName + "[]"), parameterType.ParameterCodeName + "_rows");
                    prm_variable_decl_row.InitExpression = invoke_ToArray;
                    hm.handler_method_decl.Statements.Add(prm_variable_decl_row);

                    //hm.handler_method_decl.Statements.Add(new CodeCommentStatement("" + parameterType.UserDefinedTableTypeSqlFullName));
                    invoke_facade_method.Parameters.Add(new CodeVariableReferenceExpression(prm_variable_decl_row.Name));
                    //return false;
                }
                else
                {

                    string get_param_value_method_name = BuildReadCommandParameterMethodName(false, parameterType.TypeNotNull, parameter.AllowNull);

                    var invoke_get_parameter_value = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(
                        new CodeTypeReferenceExpression(hm.handler_type_name), get_param_value_method_name));
                    invoke_get_parameter_value.Parameters.Add(new CodeVariableReferenceExpression("cmd"));
                    invoke_get_parameter_value.Parameters.Add(new CodePrimitiveExpression(parameter.ParameterName));

                    var prm_variable_decl = new CodeVariableDeclarationStatement(parameter.AllowNull ? parameterType.TypeNull : parameterType.TypeNotNull, parameterType.ParameterCodeName);
                    prm_variable_decl.InitExpression = invoke_get_parameter_value;
                    hm.handler_method_decl.Statements.Add(prm_variable_decl);

                    invoke_facade_method.Parameters.Add(new CodeVariableReferenceExpression(prm_variable_decl.Name));
                }
            }

            if (fm.CountOfResultSets > 0)
            {
                var invoke_CreateDataReader = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(invoke_facade_method, "CreateDataReader"));
                hm.handler_method_decl.Statements.Add(new CodeMethodReturnStatement(invoke_CreateDataReader));
                return true;
            }
            else
            {

                if (fm.HasReturn)
                {
                    hm.handler_method_decl.Statements.Add(new CodeMethodReturnStatement(invoke_facade_method));
                }
                else
                {
                    hm.handler_method_decl.Statements.Add(invoke_facade_method);
                    hm.handler_method_decl.Statements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(0)));
                }

                return true;
            }
        }

        class CommandFacadeMethod
        {
            internal string facade_type_name;
            internal CodeMemberMethod facade_method_decl;
            internal bool HasReturn;

            public int CountOfResultSets;
            internal CodeTypeDeclaration facade_output_type_decl;
        }

        private static CommandFacadeMethod BuildCommandFacadeMethod(string facade_type_name, string procedureMethodName, StoreLakeProcedureRegistration procedure, CodeTypeDeclaration facade_output_type_decl, int countOfResultSets, SqlDom.ProcedureMetadata procedure_metadata, IDictionary<string, TableTypeRow> udt_rows)
        {
            bool? hasReturnStatements = ProcedureGenerator.HasReturnStatement(procedure_metadata);

            CommandFacadeMethod fm = new CommandFacadeMethod()
            {
                facade_type_name = facade_type_name,
                CountOfResultSets = countOfResultSets,
                HasReturn = hasReturnStatements.GetValueOrDefault()
            };

            fm.facade_method_decl = new CodeMemberMethod() { Name = procedureMethodName, Attributes = MemberAttributes.Public };

            fm.facade_method_decl.Parameters.Add(new CodeParameterDeclarationExpression(typeof(DataSet), "db"));

            for (int ix = 0; ix < procedure.Parameters.Count; ix++)
            {
                StoreLakeParameterRegistration parameter = procedure.Parameters[ix];
                ProcedureCodeParameter parameterType = procedure_metadata.parameters[parameter.ParameterName];

                CodeParameterDeclarationExpression code_param_decl = new CodeParameterDeclarationExpression() { Name = parameterType.ParameterCodeName };
                if (parameter.ParameterDbType == SqlDbType.Structured) // IEnumerable<udtRow>
                {
                    var udtRow = udt_rows[parameter.ParameterTypeFullName];
                    code_param_decl.Type = new CodeTypeReference(typeof(IEnumerable<>));
                    code_param_decl.Type.TypeArguments.Add(new CodeTypeReference(udtRow.ClrFullTypeName));
                }
                else
                {
                    if (parameter.AllowNull)
                    {
                        code_param_decl.Type = new CodeTypeReference(parameterType.TypeNull);
                    }
                    else
                    {
                        code_param_decl.Type = new CodeTypeReference(parameterType.TypeNotNull);

                    }
                }
                fm.facade_method_decl.Parameters.Add(code_param_decl);
            }


            fm.facade_method_decl.Statements.Add(new CodeThrowExceptionStatement(new CodeObjectCreateExpression(typeof(NotImplementedException))));

            if (countOfResultSets > 0)
            {
                fm.facade_output_type_decl = facade_output_type_decl;
                fm.facade_method_decl.ReturnType = new CodeTypeReference(facade_output_type_decl.Name);
            }
            else
            {
                if (fm.HasReturn)
                {
                    // hlsyssec_inherit_groups
                    fm.facade_method_decl.ReturnType = new CodeTypeReference(typeof(int));
                }
                else
                {
                    fm.facade_method_decl.ReturnType = new CodeTypeReference(typeof(void));
                }
            }

            return fm;
        }

        internal static ProcedureCodeParameter GetParameterClrType(StoreLakeParameterRegistration parameter)
        {
            return GetParameterClrType(parameter.ParameterDbType, parameter.ParameterTypeFullName);
        }
        private static ProcedureCodeParameter GetParameterClrType(SqlDbType parameter_ParameterDbType, string parameter_ParameterTypeFullName)
        {
            // https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-data-type-mappings
            if (parameter_ParameterDbType == SqlDbType.Structured)
                return ProcedureCodeParameter.CreateUdt(parameter_ParameterTypeFullName);
            if (parameter_ParameterDbType == SqlDbType.Bit)
                return ProcedureCodeParameter.Create<bool, bool?>(DbType.Boolean);
            if (parameter_ParameterDbType == SqlDbType.NVarChar)
                return ProcedureCodeParameter.Create<string, string>(DbType.String);
            if (parameter_ParameterDbType == SqlDbType.Int)
                return ProcedureCodeParameter.Create<int, int?>(DbType.Int32);
            if (parameter_ParameterDbType == SqlDbType.BigInt)
                return ProcedureCodeParameter.Create<long, long?>(DbType.Int64);
            if (parameter_ParameterDbType == SqlDbType.SmallInt)
                return ProcedureCodeParameter.Create<short, short?>(DbType.Int16);
            if (parameter_ParameterDbType == SqlDbType.TinyInt)
                return ProcedureCodeParameter.Create<byte, byte?>(DbType.Byte);
            if (parameter_ParameterDbType == SqlDbType.UniqueIdentifier)
                return ProcedureCodeParameter.Create<Guid, Guid?>(DbType.Guid);
            if (parameter_ParameterDbType == SqlDbType.Xml)
                return ProcedureCodeParameter.Create<System.Xml.Linq.XElement, System.Xml.Linq.XElement>(DbType.Xml);
            if (parameter_ParameterDbType == SqlDbType.DateTime)
                return ProcedureCodeParameter.Create<DateTime, DateTime?>(DbType.DateTime);
            if (parameter_ParameterDbType == SqlDbType.VarBinary)
                return ProcedureCodeParameter.Create<byte[], byte[]>(DbType.Binary);
            if (parameter_ParameterDbType == SqlDbType.Decimal)
                return ProcedureCodeParameter.Create<decimal, decimal?>(DbType.Decimal);
            if (parameter_ParameterDbType == SqlDbType.NChar)
                return ProcedureCodeParameter.Create<string, string>(DbType.StringFixedLength); // taskmanagement
            if (parameter_ParameterDbType == SqlDbType.Date)
                return ProcedureCodeParameter.Create<DateTime, DateTime?>(DbType.DateTime);

            throw new NotImplementedException("" + parameter_ParameterDbType);
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
                    if (!isSetClassDeclaration && IsPublic(member_ctor.Attributes))
                    {
                        member_ctor.Attributes = MemberAttributes.Assembly;
                    }
                    if (member_ctor.Parameters.Count == 1 && IsPublic(member_ctor.Attributes))
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
                    if (IsPublic(member_method.Attributes) && !IsOverride(member_method.Attributes) && !(member_ctor != null))
                    {
                        if (IsStatic(member_method.Attributes))
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
                        if (member_method.Name.StartsWith("New") && member_method.Name.EndsWith("Row") && member_method.ReturnType.BaseType != typeof(void).FullName && IsPrivate(member_method.Attributes) && member_method.Parameters.Count == 0)
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
                    if (member_property.Type != null && member_property.Type.BaseType == typeof(DataColumn).FullName && IsPublic(member_property.Attributes))
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

                    if (isRowClassDeclaration && IsPublic(member_property.Attributes))
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

        class MyDT : DataTable
        {
            protected override void OnColumnChanging(DataColumnChangeEventArgs e)
            {
                base.OnColumnChanging(e);
            }
        }


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
        internal static IDictionary<string, string> _builtinTypeAlias = new SortedDictionary<string, string>() {
            { typeof(bool).FullName, "bool" },
            { typeof(byte).FullName, "byte" },
            { typeof(char).FullName, "char" },
            { typeof(Decimal).FullName, "decimal" },
            { typeof(double).FullName, "double" },
            { typeof(float).FullName, "float" }, // Single
            { typeof(int).FullName, "int" },
            { typeof(long).FullName, "long" },
            { typeof(short).FullName, "short" },
            { typeof(string).FullName, "string" },
        };
        private static readonly IDictionary<string, string> notnull_nullable_map = new Dictionary<string, string>() {
                        { typeof(int).FullName, "int?" },
                        { typeof(short).FullName, "short?" },
                        { typeof(bool).FullName, "bool?" },
                        { typeof(DateTime).FullName, "System.DateTime?" },
                        { typeof(Decimal).FullName, "decimal?" },
                        { typeof(Guid).FullName, "System.Guid?" },
                        { typeof(long).FullName, "long?" },
                        { typeof(float).FullName, "float?" }, // Single
                        { typeof(byte).FullName, "byte?" },
                        { typeof(string).FullName, "string" },
                        //{ typeof(byte[]).FullName, "byte[]" },
                    };
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
                } // IsValueType
                else
                {
                    throw new NotImplementedException(column.DataType.Name);
                }
            }// AllowDBNull
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
                if (notnull_nullable_map.TryGetValue(column.DataType.FullName, out string nullableType))
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

            // rename PK:
            /*
             base.Constraints.Add(new UniqueConstraint("Constraint1", new DataColumn[1]{columnagentid}, isPrimaryKey: true));
             */
            /*foreach (var stmt in member_method.Statements)
            {
                CodeExpressionStatement stmt_expr = stmt as CodeExpressionStatement;
                if (stmt_expr != null)
                {
                    CodeMethodInvokeExpression invoke_expr = stmt_expr.Expression as CodeMethodInvokeExpression;
                    if (invoke_expr != null && invoke_expr.Method.MethodName == "Add")
                    {
                        var prop_Constraints = invoke_expr.Method.TargetObject as CodePropertyReferenceExpression;

                        if (prop_Constraints != null && prop_Constraints.PropertyName == "Constraints" && (prop_Constraints.TargetObject is CodeBaseReferenceExpression || prop_Constraints.TargetObject is CodeThisReferenceExpression))
                        {
                            CodeObjectCreateExpression createExpr = (CodeObjectCreateExpression)invoke_expr.Parameters[0];
                            CodePrimitiveExpression ctor_prm_name_expr = (CodePrimitiveExpression)createExpr.Parameters[0];
                            string constraintName = (string)ctor_prm_name_expr.Value;
                            CodePrimitiveExpression ctor_prm_ispk_expr = (CodePrimitiveExpression)createExpr.Parameters[2];
                            if ((bool)ctor_prm_ispk_expr.Value)
                            {
                                //table.Constraints
                                //table.PrimaryKey
                            }
                        }
                    }
                }
            }*/

            /* [UQ_hlsysadhocprocessinstance_caseref]
             * if (table.Constraints.Count > 1) // PrimaryKey+
            {
                foreach (Constraint constraint in table.Constraints)
                {
                    UniqueConstraint uq = constraint as UniqueConstraint;
                    if (uq != null && !uq.IsPrimaryKey)
                    {
                        CodePropertyReferenceExpression prop_Constraints = new CodePropertyReferenceExpression()
                        {
                            PropertyName = "Constraints",
                            TargetObject = new CodeBaseReferenceExpression(),
                        };

                        List<CodeExpression> col_refs = new List<CodeExpression>();
                        for (int ixCol = 0; ixCol < uq.Columns.Length; ixCol++)
                        {
                            DataColumn col = uq.Columns[ixCol];
                            string column_name = "column" + col.ColumnName;
                            col_refs.Add(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), column_name));
                        }

                        CodeArrayCreateExpression prm_columns = new CodeArrayCreateExpression(typeof(DataColumn), col_refs.ToArray());

                        CodeObjectCreateExpression createExpr = new CodeObjectCreateExpression(typeof(UniqueConstraint),
                            new_CodePrimitiveExpression(uq.ConstraintName)
                            , prm_columns
                            , new_CodePrimitiveExpression(false));


                        CodeMethodInvokeExpression invoke_expr = new CodeMethodInvokeExpression();
                        invoke_expr.Method = new CodeMethodReferenceExpression() { MethodName = "Add", TargetObject = prop_Constraints };
                        invoke_expr.Parameters.Add(createExpr);

                        CodeExpressionStatement stmt_expr = new CodeExpressionStatement(invoke_expr);
                        member_method.Statements.Add(stmt_expr);
                    }
                }

            }*/
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

                                    var ifTableExists = new CodeBinaryOperatorExpression(invoke_IndexOf, CodeBinaryOperatorType.LessThan, new_CodePrimitiveExpression(0));

                                    CodeConditionStatement if_not_Exists_Add = new CodeConditionStatement(ifTableExists, stmt_expr);

                                    //new_statements.Add(stmt_expr);
                                    new_statements.Add(if_not_Exists_Add);
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

        private static bool IsPrivate(MemberAttributes attributes)
        {
            return ((attributes & MemberAttributes.Private) == MemberAttributes.Private);
        }

        private static bool IsPublic(MemberAttributes attributes)
        {
            return ((attributes & MemberAttributes.Public) == MemberAttributes.Public);
        }

        private static bool IsOverride(MemberAttributes attributes)
        {
            return ((attributes & MemberAttributes.Override) == MemberAttributes.Override);
        }

        private static bool IsStatic(MemberAttributes attributes)
        {
            return ((attributes & MemberAttributes.Static) == MemberAttributes.Static);
        }

        private static string AsTraceText(MemberAttributes attributes)
        {
            StringBuilder text = new StringBuilder();
            text.Append("" + attributes + " : ");
            if ((attributes & MemberAttributes.Public) == MemberAttributes.Public)
            {
                text.Append(" Public ");
            }
            if ((attributes & MemberAttributes.Private) == MemberAttributes.Private)
            {
                text.Append(" Private ");
            }

            if ((attributes & MemberAttributes.Static) == MemberAttributes.Static)
            {
                text.Append(" Static ");
            }

            if ((attributes & MemberAttributes.Override) == MemberAttributes.Override)
            {
                text.Append(" Override ");
            }

            if ((attributes & MemberAttributes.Family) == MemberAttributes.Family)
            {
                text.Append(" Protected ");
            }

            if ((attributes & MemberAttributes.Final) == MemberAttributes.Final)
            {
                text.Append(" sealed ");
            }

            return text.ToString();
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


        private static void CompileCode(DacPacRegistration dacpac, CompilerParameters comparam, string libdir, string outputFolder, string fileName, string outputAssemblyFullFileName, DirectoryInfo tempDirInfo, string[] codeFileNames)
        {
            string errorsFullFileName = System.IO.Path.Combine(tempDirInfo.FullName, fileName + ".errors.txt");
            string tmpDllFullFileName = System.IO.Path.Combine(tempDirInfo.FullName, fileName + ".dll");

            string snkPath = GetSnkPath(Path.Combine(tempDirInfo.FullName, "GeneratedModel.snk"));

            StringBuilder compilerOpt = new StringBuilder();
            compilerOpt.Append("/target:library ");
            compilerOpt.AppendFormat("/lib:{0} ", libdir);
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
            s_tracer.TraceEvent(TraceEventType.Verbose, 0, "Compiling assembly (ref:" + dacpac.IsReferencedPackage + "):" + outputAssemblyFullFileName);
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
                throw new StoreLakeSdkException("Compile failed: " + fn + " (" + err.Line + "," + err.Column + "): error " + err.ErrorNumber + " : " + err.ErrorText);
            }

            s_tracer.TraceEvent(TraceEventType.Verbose, 0, "  Copy generated assembly : " + outputAssemblyFullFileName);
            File.Copy(tmpDllFullFileName, outputAssemblyFullFileName, true);
        }

        private static void GenerateVersionComment(CodeNamespace codeNamespace)
        {
            codeNamespace.Comments.Add(new CodeCommentStatement(""));
            //AssemblyName name = Assembly.GetExecutingAssembly().GetName();
            //codeNamespace.Comments.Add(new CodeCommentStatement(Res.GetString("InfoVersionComment", name.Name, "4.8.3928.0")));
            codeNamespace.Comments.Add(new CodeCommentStatement(""));
        }

        //private static TextWriter CreateOutputWriter(string outputdir, string fileName, string newExtension)
        //{
        //    string fileName2 = Path.GetFileName(fileName);
        //    string path = Path.ChangeExtension(fileName2, newExtension);
        //    string text = Path.Combine(outputdir, path);
        //    return new StreamWriter(text, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        //}

        public static void ExportSchemaXsd(DataSet ds, string fileName)
        {
            ds.WriteXmlSchema(fileName); // => Writes multiple files(relations & tables!) for different namespaces!!!
            /*
            // Create a new StringBuilder object.
            System.Text.StringBuilder builder1 = new System.Text.StringBuilder();

            // Create the StringWriter object with the StringBuilder object.
            using (System.IO.StringWriter writer = new System.IO.StringWriter(builder1))
            {
                // Write the schema into the StringWriter.
                _ds.WriteXmlSchema(writer);
            }
            var schemaContent1 = builder1.ToString();

            //File.WriteAllText(fileName, schemaContent);
            //!!!  _ds.WriteXmlSchema(fileName); => Writes multiple files (relations & tables!) !!!

            XmlSchema schema;
            using (StringReader reader = new StringReader(schemaContent1))
            {
                using (XmlTextReader xmlReader = new XmlTextReader(reader))
                {
                    schema = XmlSchema.Read(xmlReader, ValidationCallback);
                }
            }
            XmlSchemaSet xSet = new XmlSchemaSet();
            xSet.Add(schema);
            xSet.XmlResolver = new MyXmlResolver(); // XmlUrlResolver
            xSet.Compile();
            // flatten
            Console.WriteLine(schema.Includes.Count);
            ResolveExternal(schema);

            System.Text.StringBuilder builder2 = new System.Text.StringBuilder();
            using (System.IO.StringWriter writer = new System.IO.StringWriter(builder2))
            {
                schema.Write(writer);
            }


            var schemaContent2 = builder1.ToString();

            File.WriteAllText(fileName, schemaContent2);
            //XmlSchemaSet*/
        }

        private static void ValidationCallback(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Warning)
                Console.Write("WARNING: ");
            else if (args.Severity == XmlSeverityType.Error)
                Console.Write("ERROR: ");

            Console.WriteLine(args.Message);
        }

        private static void ResolveExternal(XmlSchema rootSchema, XmlSchema curSchema, List<string> processed)
        {
            // Loop on all the includes
            foreach (XmlSchemaExternal external in curSchema.Includes)
            {
                XmlSchemaImport imp = external as XmlSchemaImport;
                if (string.IsNullOrEmpty(external.SchemaLocation) && external.Schema != null)
                {
                    // Avoid processing twice the same include file
                    if (!processed.Contains(external.SchemaLocation))
                    {
                        processed.Add(external.SchemaLocation);
                        XmlSchema cur = external.Schema;
                        // Recursive calls to handle includes inside the include
                        ResolveExternal(rootSchema, cur, processed);
                        // Move the items from the included schema to the root one
                        foreach (XmlSchemaObject item in cur.Items)
                        {
                            rootSchema.Items.Add(item);
                        }
                    }
                }
            }
            curSchema.Includes.Clear();
        } // ResolveExternal

        internal static void ResolveExternal(XmlSchema schema)
        {
            List<string> processed = new List<string>();
            ResolveExternal(schema, schema, processed);
        } // ResolveExternal
    }
}
