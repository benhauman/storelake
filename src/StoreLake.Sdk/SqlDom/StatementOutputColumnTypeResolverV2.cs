using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;

namespace StoreLake.Sdk.SqlDom
{
    internal class StatementOutputColumnTypeResolverV2
    {
        internal readonly ISchemaMetadataProvider SchemaMetadata;
        BatchOutputColumnTypeResolver batchResolver;
        StatementWithCtesAndXmlNamespaces statement;
        private StatementModel _model;
        public StatementOutputColumnTypeResolverV2(BatchOutputColumnTypeResolver batchResolver, StatementWithCtesAndXmlNamespaces statement)
        {
            SchemaMetadata = batchResolver.SchemaMetadata;
            this.batchResolver = batchResolver;
            this.statement = statement;
        }

        internal OutputColumnDescriptor ResolveColumnReference(ColumnReferenceExpression node)
        {
            StatementModel model = EnsureModel();
            throw new NotImplementedException();
        }

        internal OutputColumnDescriptor ResolveScalarExpression(SelectScalarExpression node)
        {
            StatementModel model = EnsureModel();
            throw new NotImplementedException();
        }

        private StatementModel EnsureModel()
        {
            if (_model == null)
            {
                _model = LoadModel();
            }
            return _model;
        }

        private StatementModel LoadModel()
        {
            StatementModel model = new StatementModel();
            return model;
        }
    }

    internal sealed class StatementModel
    {
        // root: ColumnSource query = 
        // ColumnSource types(table-view, subquery, udtf:tablefunctions, tablevariablaes, parameters) (Alias, Named, ?)
    }

}