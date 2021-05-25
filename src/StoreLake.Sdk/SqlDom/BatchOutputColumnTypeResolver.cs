using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;

namespace StoreLake.Sdk.SqlDom
{
    internal class BatchOutputColumnTypeResolver
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


            //return null;
        }

        internal DbType? ResolveScalarExpression(StatementWithCtesAndXmlNamespaces statement, SelectScalarExpression node)
        {
            //if (node.ColumnType != ColumnType.Regular)
            throw new NotImplementedException(node.AsText() + "   ## " + statement.AsText());
            //node.MultiPartIdentifier
            //return null;
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
                    cache_columns.Add(colDef.ColumnIdentifier.Value.ToUpperInvariant(), ResolveToDbDataType(colDef.DataType));
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

            internal static DbType ResolveToDbDataType(DataTypeReference dataType)
            {
                if (dataType.Name.Count == 1)
                {
                    string typeName = dataType.Name[0].Value;
                    if (string.Equals("INT", typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        //return typeof(int);
                        return DbType.Int32;
                    }

                    if (string.Equals("BIT", typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        //return typeof(bool);
                        return DbType.Boolean;
                    }

                    if (string.Equals("NVARCHAR", typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        SqlDataTypeReference sqlDataType = (SqlDataTypeReference)dataType;
                        string maxLen = sqlDataType.Parameters[0].Value;
                        //dataType.p
                        //return typeof(string);
                        return DbType.String;
                    }

                    if (string.Equals("SMALLINT", typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        //SqlDataTypeReference sqlDataType = (SqlDataTypeReference)dataType;
                        //string maxLen = sqlDataType.Parameters[0].Value;
                        //dataType.p
                        //return typeof(string);
                        return DbType.Int16;
                    }

                    throw new NotImplementedException("typeName:" + typeName);
                }
                else
                {
                    throw new NotImplementedException("Name.Count:" + dataType.Name.Count);
                }
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