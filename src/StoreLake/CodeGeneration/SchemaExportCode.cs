﻿using Dibix.TestStore.Demo;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace Dibix.TestStore.Database
{
    public static class SchemaExportCode
    {
        public static void ExportTypedDataSetCode(DataSet ds, string outputdir, string dacName)
        {
            string schemaFileName = System.IO.Path.Combine(outputdir, dacName + ".Schema.xsd");
            string filenameNoExtension = dacName + ".TestStore";

            SchemaExportCode.ExportSchemaXsd(ds, schemaFileName);
            string schemaContent = File.ReadAllText(schemaFileName);

            ImportSchemasAsDataSets(schemaContent, outputdir, filenameNoExtension, dacName);
        }

        private static void ImportSchemasAsDataSets(string schemaContent, string outputdir, string fileName, string namespaceName)
        {
            Microsoft.CSharp.CSharpCodeProvider codeProvider = new Microsoft.CSharp.CSharpCodeProvider();// //CodeDomProvider.CreateProvider(language);

            CodeCompileUnit codeCompileUnit = new CodeCompileUnit();

            GenerateDataSetClasses(codeCompileUnit, schemaContent, namespaceName, codeProvider);

            using (TextWriter textWriter = new StreamWriter(Path.Combine(outputdir, fileName + ".cs"), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            //using (TextWriter textWriter = CreateOutputWriter(Path.Combine(outputdir, path), fileName, "cs"))
            {
                codeProvider.GenerateCodeFromCompileUnit(codeCompileUnit, textWriter, null);
            }


            string fullFileName_dll = System.IO.Path.Combine(outputdir, fileName + ".dll");
            string fullFileName_err = System.IO.Path.Combine(outputdir, fileName + ".errors.txt");
            CompileCode(codeCompileUnit, fullFileName_dll, fullFileName_err);

            Console.WriteLine(fullFileName_dll);
        }

        internal static void GenerateDataSetClasses(CodeCompileUnit ccu, string schemaContent, string namespaceName, CodeDomProvider codeProvider)
        {
            CodeNamespace codeNamespace = new CodeNamespace(namespaceName);
            ccu.Namespaces.Add(codeNamespace);
            GenerateVersionComment(codeNamespace);

            System.Data.Design.TypedDataSetGenerator.Generate(schemaContent, ccu, codeNamespace, codeProvider, System.Data.Design.TypedDataSetGenerator.GenerateOption.LinqOverTypedDatasets, "zzzzzzz");

            Adjust_CCU(ccu);
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

        private static void Adjust_CCU(CodeCompileUnit ccu)
        {
            CodeNamespace extensions_type_ns = null;
            CodeTypeDeclaration extensions_type_decl = null;
            foreach (CodeNamespace ns in ccu.Namespaces)
            {
                foreach (CodeTypeDeclaration type_decl in ns.Types)
                {
                    bool isSetClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType == typeof(System.Data.DataSet).FullName;
                    if (isSetClassDeclaration)
                    {
                        extensions_type_ns = ns;
                        extensions_type_decl = CreateStaticClass(type_decl.Name + "Extensions");

                        // Create 'GetTable'
                        CodeMemberMethod method_gettable = new CodeMemberMethod() { Name = "GetTable", Attributes = MemberAttributes.Private | MemberAttributes.Static };
                        CodeTypeParameter ctp_Table = new CodeTypeParameter("TTable");
                        ctp_Table.Constraints.Add(new CodeTypeReference(typeof(DataTable)));
                        method_gettable.TypeParameters.Add(ctp_Table);
                        method_gettable.Parameters.Add(new CodeParameterDeclarationExpression(typeof(DataSet), "ds"));
                        method_gettable.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string), "tableName"));
                        method_gettable.ReturnType = new CodeTypeReference("TTable");
                        var var_decl_table = new CodeVariableDeclarationStatement("TTable", "table");
                        method_gettable.Statements.Add(var_decl_table);
                        var var_ref_table = new CodeVariableReferenceExpression("table");

                        CodePropertyReferenceExpression ds_Tables = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("ds"), "Tables");
                        CodeArrayIndexerExpression indexer = new CodeArrayIndexerExpression(ds_Tables, new CodeExpression[] { new CodeVariableReferenceExpression("tableName") });
                        CodeCastExpression cast_expr = new CodeCastExpression("TTable", indexer);
                        var_decl_table.InitExpression = cast_expr;

                        var ifTableNull = new CodeBinaryOperatorExpression(var_ref_table, CodeBinaryOperatorType.ValueEquality, new CodePrimitiveExpression(null));
                        var throwExpr = new CodeThrowExceptionStatement(new CodeObjectCreateExpression(typeof(ArgumentException), new CodeSnippetExpression("\"Table [\" + tableName + \"] could not be found.\""), new CodePrimitiveExpression("tableName")));
                        method_gettable.Statements.Add(new CodeConditionStatement(ifTableNull, throwExpr));

                        //method_gettable.Statements.Add(new CodeAssignStatement(var_ref_table, cast_expr));

                        method_gettable.Statements.Add(new CodeMethodReturnStatement(var_ref_table));
                        extensions_type_decl.Members.Add(method_gettable);

                        break;
                    }
                }

                if (extensions_type_decl != null)
                {
                    break;
                }
            }

            if (extensions_type_decl == null)
            {
                return;
            }

            foreach (CodeNamespace ns in ccu.Namespaces)
            {
                List<NestedTypeDeclaration> nestedTypes = new List<NestedTypeDeclaration>();

                foreach (CodeTypeDeclaration type_decl in ns.Types)
                {

                    Adjust_TypeDecl(extensions_type_decl, ns.Name, type_decl);


                    foreach (CodeTypeMember member_decl in type_decl.Members)
                    {
                        if (member_decl.GetType() == typeof(CodeTypeDeclaration))
                        {
                            bool vvvv = IsPublic(member_decl.Attributes);
                            nestedTypes.Add(new NestedTypeDeclaration() { Owner = type_decl, Member = (CodeTypeDeclaration)member_decl });
                        }
                    }
                }


                // MoveNestedTypesToNamespace


                foreach (NestedTypeDeclaration nested_type in nestedTypes)
                {
                    nested_type.Owner.Members.Remove(nested_type.Member);
                    ns.Types.Add(nested_type.Member);
                }
            }

            extensions_type_ns.Types.Add(extensions_type_decl);

        }

        private static void Adjust_TypeDecl(CodeTypeDeclaration extension_decl, string fullNamespaceOrOwnerTypeName, CodeTypeDeclaration type_decl)
        {
            string fullClassName = fullNamespaceOrOwnerTypeName + "." + type_decl.Name;
            Console.WriteLine("class " + fullClassName);

            //System.Data.DataSet
            bool isSetClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType == typeof(System.Data.DataSet).FullName;
            bool isTableClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType.Contains("System.Data.TypedTableBase");
            bool isRowClassDeclaration = type_decl.BaseTypes.Count > 0 && type_decl.BaseTypes[0].BaseType == typeof(System.Data.DataRow).FullName;

            if (isSetClassDeclaration)
            {
                type_decl.CustomAttributes.Clear();
                //type_decl.BaseTypes.Clear();
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
                if (member_type != null)
                {
                    //  
                    if (member_type.Name.EndsWith("RowChangeEvent"))
                    {
                        membersToRemove.Add(member_type);
                    }
                    else
                    {
                        Adjust_TypeDecl(extension_decl, fullClassName, member_type);
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
                        CodeTypeMember member_decl_x = Adjust_DataSet_Constructor(extension_decl.Name, member_ctor);
                        if (member_decl_x != null)
                        {
                            extension_decl.Members.Add(member_decl_x);
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

                    if (isTableClassDeclaration && member_ctor.Parameters.Count > 0)
                    {
                        //  hlbiattributeconfigDataTable(DataTable table)
                        if (!membersToRemove.Contains(member_ctor))
                        {
                            membersToRemove.Add(member_ctor);
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

                    if (isRowClassDeclaration && member_method.Name.StartsWith("Is") && member_method.Name.EndsWith("Null") && member_method.ReturnType != null && member_method.ReturnType.BaseType == typeof(System.Boolean).FullName)
                    {
                        // return IsNull(xxx_table.yyy_Column);
                        membersToRemove.Add(member_method);
                    }
                    else if (isRowClassDeclaration && member_method.Name.StartsWith("Set") && member_method.Name.EndsWith("Null") && member_method.ReturnType != null && member_method.ReturnType.BaseType == typeof(void).FullName)
                    {
                        // base[xxx_table.yyy_Column] = Convert.DBNull;
                        membersToRemove.Add(member_method);
                    }

                    if (isTableClassDeclaration && member_method.Name.StartsWith("Add") && member_method.Name.EndsWith("Row") && member_method.ReturnType != null && member_method.ReturnType.BaseType == typeof(void).FullName)
                    {
                        // public void AddhlbiattributeconfigRow(hlbiattributeconfigRow row)
                        membersToRemove.Add(member_method);
                    }
                    if (isTableClassDeclaration && member_method.Name.StartsWith("Add") && member_method.Name.EndsWith("Row") && member_method.ReturnType != null && member_method.ReturnType.BaseType != typeof(void).FullName)
                    {
                        // public hlcmdatamodelassociationsearchRow AddhlcmdatamodelassociationsearchRow(int associationid, int searchid)
                        member_method.Name = "AddRowWithValues";
                        member_method.Attributes = MemberAttributes.Public | MemberAttributes.Final;
                    }

                    if (isSetClassDeclaration && member_method.Name.StartsWith("ShouldSerialize") && member_method.Parameters.Count == 0 && member_method.ReturnType != null && member_method.ReturnType.BaseType == typeof(bool).FullName)
                    {
                        // bool ShouldSerializehlsysrecentsearch()
                        membersToRemove.Add(member_method);
                    }

                    if (isSetClassDeclaration && member_method.Name == "InitClass")
                    {
                        Adjust_DataSet_InitClass(member_method);
                        extension_decl.Members.Add(member_method);
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
                        CodeMemberMethod member_method_x = Adjust_DataSet_TableAccessor(extension_decl, member_property);
                        if (member_method_x != null)
                        {
                            extension_decl.Members.Add(member_method_x);
                            membersToRemove.Add(member_property);
                        }
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

            foreach (var memberToRemove in membersToRemove)
            {
                type_decl.Members.Remove(memberToRemove);
            }

            foreach (var memberToInsert in membersToInsert)
            {
                type_decl.Members.Add(memberToInsert);
            }
        }

        private static void Adjust_DataSet_InitClass(CodeMemberMethod member_method)
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

            List<CodeStatement> vars = new List<CodeStatement>();
            List<CodeStatement> old_statements = new List<CodeStatement>();

            Dictionary<string, CodeAssignStatement> field_assign_stmts = new Dictionary<string, CodeAssignStatement>();
            foreach (CodeStatement stmt in member_method.Statements)
            {



                bool skip_old = false;
                // tablehlbiattributeconfig = new hlbiattributeconfigDataTable();
                CodeAssignStatement stmt_assign = stmt as CodeAssignStatement;
                if (stmt_assign != null)
                {
                    CodeFieldReferenceExpression fieldRefExpr = stmt_assign.Left as CodeFieldReferenceExpression;
                    CodeObjectCreateExpression createObjectExpr = stmt_assign.Right as CodeObjectCreateExpression;
                    if (fieldRefExpr != null && createObjectExpr != null)
                    {
                        //CodeVariableDeclarationStatement var_decl = new CodeVariableDeclarationStatement(createObjectExpr.CreateType, fieldRefExpr.FieldName);
                        //vars.Add(var_decl);
                        field_assign_stmts.Add(fieldRefExpr.FieldName, stmt_assign);
                        skip_old = true;
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

                        skip_old = true;
                    }
                }
                else
                {

                    CodeExpressionStatement stmt_expr = stmt as CodeExpressionStatement;
                    if (stmt_expr != null)
                    {
                        CodeMethodInvokeExpression invoke_expr = stmt_expr.Expression as CodeMethodInvokeExpression;
                        if (invoke_expr != null)
                        {
                            var prop_Table = invoke_expr.Method.TargetObject as CodePropertyReferenceExpression;
                            CodeFieldReferenceExpression fieldRefExpr = invoke_expr.Parameters.Count == 1 ? invoke_expr.Parameters[0] as CodeFieldReferenceExpression : null;
                            if (fieldRefExpr != null && prop_Table != null && prop_Table.TargetObject is CodeBaseReferenceExpression && prop_Table.PropertyName == "Tables")
                            {
                                //skip_old = true;
                                prop_Table.TargetObject = prm_ref_ds;

                                CodeAssignStatement field_assign_stmt = field_assign_stmts[fieldRefExpr.FieldName];
                                CodeObjectCreateExpression createObj = (CodeObjectCreateExpression)field_assign_stmt.Right;

                                invoke_expr.Parameters.Clear();
                                invoke_expr.Parameters.Add(createObj);
                            }
                        }
                    }
                    else
                    {

                    }


                }


                if (!skip_old)
                {
                    old_statements.Add(stmt);
                }
            }

            member_method.Statements.Clear();
            foreach (var stmt in vars)
            {
                member_method.Statements.Add(stmt);
            }
            foreach (var stmt in old_statements)
            {
                member_method.Statements.Add(stmt);
            }
        }

        private static CodeMemberMethod Adjust_DataSet_TableAccessor(CodeTypeDeclaration extension_decl, CodeMemberProperty member_property)
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
            //CodeArrayIndexerExpression indexer = new CodeArrayIndexerExpression(base_Tables, new CodeExpression[] { new CodePrimitiveExpression(member_property.Name) });
            //CodeCastExpression cast_expr = new CodeCastExpression(member_property.Type, indexer);
            //member_method.Statements.Add(new CodeMethodReturnStatement(cast_expr));

            member_method.Statements.Add(new CodeMethodReturnStatement(
                             new CodeMethodInvokeExpression(
                                  new CodeMethodReferenceExpression(
                                     new CodeTypeReferenceExpression(extension_decl.Name),
                                     "GetTable",
                                             new CodeTypeReference[] { member_property.Type }),
                                              new CodeVariableReferenceExpression("ds"),
                                                       new CodePrimitiveExpression(member_property.Name))));

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



            var param_decl = new CodeParameterDeclarationExpression("this TDataSet", "ds");
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

        private static void CompileCode(CodeCompileUnit codeCompileUnit, string outputAssemblyFullFileName, string outputErrorsFullFileName)
        {
            CompilerParameters comparam = new CompilerParameters(new string[] { });

            comparam.GenerateInMemory = false;
            //Indicates whether the output is an executable.  
            comparam.GenerateExecutable = false;
            comparam.IncludeDebugInformation = true;
            comparam.TempFiles.KeepFiles = true;
            //provide the name of the class which contains the Main Entry //point method  
            //comparam.MainClass = "mynamespace.CMyclass";
            //provide the path where the generated assembly would be placed  
            comparam.OutputAssembly = outputAssemblyFullFileName;
            //Create an instance of the c# compiler and pass the assembly to //compile  
            Microsoft.CSharp.CSharpCodeProvider ccp = new Microsoft.CSharp.CSharpCodeProvider();
#pragma warning disable CS0618 // Type or member is obsolete
            ICodeCompiler icc = ccp.CreateCompiler();
#pragma warning restore CS0618 // Type or member is obsolete
            //The CompileAssemblyFromDom would either return the list of  
            //compile time errors (if any), or would create the  
            //assembly in the respective path in case of successful //compilation  
            CompilerResults compres = icc.CompileAssemblyFromDom(comparam, codeCompileUnit);
            if (compres == null || compres.Errors.Count > 0)
            {
                StringBuilder errorFileContent = new StringBuilder();
                errorFileContent.AppendLine("Errors: " + compres.Errors.Count);
                for (int i = 0; i < compres.Errors.Count; i++)
                {
                    errorFileContent.AppendLine("Error === " + i + "/ " + compres.Errors.Count + "=======================");
                    //Console.WriteLine(compres.Errors[i]);
                }

                File.WriteAllText(outputErrorsFullFileName, errorFileContent.ToString());
                Console.WriteLine(outputErrorsFullFileName);

                var err = compres.Errors[0];
                throw new InvalidOperationException("Compile failed:" + err.ErrorText + " Line:" + err.Line + ", Column:" + err.Column);
            }
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
