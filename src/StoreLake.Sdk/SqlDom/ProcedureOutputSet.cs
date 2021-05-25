using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StoreLake.Sdk.SqlDom
{
    public sealed class ProcedureOutputSet
    {
        private readonly TSqlFragment initiator;
        internal ProcedureOutputSet(StatementWithCtesAndXmlNamespaces initiator)
        {
            this.initiator = initiator;
        }
        private readonly List<ProcedureOutputColumn> resultFragments = new List<ProcedureOutputColumn>();

        public int ColumnCount => resultFragments.Count;
        public ProcedureOutputColumn ColumnAt(int indexZeroBased)
        {
            return resultFragments.ElementAt(indexZeroBased);
        }

        internal void AddColumn(ProcedureOutputColumn column)
        {
            resultFragments.Add(column);
        }

        internal void Clear()
        {
            resultFragments.Clear();
        }

        internal bool HasMissingColumnInfo()
        {
            return resultFragments.Any(x => x.HasMissingInformation);
        }

        internal void ApplyMissingInformation(ProcedureOutputSet resultOutput)
        {
            if (resultFragments.Count != resultOutput.resultFragments.Count)
            {
                throw new NotSupportedException("Different column count. Expected:" + resultFragments.Count + ", Actual:" + resultOutput.resultFragments.Count);
            }
            for (int ix = 0; ix < resultFragments.Count; ix++)
            {
                if (resultFragments[ix].HasMissingInformation && !resultOutput.resultFragments[ix].HasMissingInformation)
                {
                    resultFragments[ix].ApplyMissingInformation(resultOutput.resultFragments[ix]);
                }
            }
        }
    }
}
