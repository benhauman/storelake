using Microsoft.SqlServer.TransactSql.ScriptDom;
using StoreLake.Sdk.SqlDom;
using System;

namespace StoreLake.Test
{
    internal static class TestResources
    {
        internal static string LoadProcedureBody(string procedureFileName)
        {
            var ddl = ResourceHelper.GetSql("SQL.Procedures." + procedureFileName);

            string begin = "AS" + "\r\n";
            var idx = ddl.IndexOf(begin);
            if (idx < 0)
            {
                throw new NotImplementedException();
            }
            string body = ddl.Substring(idx + begin.Length);
            return body;
        }

        internal static TestSource LoadTable(string tableFileName)
        {
            var ddl = ResourceHelper.GetSql("SQL.Tables." + tableFileName);
            return LoadTableFromDDL(ddl);
        }
        private static TestSource LoadTableFromDDL(string ddl)
        { 
            TSqlFragment sqlF = ScriptDomFacade.Parse(ddl);
            CreateTableStatement stmt_CreateTable = (CreateTableStatement)((TSqlScript)sqlF).Batches[0].Statements[0];

            string schemaName = stmt_CreateTable.SchemaObjectName.SchemaIdentifier.Dequote();
            string tableName = stmt_CreateTable.SchemaObjectName.BaseIdentifier.Dequote();
            var table = new TestSource(schemaName, tableName);
            foreach (ColumnDefinition col in stmt_CreateTable.Definition.ColumnDefinitions)
            {
                string columnName = col.ColumnIdentifier.Dequote();
                var columnDbType = ProcedureGenerator.ResolveToDbDataType(col.DataType);

                table.AddColumn(columnName, columnDbType);
            }

            return table;
        }

        internal static TestSchema LoadTables(this TestSchema schema)
        {
            var tableFileNames = ResourceHelper.CollectResourceNamesByPrefix(typeof(ResourceHelper), "SQL.Tables.");
            foreach(var resourceName in tableFileNames)
            {
                string ddl = ResourceHelper.LoadResourceText(typeof(ResourceHelper), resourceName);
                schema.AddSource(LoadTableFromDDL(ddl));
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

        private static TestFunction LoadFunctionMetatadaFromDDL(TestSchema schema, string ddl)
        {
            TSqlFragment sqlF = ScriptDomFacade.Parse(ddl);
            CreateFunctionStatement stmt_CreateFunction = (CreateFunctionStatement)((TSqlScript)sqlF).Batches[0].Statements[0];

            string schemaName = stmt_CreateFunction.Name.SchemaIdentifier.Dequote();
            string functionName = stmt_CreateFunction.Name.BaseIdentifier.Dequote();
            var function = new TestFunction(schemaName, functionName, f=>LoadFunctionOutputColumns(schema, f, stmt_CreateFunction));
            foreach (ProcedureParameter prm in stmt_CreateFunction.Parameters)
            {
                string parameterName = prm.VariableName.Dequote();
                var parameterDbType = ProcedureGenerator.ResolveToDbDataType(prm.DataType);

                function.AddParameter(parameterName, parameterDbType);
            }

            return function;

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
            BatchOutputColumnTypeResolver batchResolver = new BatchOutputColumnTypeResolver(schema, stmt_CreateFunction);
            if (stmt_CreateFunction.ReturnType is SelectFunctionReturnType fn_sel)
            {
                StatementOutputColumnTypeResolverV2 resolver = new StatementOutputColumnTypeResolverV2(batchResolver, fn_sel.SelectStatement);

                QuerySpecification first = TopQuerySpecification(fn_sel.SelectStatement.QueryExpression);
                foreach (SelectElement se in first.SelectElements)
                {
                    if (se is SelectScalarExpression scalarExpr)
                    {
                        var col = resolver.ResolveScalarExpression(scalarExpr);
                        function_source.AddColumn(col.OutputColumnName, col.ColumnDbType);
                    }
                    else
                    {
                        throw new NotImplementedException(se.WhatIsThis());
                    }
                }
            }
            else
            {
                //stmt_CreateFunction.ReturnType.Accept(new DumpFragmentVisitor(true));
                throw new NotImplementedException();
            }
        }
    }
}