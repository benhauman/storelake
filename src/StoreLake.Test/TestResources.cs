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
            //schema
            //        .AddSource(TestResources.LoadTable("hlsysassociationdef"))
            //        .AddSource(TestResources.LoadTable("hlsysactioncontext"))
            //        .AddSource(TestResources.LoadTable("hlsyscasehistory"))
            //        ;


            return schema;
        }
    }
}