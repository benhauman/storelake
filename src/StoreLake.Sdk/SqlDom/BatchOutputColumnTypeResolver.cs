namespace StoreLake.Sdk.SqlDom
{
    using System;
    using System.Collections.Generic;
    using Microsoft.SqlServer.TransactSql.ScriptDom;

    public interface IBatchParameterMetadata
    {
        ColumnTypeMetadata TryGetParameterType(string parameterName);
    }
    public sealed class BatchOutputColumnTypeResolver
    {
        internal readonly ISchemaMetadataProvider SchemaMetadata;
        private readonly IBatchParameterMetadata parameters;
        private readonly TSqlFragment BodyFragment;
        public BatchOutputColumnTypeResolver(ISchemaMetadataProvider schemaMetadata, TSqlFragment bodyFragment, IBatchParameterMetadata parametersMetadata)
        {
            SchemaMetadata = schemaMetadata;
            BodyFragment = bodyFragment;
            parameters = parametersMetadata;
        }

        internal ColumnTypeMetadata TryGetScalarVariableType(string variableName)
        {
            var pt = parameters.TryGetParameterType(variableName);
            if (pt != null)
            {
                return new ColumnTypeMetadata(pt.ColumnDbType, pt.AllowNull);
            }

            // scan for variable declaration

            TableVariableDeclarionVisitor vstor = new TableVariableDeclarionVisitor(variableName);
            BodyFragment.Accept(vstor);
            if (vstor.variableDefinition != null)
            {
                throw new NotImplementedException(variableName);
            }

            if (vstor.VariableDataType != null)
            {
                var variableDbType = ProcedureGenerator.ResolveToDbDataType(vstor.VariableDataType);
                return new ColumnTypeMetadata(variableDbType, true);
            }
            //BodyFragment.Accept(new DumpFragmentVisitor(true));
            throw new NotImplementedException(variableName);
        }
        internal IColumnSourceMetadata TryGetTableVariable(string variableName)
        {
            if (cache_table_variables.TryGetValue(variableName, out TableVariableMetadata source_metadata))
            {
                return source_metadata;
            }
            TableVariableDeclarionVisitor vstor = new TableVariableDeclarionVisitor(variableName);
            BodyFragment.Accept(vstor);
            if (vstor.variableDefinition != null)
            {
                source_metadata = new TableVariableMetadata(vstor.variableDefinition);
                cache_table_variables.Add(variableName, source_metadata);
                return source_metadata;
            }

            if (vstor.VariableDataType != null)
            {
                IColumnSourceMetadata udt_metadata = SchemaMetadata.TryGetUserDefinedTableTypeMetadata(vstor.VariableDataType.Name.SchemaIdentifier.Dequote(), vstor.VariableDataType.Name.BaseIdentifier.Dequote());
                return udt_metadata;
            }
            var prm = parameters.TryGetParameterType(vstor.VariableName);
            if (prm != null && prm.IsUserDefinedTableType)
            {
                IColumnSourceMetadata udt = SchemaMetadata.TryGetUserDefinedTableTypeMetadata(prm.UserDefinedTableTypeSchema, prm.UserDefinedTableTypeName);
                if (udt != null)
                {
                    return udt;
                }
            }
            //BodyFragment.Accept(new DumpFragmentVisitor(true));
            throw new NotImplementedException(variableName);
        }

        private readonly IDictionary<string, TableVariableMetadata> cache_table_variables = new SortedDictionary<string, TableVariableMetadata>(StringComparer.OrdinalIgnoreCase);

        private class TableVariableMetadata : IColumnSourceMetadata
        {
            private readonly TableDefinition variableDefinition;

            private readonly IDictionary<string, ColumnTypeMetadata> cache_columns = new SortedDictionary<string, ColumnTypeMetadata>(StringComparer.OrdinalIgnoreCase);

            public TableVariableMetadata(TableDefinition variableDefinition)
            {
                this.variableDefinition = variableDefinition;

                foreach (ColumnDefinition colDef in variableDefinition.ColumnDefinitions)
                {
                    cache_columns.Add(colDef.ColumnIdentifier.Value, new ColumnTypeMetadata(ProcedureGenerator.ResolveToDbDataType(colDef.DataType), true));
                }
            }

            ColumnTypeMetadata IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
            {
                if (cache_columns.TryGetValue(columnName, out ColumnTypeMetadata columnDbType))
                {
                    return columnDbType;
                }

                return null;
            }
        }

        private class TableVariableDeclarionVisitor : DumpFragmentVisitor
        {
            internal readonly string VariableName;
            internal TableDefinition variableDefinition;
            internal DataTypeReference VariableDataType;
            public TableVariableDeclarionVisitor(string variableName) : base(false)
            {
                this.VariableName = variableName;
            }

            public override void ExplicitVisit(DeclareTableVariableBody node)
            {
                if (string.Equals(node.VariableName.Value, VariableName, StringComparison.OrdinalIgnoreCase))
                {
                    variableDefinition = node.Definition;
                }
            }

            public override void ExplicitVisit(DeclareTableVariableStatement node)
            {
                base.ExplicitVisit(node);
            }

            public override void ExplicitVisit(DeclareVariableElement node)
            {
                if (string.Equals(node.VariableName.Value, VariableName, StringComparison.OrdinalIgnoreCase))
                {
                    VariableDataType = node.DataType;
                    //variableDefinition = node.Definition;
                    //throw new NotImplementedException(node.WhatIsThis());
                }
                base.ExplicitVisit(node);
            }
        }
    }
}