using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;

namespace StoreLake.Sdk.SqlDom
{
    public sealed class BatchOutputColumnTypeResolver
    {
        internal readonly ISchemaMetadataProvider SchemaMetadata;
        private readonly TSqlFragment BodyFragment;
        public BatchOutputColumnTypeResolver(ISchemaMetadataProvider schemaMetadata, TSqlFragment bodyFragment)
        {
            SchemaMetadata = schemaMetadata;
            BodyFragment = bodyFragment;
        }
        internal DbType? ResolveColumnReference(StatementWithCtesAndXmlNamespaces statement, ColumnReferenceExpression node)
        {
            if (node.ColumnType != ColumnType.Regular)
                throw new NotImplementedException(node.AsText());

            if (node.MultiPartIdentifier.Count != 1)
            {
                throw new NotImplementedException(node.AsText());
                //return null;
            }
            else
            {
                // no source only column name => traverse all source and find t
                throw new NotImplementedException(node.AsText() + "   ## " + statement.AsText());
            }
        }

        internal IColumnSourceMetadata TryGetTableVariable(string variableName)
        {
            if (cache_table_variables.TryGetValue(variableName.ToUpperInvariant(), out TableVariableMetadata source_metadata))
            {
                return source_metadata;
            }
            TableVariableDeclarionVisitor vstor = new TableVariableDeclarionVisitor(variableName);
            BodyFragment.Accept(vstor);
            if (vstor.variableDefinition != null)
            {
                source_metadata = new TableVariableMetadata(vstor.variableDefinition);
                cache_table_variables.Add(variableName.ToUpperInvariant(), source_metadata);
                return source_metadata;
            }
            throw new NotImplementedException(variableName);
        }

        private readonly IDictionary<string, TableVariableMetadata> cache_table_variables = new SortedDictionary<string, TableVariableMetadata>();

        class TableVariableMetadata : IColumnSourceMetadata
        {
            private readonly TableDefinition variableDefinition;

            private readonly IDictionary<string, DbType> cache_columns = new SortedDictionary<string, DbType>();

            public TableVariableMetadata(TableDefinition variableDefinition)
            {
                this.variableDefinition = variableDefinition;

                foreach (ColumnDefinition colDef in variableDefinition.ColumnDefinitions)
                {
                    cache_columns.Add(colDef.ColumnIdentifier.Value.ToUpperInvariant(), ProcedureGenerator.ResolveToDbDataType(colDef.DataType));
                }
            }

            DbType? IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
            {
                if (cache_columns.TryGetValue(columnName.ToUpperInvariant(), out DbType columnDbType))
                {
                    return columnDbType;
                }

                return null;
            }

        }

        class TableVariableDeclarionVisitor : DumpFragmentVisitor
        {
            private readonly string variableName;
            internal TableDefinition variableDefinition;
            public TableVariableDeclarionVisitor(string variableName) : base(false)
            {
                this.variableName = variableName;
            }

            public override void ExplicitVisit(DeclareTableVariableBody node)
            {
                if (string.Equals(node.VariableName.Value, variableName, StringComparison.OrdinalIgnoreCase))
                {
                    variableDefinition = node.Definition;
                }
            }
        }
    }
}