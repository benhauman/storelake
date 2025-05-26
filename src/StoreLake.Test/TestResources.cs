namespace StoreLake.Test
{
    using System;
    using System.Text;
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using StoreLake.Sdk.SqlDom;

    internal static class TestResources
    {
        private static TestTable LoadTableFromDDL(string ddl)
        {
            TSqlFragment sqlF = ScriptDomFacade.Parse(ddl);
            CreateTableStatement stmt_CreateTable = (CreateTableStatement)((TSqlScript)sqlF).Batches[0].Statements[0];

            string schemaName = stmt_CreateTable.SchemaObjectName.SchemaIdentifier.Dequote();
            string tableName = stmt_CreateTable.SchemaObjectName.BaseIdentifier.Dequote();
            var table = new TestTable(schemaName, tableName);
            foreach (ColumnDefinition col in stmt_CreateTable.Definition.ColumnDefinitions)
            {
                string columnName = col.ColumnIdentifier.Dequote();
                var columnDbType = ProcedureGenerator.ResolveToDbDataType(col.DataType);

                table.AddColumn(columnName, columnDbType, true);
            }

            return table;
        }

        private static TestTable LoadUDTFromDDL(string ddl)
        {
            TSqlFragment sqlF = ScriptDomFacade.Parse(ddl);
            CreateTypeTableStatement stmt_CreateTable = (CreateTypeTableStatement)((TSqlScript)sqlF).Batches[0].Statements[0];

            string schemaName = stmt_CreateTable.Name.SchemaIdentifier.Dequote();
            string tableName = stmt_CreateTable.Name.BaseIdentifier.Dequote();
            var table = new TestTable(schemaName, tableName);
            foreach (ColumnDefinition col in stmt_CreateTable.Definition.ColumnDefinitions)
            {
                string columnName = col.ColumnIdentifier.Dequote();
                var columnDbType = ProcedureGenerator.ResolveToDbDataType(col.DataType);

                table.AddColumn(columnName, columnDbType, true);
            }

            return table;
        }
        internal static TestSchema LoadTables(this TestSchema schema)
        {
            var tableFileNames = ResourceHelper.CollectResourceNamesByPrefix(typeof(ResourceHelper), "SQL.Tables.");
            foreach (var resourceName in tableFileNames)
            {
                string ddl = ResourceHelper.LoadResourceText(typeof(ResourceHelper), resourceName);
                schema.AddTable(LoadTableFromDDL(ddl));
            }

            return schema;
        }

        internal static TestSchema LoadUDTs(this TestSchema schema)
        {
            var tableFileNames = ResourceHelper.CollectResourceNamesByPrefix(typeof(ResourceHelper), "SQL.Types.");
            foreach (var resourceName in tableFileNames)
            {
                string ddl = ResourceHelper.LoadResourceText(typeof(ResourceHelper), resourceName);
                schema.AddUDT(LoadUDTFromDDL(ddl));
            }

            return schema;
        }

        internal static TestSchema LoadViews(this TestSchema schema)
        {
            var viewFileNames = ResourceHelper.CollectResourceNamesByPrefix(typeof(ResourceHelper), "SQL.Views.");
            foreach (var resourceName in viewFileNames)
            {
                string ddl = ResourceHelper.LoadResourceText(typeof(ResourceHelper), resourceName);
                schema.AddView(LoadViewMetatadaFromDDL(schema, ddl));
            }

            return schema;
        }

        internal static TestSchema LoadFunctionsMetadata(this TestSchema schema)
        {
            var functionFileNames = ResourceHelper.CollectResourceNamesByPrefix(typeof(ResourceHelper), "SQL.Functions.");
            foreach (var resourceName in functionFileNames)
            {
                string ddl = ResourceHelper.LoadResourceText(typeof(ResourceHelper), resourceName);
                schema.AddFunction(LoadFunctionMetatadaFromDDL(schema, ddl));
            }

            return schema;
        }

        internal static TestFunction LoadFunctionMetatadaFromDDL(TestSchema schema, string ddl)
        {
            TSqlFragment sqlF = ScriptDomFacade.Parse(ddl);
            CreateFunctionStatement stmt_CreateFunction = (CreateFunctionStatement)((TSqlScript)sqlF).Batches[0].Statements[0];

            string schemaName = stmt_CreateFunction.Name.SchemaIdentifier.Dequote();
            string functionName = stmt_CreateFunction.Name.BaseIdentifier.Dequote();

            if (stmt_CreateFunction.StatementList != null)
            {
                // 'hlsyssec_query_agentsystemacl'
                return LoadMultiStatementTableValuedFunction(schemaName, functionName, stmt_CreateFunction);
            }

            // inline 'hlsur_query_surveyresults'
            string functionBody = GetFragmentStreamAsText(((SelectFunctionReturnType)stmt_CreateFunction.ReturnType).SelectStatement);

            var function = new TestFunction(schemaName, functionName, functionBody, f => LoadFunctionOutputColumns(schema, f, stmt_CreateFunction));
            foreach (ProcedureParameter prm in stmt_CreateFunction.Parameters)
            {
                string parameterName = prm.VariableName.Dequote();
                var parameterDbType = ProcedureGenerator.ResolveToDbDataType(prm.DataType);

                function.AddParameter(parameterName, parameterDbType, true);
            }

            return function;
        }

        private static TestFunction LoadMultiStatementTableValuedFunction(string schemaName, string functionName, CreateFunctionStatement stmt_CreateFunction)
        {
            if (stmt_CreateFunction.ReturnType is TableValuedFunctionReturnType tvfReturnType)
            {
                TestFunction function = new TestFunction(schemaName, functionName, "", (x) =>
                {
                });

                //tvfReturnType.DeclareTableVariableBody.VariableName
                var tableBody = tvfReturnType.DeclareTableVariableBody;

                foreach (ColumnDefinition col in tableBody.Definition.ColumnDefinitions)
                {
                    string columnName = col.ColumnIdentifier.Dequote();
                    var columnDbType = ProcedureGenerator.ResolveToDbDataType(col.DataType);

                    function.AddFunctionColumn(columnName, columnDbType, true);
                }

                return function;
            }
            throw new NotImplementedException(stmt_CreateFunction.ReturnType.WhatIsThis());
        }

        private static TestView LoadViewMetatadaFromDDL(TestSchema schema, string ddl)
        {
            TSqlFragment sqlF = ScriptDomFacade.Parse(ddl);
            CreateViewStatement stmt_CreateFunction = (CreateViewStatement)((TSqlScript)sqlF).Batches[0].Statements[0];
            //string body = ExtractViewDefinition(ddl);
            string body = GetFragmentStreamAsText(stmt_CreateFunction.SelectStatement);

            string schemaName = stmt_CreateFunction.SchemaObjectName.SchemaIdentifier.Dequote();
            string functionName = stmt_CreateFunction.SchemaObjectName.BaseIdentifier.Dequote();
            var function = new TestView(schemaName, functionName, body, f => LoadViewOutputColumnsX(schema, f, stmt_CreateFunction));

            return function;
        }

        private static void LoadViewOutputColumnsX(TestSchema schema, TestView vw, CreateViewStatement stmt_CreateFunction)
        {
            ProcedureGenerator.LoadViewOutputColumns(schema, vw.Body, (col) =>
            {
                vw.AddViewColumn(col.OutputColumnName, col.ColumnType.ColumnDbType, col.ColumnType.AllowNull, col.OutputColumnName);
            });
        }

        private static QuerySpecification TopQuerySpecification(QueryExpression expr)
        {
            if (expr is QuerySpecification qspec)
                return qspec;
            BinaryQueryExpression bqExpr = (BinaryQueryExpression)expr;
            return TopQuerySpecification(bqExpr.FirstQueryExpression);
        }

        private static void LoadFunctionOutputColumns(TestSchema schema, TestFunction function_source, CreateFunctionStatement stmt_CreateFunction)
        {
            ProcedureGenerator.LoadFunctionOutputColumns(schema, function_source, function_source.FunctionBodyScript, (col) =>
            {
                function_source.AddFunctionColumn(col.OutputColumnName, col.ColumnType.ColumnDbType, col.ColumnType.AllowNull);
            });
        }
        private static void LoadViewOutputColumns(TestSchema schema, TestView function_source, CreateViewStatement stmt_CreateView)
        {
            BatchOutputColumnTypeResolver batchResolver = new BatchOutputColumnTypeResolver(schema, stmt_CreateView, function_source);

            StatementOutputColumnTypeResolverV2 resolver = new StatementOutputColumnTypeResolverV2(batchResolver, stmt_CreateView.SelectStatement);

            QuerySpecification first = TopQuerySpecification(stmt_CreateView.SelectStatement.QueryExpression);
            foreach (SelectElement se in first.SelectElements)
            {
                if (se is SelectScalarExpression scalarExpr)
                {
                    OutputColumnDescriptor col = resolver.ResolveSelectScalarExpression(scalarExpr);
                    function_source.AddViewColumn(col.OutputColumnName, col.ColumnType.ColumnDbType, col.ColumnType.AllowNull, col.OutputColumnName);
                }
                else
                {
                    throw new NotImplementedException(se.WhatIsThis());
                }
            }
        }

        internal static string GetFragmentStreamAsText(TSqlFragment fragment)
        {
            StringBuilder text = new StringBuilder();
            for (var ix = fragment.FirstTokenIndex; ix <= fragment.LastTokenIndex; ix++)
            {
                text.Append(fragment.ScriptTokenStream[ix].Text);
            }
            return text.ToString();
        }
    }
}