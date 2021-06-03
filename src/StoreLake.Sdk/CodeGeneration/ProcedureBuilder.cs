﻿using StoreLake.Sdk.SqlDom;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StoreLake.Sdk.CodeGeneration
{
    internal static class ProcedureBuilder
    {
        internal static readonly TraceSource s_tracer = SchemaExportCode.s_tracer;

        internal static void BuildProcedures(RegistrationResult rr, DacPacRegistration dacpac, CodeNamespace ns_procedures, ExtensionsClass exttype)
        {
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

        private static void CollectProcedureParameters(RegistrationResult rr, StoreLakeProcedureRegistration procedure, Action<StoreLakeParameterRegistration, string, ProcedureCodeParameter> collector)
        {
            for (int ix = 0; ix < procedure.Parameters.Count; ix++)
            {
                StoreLakeParameterRegistration parameter = procedure.Parameters[ix];
                string codeParameterName = parameter.ParameterName.Replace("@", "");
                ProcedureCodeParameter parameterType = TypeMap.GetParameterClrType(parameter);
                parameterType.ParameterCodeName = codeParameterName;
                collector(parameter, parameter.ParameterName, parameterType);
            }
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
                    Dictionary<string, ProcedureCodeParameter> procedureParameters = new Dictionary<string, ProcedureCodeParameter>();
                    CollectProcedureParameters(rr, procedure, (parameter, parameterName, parameterType) =>
                    {
                        procedureParameters.Add(parameterName, parameterType);
                    });

                    procedure_metadata = SqlDom.ProcedureGenerator.ParseProcedureBody(procedure.ProcedureName, procedure.ProcedureBodyScript, procedureParameters);
                    int? isQueryProcedure = SqlDom.ProcedureGenerator.IsQueryProcedure(rr.DoResolveColumnTypes, rr.SchemaMetadata(), procedure_metadata).Length;
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

        class CommandHandlerMethod
        {
            internal string handler_type_name;
            internal CodeMemberMethod handler_method_decl;
        }
        class CommandFacadeMethod
        {
            internal string facade_type_name;
            internal CodeMemberMethod facade_method_decl;
            internal bool HasReturn;

            public int CountOfResultSets;
            internal CodeTypeDeclaration facade_output_type_decl;
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

            //CollectProcedureParameters(rr, procedure, (parameter, parameterName, parameterType) =>
            for (int ix = 0; ix < procedure.Parameters.Count; ix++)
            {
                StoreLakeParameterRegistration parameter = procedure.Parameters[ix];

                //procedure_metadata.AddParameter(parameterName, parameterType);

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
                    hm.handler_method_decl.Statements.Add(new CodeCommentStatement("  Parameter [" + ix + "] : " + parameter.ParameterName + " (" + parameter.ParameterTypeFullName + ") "));
                }
            };

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


        private static string BuildReadCommandParameterMethodName(bool isUDT, Type typeNotNull, bool allowNull)
        {
            if (isUDT)
            {
                return "read_Records";
            }
            string typeName = (typeNotNull == typeof(byte[])) ? "Bytes" : typeNotNull.Name;
            return "read_" + typeName + (allowNull ? "_OrNull" : "");
        }
        private static void BuildStoreProceduresProvider(RegistrationResult rr, DacPacRegistration dacpac, CodeNamespace ns
                , ExtensionsClass exttype
                , CodeTypeDeclaration procedures_handler_type_decl, string handlerMethod, CodeTypeDeclaration procedures_facade_type_decl, string facadeMethod)
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
        private static void AddReadParameterFunctions(CodeTypeDeclaration type_decl)
        {
            //type_decl.StartDirectives.Add(new CodeRegionDirective(CodeRegionMode.Start, "Readers"));


            bool startRegion = true;
            foreach (ParameterTypeMap prm_type in TypeMap.s_ParameterTypeMap.Values)
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

    } // builder
}