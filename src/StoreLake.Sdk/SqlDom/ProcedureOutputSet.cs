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

        public static string PrepareOutputColumnName(ProcedureOutputSet procedureOutputResultSet, ProcedureOutputColumn outputColumn, IEnumerable<string> collectedOutputColumnNames, int ix)
        {
            string outputColumnName;
            if (string.IsNullOrEmpty(outputColumn.OutputColumnName))
            {
                //throw new NotImplementedException("Missing column name.");
                if (procedureOutputResultSet.ColumnCount == 1)
                {
                    outputColumnName = "value";
                }
                else
                {
                    outputColumnName = "value" + ix;
                }
            }
            else
            {
                if (outputColumn.OutputColumnName[0] == '@')
                    outputColumnName = outputColumn.OutputColumnName.Substring(1);
                else
                    outputColumnName = outputColumn.OutputColumnName;

                // make the column name unique
                int cnt = collectedOutputColumnNames.Count(x => string.Equals(x, outputColumnName, StringComparison.OrdinalIgnoreCase));
                if (cnt > 0)
                    outputColumnName = outputColumnName + (cnt + 1);
            }

            return outputColumnName;
        }

    }
}
