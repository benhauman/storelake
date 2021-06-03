﻿using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;

namespace StoreLake.Sdk.SqlDom
{
    public interface IBatchParameterMetadata
    {
        System.Data.DbType? TryGetParameterType(string parameterName);
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

        internal bool TryGetScalarVariableType(string variableName, out DbType variableDbType)
        {
            DbType? parameterDbType = parameters.TryGetParameterType(variableName);
            if (parameterDbType.HasValue)
            {
                variableDbType = parameterDbType.Value;
                return true;
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
                variableDbType = ProcedureGenerator.ResolveToDbDataType(vstor.VariableDataType);
                return true;
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
            //BodyFragment.Accept(new DumpFragmentVisitor(true));
            throw new NotImplementedException(variableName);
        }

        private readonly IDictionary<string, TableVariableMetadata> cache_table_variables = new SortedDictionary<string, TableVariableMetadata>(StringComparer.OrdinalIgnoreCase);

        class TableVariableMetadata : IColumnSourceMetadata
        {
            private readonly TableDefinition variableDefinition;

            private readonly IDictionary<string, DbType> cache_columns = new SortedDictionary<string, DbType>(StringComparer.OrdinalIgnoreCase);

            public TableVariableMetadata(TableDefinition variableDefinition)
            {
                this.variableDefinition = variableDefinition;

                foreach (ColumnDefinition colDef in variableDefinition.ColumnDefinitions)
                {
                    cache_columns.Add(colDef.ColumnIdentifier.Value, ProcedureGenerator.ResolveToDbDataType(colDef.DataType));
                }
            }

            DbType? IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
            {
                if (cache_columns.TryGetValue(columnName, out DbType columnDbType))
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
            internal DataTypeReference VariableDataType;
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

            public override void ExplicitVisit(DeclareTableVariableStatement node)
            {
                base.ExplicitVisit(node);
            }

            public override void ExplicitVisit(DeclareVariableElement node)
            {
                if (string.Equals(node.VariableName.Value, variableName, StringComparison.OrdinalIgnoreCase))
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