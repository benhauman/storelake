using StoreLake.Sdk.SqlDom;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
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

            ProcedureHandlerRegistry hreg = BuildProcedureRegistry(procedures_handler_type_decl);

            BuildStoreProceduresHandlerType(rr, dacpac, exttype.extensions_type_decl, ns_procedures, hreg, procedures_handler_type_decl, procedures_facade_type_decl, extMethodAccess_HandlerFacade, rr.udt_rows);
            BuildStoreProceduresProvider(rr, dacpac, ns_procedures, exttype, procedures_handler_type_decl, extMethodAccess_CommandExecuteHandler, procedures_facade_type_decl, extMethodAccess_HandlerFacade);

            hreg.FinishRegistration();
        }

        class ProcedureHandlerRegistry
        {
            internal CodeVariableReferenceExpression dict_exec_ref;
            internal CodeVariableReferenceExpression dict_read_ref;
            internal CodeMemberMethod InitializeExec_decl;
            internal CodeMemberMethod InitializeRead_decl;
            private CodeTypeDeclaration procedures_handler_type_decl;

            public ProcedureHandlerRegistry(CodeTypeDeclaration procedures_handler_type_decl)
            {
                this.procedures_handler_type_decl = procedures_handler_type_decl;
            }

            internal void FinishRegistration()
            {
                InitializeExec_decl.Statements.Add(new CodeMethodReturnStatement(dict_exec_ref));
                InitializeRead_decl.Statements.Add(new CodeMethodReturnStatement(dict_read_ref));
            }

            internal void RegisterProcedureRead(ProcedureMetadata procedure_metadata, CommandHandlerMethod hm)
            {
                //dict.Add("hlsyssec_cache_refresh", x => x.hlsyssec_cache_refresh);
                if (hm.IsQueryProcedure)
                {
                    var invoke_Add = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(dict_read_ref, "Add")
                            , new CodePrimitiveExpression(procedure_metadata.ProcedureFullName)
                            , new CodeSnippetExpression("x => x." + hm.handler_method_decl.Name)
                            );
                    InitializeRead_decl.Statements.Add(invoke_Add);
                }
                else
                {
                    var invoke_Add = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(dict_exec_ref, "Add")
                            , new CodePrimitiveExpression(procedure_metadata.ProcedureFullName)
                            , new CodeSnippetExpression("x => x." + hm.handler_method_decl.Name)
                            );
                    InitializeExec_decl.Statements.Add(invoke_Add);
                }
            }
        }

        private static ProcedureHandlerRegistry BuildProcedureRegistry(CodeTypeDeclaration procedures_handler_type_decl)
        {
            ProcedureHandlerRegistry hreg = new ProcedureHandlerRegistry(procedures_handler_type_decl);
            // Exec
            BuildHandlerRegistryMethod(procedures_handler_type_decl, "s_handlers_exec", "InitializeHandlersExec", "TryGetHandlerForCommandExecuteProcedureNonQuery"
                , typeof(Func<DataSet, DbCommand, int>)
                , (InitializeMethod_decl, dict_ref) =>
            {
                hreg.InitializeExec_decl = InitializeMethod_decl;
                hreg.dict_exec_ref = dict_ref;
            });

            // Read
            BuildHandlerRegistryMethod(procedures_handler_type_decl, "s_handlers_read", "InitializeHandlersRead", "TryGetHandlerForCommandExecuteProcedureQuery"
                , typeof(Func<DataSet, DbCommand, DbDataReader>)
                , (InitializeMethod_decl, dict_ref) =>
            {
                hreg.InitializeRead_decl = InitializeMethod_decl;
                hreg.dict_read_ref = dict_ref;
            });


            return hreg;
        }

        private static void BuildHandlerRegistryMethod(CodeTypeDeclaration procedures_handler_type_decl, string fieldName
            , string methodNameInit, string methodNameGet
            , Type methodSigniture
            , Action<CodeMemberMethod, CodeVariableReferenceExpression> collector)
        {
            var InitializeExec_decl = new CodeMemberMethod() { Name = methodNameInit, Attributes = MemberAttributes.Private | MemberAttributes.Static };
            procedures_handler_type_decl.Members.Add(InitializeExec_decl);

            //Func<DataSet, DbCommand, int>;
            CodeTypeReference T_Func__DataSet_DbCommand_int = new CodeTypeReference(methodSigniture);// typeof(Func<DataSet, DbCommand, int>));
            CodeTypeReference T_Func_Provider = new CodeTypeReference(typeof(Func<,>));
            T_Func_Provider.TypeArguments.Add(new CodeTypeReference(procedures_handler_type_decl.Name));
            T_Func_Provider.TypeArguments.Add(T_Func__DataSet_DbCommand_int);
            CodeTypeReference T_Dictionary = new CodeTypeReference(typeof(Dictionary<,>)); ;//<string, Func_provider>
            T_Dictionary.TypeArguments.Add(typeof(string));
            T_Dictionary.TypeArguments.Add(T_Func_Provider);

            var dict_exec_decl = new CodeVariableDeclarationStatement(T_Dictionary, "dct");
            dict_exec_decl.InitExpression = new CodeObjectCreateExpression(dict_exec_decl.Type, new CodePropertyReferenceExpression(new CodeTypeReferenceExpression(typeof(StringComparer)), "OrdinalIgnoreCase"));

            InitializeExec_decl.Statements.Add(dict_exec_decl);
            var dict_exec_ref = new CodeVariableReferenceExpression(dict_exec_decl.Name);

            InitializeExec_decl.ReturnType = dict_exec_decl.Type;


            CodeMemberField s_handlers_exec = new CodeMemberField(dict_exec_decl.Type, fieldName) { Attributes = MemberAttributes.Private | MemberAttributes.Static };
            s_handlers_exec.InitExpression = new CodeMethodInvokeExpression(null, InitializeExec_decl.Name);
            procedures_handler_type_decl.Members.Add(s_handlers_exec);

            // TryGet
            CodeMemberMethod TryGetHandler_decl = new CodeMemberMethod() { Name = methodNameGet, Attributes = MemberAttributes.Public | MemberAttributes.Static };
            procedures_handler_type_decl.Members.Add(TryGetHandler_decl);
            var prm_handlers_decl = new CodeParameterDeclarationExpression(procedures_handler_type_decl.Name, "handlers");
            TryGetHandler_decl.Parameters.Add(prm_handlers_decl);
            var prm_name_decl = new CodeParameterDeclarationExpression(typeof(string), "name");
            TryGetHandler_decl.Parameters.Add(prm_name_decl);

            TryGetHandler_decl.ReturnType = new CodeTypeReference(methodSigniture);
            CodeVariableDeclarationStatement var_handler_decl = new CodeVariableDeclarationStatement(T_Func_Provider, "handler");
            TryGetHandler_decl.Statements.Add(var_handler_decl);

            var invoke_TryGetValue = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(procedures_handler_type_decl.Name), s_handlers_exec.Name), "TryGetValue"));
            invoke_TryGetValue.Parameters.Add(new CodeArgumentReferenceExpression(prm_name_decl.Name));
            CodeDirectionExpression out_handler = new CodeDirectionExpression(FieldDirection.Out, new CodeVariableReferenceExpression(var_handler_decl.Name));
            invoke_TryGetValue.Parameters.Add(out_handler);
            TryGetHandler_decl.Statements.Add(invoke_TryGetValue);

            var invoke_Func = new CodeDelegateInvokeExpression(new CodeVariableReferenceExpression(var_handler_decl.Name), new CodeArgumentReferenceExpression(prm_handlers_decl.Name));
            //TryGetHandler_decl.Statements.Add(invoke_Func);

            var if_TryGetValue = new CodeConditionStatement(invoke_TryGetValue, new CodeMethodReturnStatement(invoke_Func));
            if_TryGetValue.FalseStatements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(null)));
            TryGetHandler_decl.Statements.Add(if_TryGetValue);

            //TryGetHandler_decl.Statements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(null)));
            collector(InitializeExec_decl, dict_exec_ref);

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

        private static void BuildStoreProceduresHandlerType(RegistrationResult rr, DacPacRegistration dacpac, CodeTypeDeclaration extensions_type_decl, CodeNamespace ns_procedures
            , ProcedureHandlerRegistry hreg
            , CodeTypeDeclaration procedures_handler_type_decl
            , CodeTypeDeclaration procedures_facade_type_decl
            , string extMethodAccess_HandlerFacade
            , IDictionary<string, TableTypeRow> udt_rows)
        {
            AddReadParameterFunctions(procedures_handler_type_decl);

            Console.WriteLine("Procedures found:" + dacpac.registered_Procedures.Values.Count);
            foreach (var procedure in dacpac.registered_Procedures.Values)
            {
                //Console.WriteLine("" + procedure.ProcedureSchemaName + "." + procedure.ProcedureName);

                ProcedureOutputSet[] procedureOutputResultSets;
                string procedureFacadeMethodName;
                SqlDom.ProcedureMetadata procedure_metadata;
                try
                {
                    Dictionary<string, ProcedureCodeParameter> procedureParameters = new Dictionary<string, ProcedureCodeParameter>();
                    CollectProcedureParameters(rr, procedure, (parameter, parameterName, parameterType) =>
                    {
                        procedureParameters.Add(parameterName, parameterType);
                    });

                    procedure_metadata = SqlDom.ProcedureGenerator.ParseProcedureBody(rr.ds.Namespace, procedure.ProcedureName, procedure.ProcedureBodyScript, procedureParameters);
                    procedureOutputResultSets = SqlDom.ProcedureGenerator.IsQueryProcedure(rr.DoResolveColumnTypes, rr.SchemaMetadata(), procedure_metadata);
                    int anno_countOfResultSets = procedure.Annotations.Count(x => x.AnnotationKey == "Return");
                    if (anno_countOfResultSets > 0)
                    {
                        if (procedureOutputResultSets.Length == anno_countOfResultSets)
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
                        if (procedureOutputResultSets.Length > 0)
                        {
                            // uups => no annotations =>
                            //[hlcmgetcontact]
                            //countOfResultSets = 1; // or more?
                        }
                        else
                        {
                            // ok
                        }
                    }
                    var annotation_Name = procedure.Annotations.SingleOrDefault(x => x.AnnotationKey == "Name");
                    if (annotation_Name != null)
                    {
                        procedureFacadeMethodName = annotation_Name.AnnotationValue;
                    }
                    else
                    {
                        procedureFacadeMethodName = procedure.ProcedureName;
                    }

                    if (procedureOutputResultSets != null)
                    {
                        GenerateProcedureDeclaration(rr, dacpac, extMethodAccess_HandlerFacade, procedures_handler_type_decl, hreg, procedure, procedureOutputResultSets, procedureFacadeMethodName, ns_procedures, procedures_facade_type_decl, procedure_metadata, udt_rows);
                    }
                    else
                    {
                        s_tracer.TraceEvent(TraceEventType.Warning, 0, "SKIP procedure  [" + procedure.ProcedureName + "] generation failed.");
                    }
                }
                catch (Exception ex)
                {
                    s_tracer.TraceEvent(TraceEventType.Warning, 0, "Procedure  [" + procedure.ProcedureName + "] generation failed." + ex.Message);
                    procedureOutputResultSets = null;
                    procedureFacadeMethodName = procedure.ProcedureName;
                    procedure_metadata = null;
                }


            } // foreach procedure
        }

        class CommandHandlerMethod
        {
            internal string handler_type_name;
            internal CodeMemberMethod handler_method_decl;
            internal bool IsQueryProcedure;
        }
        class CommandFacadeMethod
        {
            internal string facade_type_name;
            internal CodeMemberMethod facade_method_decl;
            internal bool HasReturn;

            public ProcedureOutputSet[] OutputResultSets;
            internal CodeTypeDeclaration facade_output_type_decl;
            internal CodeParameterDeclarationExpression facade_method_decl_prm_output;
        }

        private static void GenerateProcedureDeclaration(RegistrationResult rr
            , DacPacRegistration dacpac
            , string extMethodAccess_HandlerFacade
            , CodeTypeDeclaration procedures_type_decl
            , ProcedureHandlerRegistry hreg
            , StoreLakeProcedureRegistration procedure
            , ProcedureOutputSet[] procedureOutputResultSets
            , string facadeMethodName
            , CodeNamespace ns_procedures, CodeTypeDeclaration procedures_facade_type_decl, SqlDom.ProcedureMetadata procedure_metadata, IDictionary<string, TableTypeRow> udt_rows)
        {
            bool? hasReturnStatements = ProcedureGenerator.HasReturnStatement(procedure_metadata);

            CommandHandlerMethod hm = new CommandHandlerMethod() { handler_type_name = procedures_type_decl.Name };
            hm.handler_method_decl = new CodeMemberMethod()
            {
                Name = procedure.ProcedureName, //procedureMethodName,
                Attributes = MemberAttributes.Public
            };
            procedures_type_decl.Members.Add(hm.handler_method_decl);
            hm.handler_method_decl.Parameters.Add(new CodeParameterDeclarationExpression(typeof(DataSet), "db"));
            hm.handler_method_decl.Parameters.Add(new CodeParameterDeclarationExpression(typeof(System.Data.Common.DbCommand), "cmd"));

            Type returnType = procedureOutputResultSets.Length > 0
                ? typeof(System.Data.Common.DbDataReader)
                : typeof(int);
            //: hasReturnStatements.GetValueOrDefault()
            //    ? typeof(int) : typeof(void);
            hm.IsQueryProcedure = procedureOutputResultSets.Length > 0;
            hm.handler_method_decl.ReturnType = new CodeTypeReference(returnType);

            hm.handler_method_decl.Statements.Add(new CodeCommentStatement("Procedure: " + procedure.ProcedureSchemaName + "." + procedure.ProcedureName));
            //hm.handler_method_decl.Statements.Add(new CodeCommentStatement("Parameters:" + procedure.Parameters.Count));

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
            if (procedureOutputResultSets.Length > 0)
            {
                // read
                //try
                //{
                facade_output_type_decl = BuildCommandFacadeOutputType(procedure, facadeMethodName, procedureOutputResultSets);
                ns_procedures.Types.Add(facade_output_type_decl);
                //}
                //catch (Exception ex)
                //{
                //    s_tracer.TraceEvent(TraceEventType.Warning, 0, "Procedure  [" + procedure.ProcedureName + "] generation failed." + ex.Message);
                //    return;
                //}
            }
            else
            {
                facade_output_type_decl = null;
            }

            CommandFacadeMethod fm = BuildCommandFacadeMethod(dacpac, procedures_facade_type_decl.Name, facadeMethodName, procedure, facade_output_type_decl, procedureOutputResultSets, procedure_metadata, udt_rows);
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


            hreg.RegisterProcedureRead(procedure_metadata, hm);
        }

        private static CodeTypeDeclaration BuildCommandFacadeOutputType(StoreLakeProcedureRegistration procedure, string facadeMethodName, ProcedureOutputSet[] procedureOutputResultSets)
        {
            CodeTypeDeclaration output_type_decl = new CodeTypeDeclaration() { Name = facadeMethodName + "ResultSets" };

            CodeConstructor ctor = new CodeConstructor() { Attributes = MemberAttributes.Assembly };
            output_type_decl.Members.Add(ctor);

            // Create
            //CodeMemberMethod method_create = new CodeMemberMethod()
            //{
            //    Name = "Create",
            //    Attributes = MemberAttributes.Public | MemberAttributes.Static,
            //    ReturnType = new CodeTypeReference(output_type_decl.Name)
            //};
            ////method_create.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IDataRecord), "record"));
            //var invoke_ctor = new CodeObjectCreateExpression(new CodeTypeReference(output_type_decl.Name));//, new CodeVariableReferenceExpression("record"));
            //method_create.Statements.Add(new CodeMethodReturnStatement(invoke_ctor));
            //output_type_decl.Members.Add(method_create);

            // CreateDataReader
            CodeMemberMethod method_reader = new CodeMemberMethod()
            {
                Name = "CreateDataReader",
                Attributes = MemberAttributes.Assembly | MemberAttributes.Final,
                ReturnType = new CodeTypeReference(typeof(System.Data.Common.DbDataReader))
            };
            //method_reader.Statements.Add(new CodeThrowExceptionStatement(new CodeObjectCreateExpression(typeof(NotImplementedException))));
            output_type_decl.Members.Add(method_reader);

            if (procedureOutputResultSets.Length > 1)
            {
                CodeMemberField field_tables_decl = new CodeMemberField() { Attributes = MemberAttributes.Private };
                field_tables_decl.Name = "tables";
                field_tables_decl.Type = new CodeTypeReference(new CodeTypeReference(typeof(DataTable)), 1);
                field_tables_decl.InitExpression = new CodeArrayCreateExpression(typeof(DataTable), procedureOutputResultSets.Length);
                output_type_decl.Members.Add(field_tables_decl);
                CodeFieldReferenceExpression field_tables_ref = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), field_tables_decl.Name);

                method_reader.Statements.Add(new CodeMethodReturnStatement(new CodeObjectCreateExpression(typeof(DataTableReader), field_tables_ref)));

                CodeVariableDeclarationStatement var_table_decl = new CodeVariableDeclarationStatement(new CodeTypeReference(typeof(DataTable)), "table");
                ctor.Statements.Add(var_table_decl);
                CodeVariableReferenceExpression var_table_ref = new CodeVariableReferenceExpression(var_table_decl.Name);
                for (int ix = 0; ix < procedureOutputResultSets.Length; ix++)
                {
                    ProcedureOutputSet procedureOutputResultSet = procedureOutputResultSets[ix];
                    CodeAssignStatement assign_table_ref = new CodeAssignStatement(var_table_ref, new CodeObjectCreateExpression(typeof(DataTable), new CodePrimitiveExpression("Output" + (1 + ix))));
                    ctor.Statements.Add(assign_table_ref);

                    // setup table columns....
                    SetupOutputSetTableColumns(procedure, ctor.Statements, procedureOutputResultSet, var_table_ref, (ix + 1));

                    // put into the array
                    CodeArrayIndexerExpression table_at = new CodeArrayIndexerExpression(field_tables_ref, new CodePrimitiveExpression(ix));
                    CodeAssignStatement assign_table_at = new CodeAssignStatement(table_at, var_table_ref);
                    ctor.Statements.Add(assign_table_at);

                    CodeMemberProperty prop_Table = new CodeMemberProperty() { Name = "OutputTable" + (1 + ix), Type = new CodeTypeReference(typeof(DataTable)), Attributes = MemberAttributes.Public | MemberAttributes.Final };
                    prop_Table.GetStatements.Add(new CodeMethodReturnStatement(table_at));
                    output_type_decl.Members.Add(prop_Table);

                    // Set1AddRow()
                    string methodName = "Set" + (ix + 1) + "AddRow";
                    BuildFacadeOutputTableAddRow(procedureOutputResultSet, output_type_decl, methodName, table_at);
                }
            }
            else
            {
                ProcedureOutputSet procedureOutputResultSet = procedureOutputResultSets[0];
                CodeMemberField field_tables_decl = new CodeMemberField() { Attributes = MemberAttributes.Private };
                field_tables_decl.Name = "table";
                field_tables_decl.Type = new CodeTypeReference(typeof(DataTable));
                field_tables_decl.InitExpression = new CodeObjectCreateExpression(typeof(DataTable), new CodePrimitiveExpression("Output"));
                output_type_decl.Members.Add(field_tables_decl);
                CodeFieldReferenceExpression field_table_ref = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), field_tables_decl.Name);

                method_reader.Statements.Add(new CodeMethodReturnStatement(new CodeObjectCreateExpression(typeof(DataTableReader), field_table_ref)));

                // setup table columns....
                try
                {
                    SetupOutputSetTableColumns(procedure, ctor.Statements, procedureOutputResultSet, field_table_ref, 1);


                    CodeMemberProperty prop_Table = new CodeMemberProperty() { Name = "OutputTable", Type = new CodeTypeReference(typeof(DataTable)), Attributes = MemberAttributes.Public | MemberAttributes.Final };
                    prop_Table.GetStatements.Add(new CodeMethodReturnStatement(field_table_ref));
                    output_type_decl.Members.Add(prop_Table);

                    BuildFacadeOutputTableAddRow(procedureOutputResultSet, output_type_decl, "AddRow", field_table_ref);
                }
                catch (Exception ex)
                {
                    // hlsyssession_connectimplementation
                    s_tracer.TraceEvent(TraceEventType.Warning, 0, "Procedure  [" + procedure.ProcedureName + "] generation failed." + ex.Message);
                }
                
            }


            return output_type_decl;
        }

        private static void SetupOutputSetTableColumns(StoreLakeProcedureRegistration procedure, CodeStatementCollection statements, ProcedureOutputSet outputResultSet, CodeExpression tableRef, int outputSetIndex)
        {
            var table_Columns = new CodePropertyReferenceExpression(tableRef, "Columns");

            IDictionary<string, ProcedureOutputColumn> outputColumnNames = new SortedDictionary<string, ProcedureOutputColumn>(StringComparer.OrdinalIgnoreCase);
            for (int ix = 0; ix < outputResultSet.ColumnCount; ix++)
            {
                ProcedureOutputColumn outputColumn = outputResultSet.ColumnAt(ix);
                string outputColumnName = ProcedureOutputSet.PrepareOutputColumnName(outputResultSet, outputColumn, outputColumnNames.Keys, ix);
                outputColumnNames.Add(outputColumnName, outputColumn);
                if (!outputColumn.ColumnDbType.HasValue)
                {
                    // ["hlsyssession_connectimplementation"], 'DaysSinceSaasExpiration' 
                    throw new InvalidOperationException("Output column type could not be resolved. Column:" + outputColumn.OutputColumnName + " Procedure:" + procedure.ProcedureName);
                }
                Type columnClrType = TypeMap.ResolveColumnClrType(outputColumn.ColumnDbType.Value);

                // System.Data.DataColumn t1_id = new System.Data.DataColumn("id", typeof(int));
                // t1_id.AllowDBNull = false;
                // t1_id.ReadOnly = true;
                // table.Columns.Add(t1_id);

                var column_decl = new CodeVariableDeclarationStatement(typeof(DataColumn), "t" + outputSetIndex + "_" + outputColumnName);
                column_decl.InitExpression = new CodeObjectCreateExpression(typeof(DataColumn), new CodePrimitiveExpression(outputColumnName), new CodeTypeOfExpression(columnClrType));
                statements.Add(column_decl);
                var column_ref = new CodeVariableReferenceExpression(column_decl.Name);

                statements.Add(new CodeAssignStatement(new CodePropertyReferenceExpression(column_ref, "AllowDBNull"), new CodePrimitiveExpression(true)));
                statements.Add(new CodeAssignStatement(new CodePropertyReferenceExpression(column_ref, "ReadOnly"), new CodePrimitiveExpression(true)));

                var invoke_Columns_Add = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(table_Columns, "Add"));

                invoke_Columns_Add.Parameters.Add(column_ref);

                statements.Add(invoke_Columns_Add);
            }
        }

        private static void BuildFacadeOutputTableAddRow(ProcedureOutputSet procedureOutputResultSet, CodeTypeDeclaration output_type_decl, string methodName, CodeExpression tableRef)
        {
            CodeMemberMethod method_AddRow_decl = new CodeMemberMethod()
            {
                Name = methodName,
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                ReturnType = new CodeTypeReference(output_type_decl.Name)
            };

            output_type_decl.Members.Add(method_AddRow_decl);

            // AddRowWithValues:
            // object[] columnValuesArray = new object[] {....}
            // DataRow row = this.NewRow();
            // row.ItemArray = columnValuesArray;
            // table.Rows.Add(row);

            var columnValuesArray_Init = new CodeArrayCreateExpression(typeof(object), procedureOutputResultSet.ColumnCount);


            IDictionary<string, ProcedureOutputColumn> outputColumnNames = new SortedDictionary<string, ProcedureOutputColumn>(StringComparer.OrdinalIgnoreCase);
            for (int ix = 0; ix < procedureOutputResultSet.ColumnCount; ix++)
            {
                ProcedureOutputColumn outputColumn = procedureOutputResultSet.ColumnAt(ix);
                string outputColumnName = ProcedureOutputSet.PrepareOutputColumnName(procedureOutputResultSet, outputColumn, outputColumnNames.Keys, ix);
                outputColumnNames.Add(outputColumnName, outputColumn);

                Type columnClrType = TypeMap.ResolveColumnClrType(outputColumn.ColumnDbType.Value);

                CodeTypeReference parameterTypeRef;
                if (columnClrType.IsValueType)
                {
                    parameterTypeRef = new CodeTypeReference(typeof(Nullable<>));
                    parameterTypeRef.TypeArguments.Add(columnClrType);
                }
                else
                {
                    parameterTypeRef = new CodeTypeReference(columnClrType);
                }

                CodeParameterDeclarationExpression param_decl = new CodeParameterDeclarationExpression(parameterTypeRef, outputColumnName);

                method_AddRow_decl.Parameters.Add(param_decl);

                columnValuesArray_Init.Initializers.Add(new CodeVariableReferenceExpression(param_decl.Name));
            }

            //// object[] columnValuesArray = new object[] {....}
            //var columnValuesArray_decl = new CodeVariableDeclarationStatement(typeof(object[]), "columnValuesArray");
            //columnValuesArray_decl.InitExpression = columnValuesArray_Init;
            //method_AddRow_decl.Statements.Add(columnValuesArray_decl);

            // DataRow row = this.NewRow();
            var row_decl = new CodeVariableDeclarationStatement(typeof(DataRow), "row");
            row_decl.InitExpression = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(tableRef, "NewRow"));
            method_AddRow_decl.Statements.Add(row_decl);

            // row.ItemArray = columnValuesArray;
            var row_ItemArray = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(row_decl.Name), "ItemArray");
            var assign_row_ItemArray = new CodeAssignStatement(row_ItemArray, columnValuesArray_Init);// new CodeVariableReferenceExpression(columnValuesArray_decl.Name));
            method_AddRow_decl.Statements.Add(assign_row_ItemArray);

            // table.Rows.Add(row);
            var table_Rows = new CodePropertyReferenceExpression(tableRef, "Rows");
            var invoke_Rows_Add = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(table_Rows, "Add"), new CodeVariableReferenceExpression(row_decl.Name));
            method_AddRow_decl.Statements.Add(invoke_Rows_Add);


            method_AddRow_decl.Statements.Add(new CodeMethodReturnStatement(new CodeThisReferenceExpression()));
        }

        private static bool BuildInvokeFacadeMethod(string extMethodAccess_HandlerFacade
            , CommandFacadeMethod fm
            , StoreLakeProcedureRegistration procedure
            , ProcedureMetadata procedure_metadata
            , CommandHandlerMethod hm
            , IDictionary<string, TableTypeRow> udt_rows
            )
        {
            var invoke_get_facade = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(
                new CodeVariableReferenceExpression("db"), extMethodAccess_HandlerFacade));

            var invoke_facade_method = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(invoke_get_facade, fm.facade_method_decl.Name));
            invoke_facade_method.Parameters.Add(new CodeVariableReferenceExpression("db"));

            if (fm.OutputResultSets.Length > 0)
            {
                var var_output = new CodeVariableDeclarationStatement(fm.facade_output_type_decl.Name, fm.facade_method_decl_prm_output.Name);
                var_output.InitExpression = new CodeObjectCreateExpression(fm.facade_output_type_decl.Name);
                hm.handler_method_decl.Statements.Add(var_output);
                invoke_facade_method.Parameters.Add(new CodeVariableReferenceExpression(var_output.Name));
            }

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

            if (fm.OutputResultSets.Length > 0)
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
        private static void BuildStoreProceduresProvider(RegistrationResult rr
                , DacPacRegistration dacpac
                , CodeNamespace ns
                , ExtensionsClass exttype
                , CodeTypeDeclaration procedures_handler_type_decl
                , string handlerMethod
                , CodeTypeDeclaration procedures_facade_type_decl
                , string facadeMethod)
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

            var Table_TableName_Ref = new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(procedure_provider_DataTable_Type_decl.Name), Table_TableName.Name);
            var method_decl_get_handler = BuildExtensionMethodsForProcedures(exttype, procedures_handler_type_decl, handlerMethod, procedure_provider_DataTable_Type_decl, Table_TableName_Ref, field_handlerInstanceCommandExecute);
            var method_decl_get_facade = BuildExtensionMethodsForProcedures(exttype, procedures_facade_type_decl, facadeMethod, procedure_provider_DataTable_Type_decl, Table_TableName_Ref, field_handlerInstanceFacade);

            // Register table in DataSet initialization
            /*
            if ((ds.Tables.IndexOf(xxx.xxxTableName) < 0)) {
                ds.Tables.Add(new xxx());
            } 
             */
            var prop_ds_Tables = new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("ds"), "Tables");
            CodeMethodInvokeExpression invoke_IndexOf = new CodeMethodInvokeExpression(prop_ds_Tables, "IndexOf", new CodeExpression[] {
                                        Table_TableName_Ref
                                    });
            var ifTableNotExists = new CodeBinaryOperatorExpression(invoke_IndexOf, CodeBinaryOperatorType.LessThan, new CodePrimitiveExpression(0));

            CodeMethodInvokeExpression invoke_AddTable = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(prop_ds_Tables, "Add"), new CodeObjectCreateExpression(procedure_provider_DataTable_Type_decl.Name));
            CodeConditionStatement if_table_not_Exists_Add = new CodeConditionStatement(ifTableNotExists);
            if_table_not_Exists_Add.TrueStatements.Add(invoke_AddTable);


            exttype.extensions_method_InitDataSet.Statements.Add(if_table_not_Exists_Add);

        }


        private static CodeMemberMethod BuildExtensionMethodsForProcedures(ExtensionsClass exttype,
            CodeTypeDeclaration procedures_type_decl, string extensionMethodNameGet,
            CodeTypeDeclaration procedure_provider_DataTable_Type_decl,
            CodeFieldReferenceExpression Table_TableName_Ref,
            CodeMemberField field_handlerInstance)
        {
            CodeMemberMethod extensions_method_GetHandler = new CodeMemberMethod()
            {
                Name = extensionMethodNameGet,
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                ReturnType = new CodeTypeReference(procedures_type_decl.Name)
            };
            //CodeTypeParameter ctp = new CodeTypeParameter("TDataSet");
            //ctp.Constraints.Add(new CodeTypeReference(typeof(DataSet)));
            //extensions_method_GetHandler.TypeParameters.Add(ctp);


            var param_decl_ds = new CodeParameterDeclarationExpression("this " + typeof(DataSet).FullName, "ds");
            extensions_method_GetHandler.Parameters.Add(param_decl_ds);

            var method_GetTable_Handlers = new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(exttype.extensions_type_decl.Name),
                            exttype.extensions_method_GetTable.Name, new CodeTypeReference[] {
                            new CodeTypeReference( procedure_provider_DataTable_Type_decl.Name)
                            });

            var invoke_GetTable_Handlers = new CodeMethodInvokeExpression(method_GetTable_Handlers, new CodeExpression[] {
                new CodeVariableReferenceExpression("ds")
                , Table_TableName_Ref // new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(procedure_provider_DataTable_Type_decl.Name), Table_TableName.Name)
            });

            var field_ref_handlerInstanceCommandExecute = new CodeFieldReferenceExpression(invoke_GetTable_Handlers, field_handlerInstance.Name);
            extensions_method_GetHandler.Statements.Add(new CodeMethodReturnStatement(field_ref_handlerInstanceCommandExecute));


            exttype.extensions_type_decl.Members.Add(extensions_method_GetHandler);

            CodeMemberMethod extensions_method_SetHandler = new CodeMemberMethod()
            {
                Name = "SetCommandExecuteHandlerInstanceFor" + extensionMethodNameGet, // SetHandlerFacadeInstanceDor
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                ReturnType = new CodeTypeReference(typeof(DataSet)),//"TDataSet")
            };
            //CodeTypeParameter ctp_set_ds = new CodeTypeParameter("DataSet");
            //ctp_set_ds.Constraints.Add(new CodeTypeReference(typeof(DataSet)));
            //extensions_method_SetHandler.TypeParameters.Add(ctp_set_ds);
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


        private static CommandFacadeMethod BuildCommandFacadeMethod(DacPacRegistration dacpac, string facade_type_name, string procedureMethodName, StoreLakeProcedureRegistration procedure, CodeTypeDeclaration facade_output_type_decl, ProcedureOutputSet[] procedureOutputResultSets, SqlDom.ProcedureMetadata procedure_metadata, IDictionary<string, TableTypeRow> udt_rows)
        {
            bool? hasReturnStatements = ProcedureGenerator.HasReturnStatement(procedure_metadata);

            CommandFacadeMethod fm = new CommandFacadeMethod()
            {
                facade_type_name = facade_type_name,
                OutputResultSets = procedureOutputResultSets,
                HasReturn = hasReturnStatements.GetValueOrDefault(),
            };

            fm.facade_method_decl = new CodeMemberMethod() { Name = procedureMethodName, Attributes = MemberAttributes.Public };

            fm.facade_method_decl.Parameters.Add(new CodeParameterDeclarationExpression(typeof(DataSet), "db"));

            if (procedureOutputResultSets.Length > 0)
            {
                fm.facade_method_decl_prm_output = new CodeParameterDeclarationExpression(facade_output_type_decl.Name, "output");
                fm.facade_method_decl.Parameters.Add(fm.facade_method_decl_prm_output);
            }

            for (int ix = 0; ix < procedure.Parameters.Count; ix++)
            {
                StoreLakeParameterRegistration parameter = procedure.Parameters[ix];
                ProcedureCodeParameter parameterType = procedure_metadata.parameters[parameter.ParameterName];

                CodeParameterDeclarationExpression code_param_decl = new CodeParameterDeclarationExpression() { Name = parameterType.ParameterCodeName };
                if (parameter.ParameterDbType == SqlDbType.Structured) // IEnumerable<udtRow>
                {
                    if (!udt_rows.TryGetValue(parameter.ParameterTypeFullName, out TableTypeRow udtRow))
                    {
                        throw new StoreLakeSdkException("Table type could not be found:" + parameter.ParameterTypeFullName + " Procedure:" + procedure.ProcedureName + ", dacpac:" + dacpac.DacPacAssemblyLogicalName);
                    }
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

            if (procedureOutputResultSets.Length > 0)
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
