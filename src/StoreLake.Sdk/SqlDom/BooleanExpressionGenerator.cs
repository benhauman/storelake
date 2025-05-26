namespace StoreLake.Sdk.SqlDom
{
    using System;
    using System.CodeDom;
    using System.Data;
    using Microsoft.SqlServer.TransactSql.ScriptDom;

    public static class BooleanExpressionGenerator
    {
        public static CodeExpression BuildFromCheckConstraintDefinition(string schemaName, DataTable table, string check_name, string check_definition, out bool hasError, out string errorText)
        {
            string select_statement = "SELECT colx = IIF((" + check_definition + "), 1, 0) FROM " + schemaName + ".[" + table.TableName + "] WHERE " + check_definition;
            TSqlFragment sqlF = ScriptDomFacade.Parse(select_statement);
            IIFVisitor iif_visitor = new IIFVisitor();
            sqlF.Accept(iif_visitor);
            if (iif_visitor.result == null)
            {
                throw new NotSupportedException("Expression 'IIF' could not be found:" + sqlF.AsText());
            }

            BooleanExpression iif_predicate = iif_visitor.result.Predicate;

            //if (iif_visitor != null)
            //{
            //    //return null; // comment out if it needed
            //}

            //Console.WriteLine("");
            //Console.WriteLine("==== " + check_name + " ============");
            //Console.WriteLine(ScriptDomFacade.GenerateScript(iif_predicate));
            //Console.WriteLine("-------------------------------------------");
            //Console.WriteLine("");

            var databaseMetadata = new DatabaseMetadataOnTable(table);
            hasError = false;
            errorText = null;
            return BooleanExpressionGeneratorVisitor.TryBuildFromFragment(databaseMetadata, iif_predicate, ref hasError, ref errorText);
        }
        private sealed class IIFVisitor : TSqlFragmentVisitor
        {
            internal IIfCall result;
            public override void ExplicitVisit(IIfCall node)
            {
                result = node;
            }
        }

        private class DatabaseMetadataOnTable : IDatabaseMetadataProvider
        {
            private readonly DataTable table;
            public DatabaseMetadataOnTable(DataTable table)
            {
                this.table = table;
            }
        }
    }
}
