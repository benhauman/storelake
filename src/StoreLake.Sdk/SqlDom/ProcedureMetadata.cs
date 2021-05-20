using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace StoreLake.Sdk.SqlDom
{
    public sealed class ProcedureMetadata
    {
        public ProcedureMetadata(string procedureName, TSqlFragment bodyFragment)
        {
            ProcedureName = procedureName;
            BodyFragment = bodyFragment;
        }

        public string ProcedureName { get; private set; }
        public TSqlFragment BodyFragment { get; private set; }
    }
}