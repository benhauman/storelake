using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Diagnostics;

[assembly: DebuggerDisplay(@"\{Bzz = {BaseIdentifier.Value}}", Target = typeof(SchemaObjectName))]
[assembly: DebuggerDisplay(@"\{Bzz = {StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(SchemaObject)}}", Target = typeof(NamedTableReference))]
[assembly: DebuggerDisplay(@"{StoreLake.Sdk.SqlDom.SqlDomExtensions.WhatIsThis(this)}", Target = typeof(TSqlFragment))]
// see [DebuggerTypeProxy(typeof(HashtableDebugView))]
namespace StoreLake.Sdk.SqlDom
{
    internal interface IQueryModel
    {
        bool TryGetQueryOutputColumnByName(BatchOutputColumnTypeResolver batchResolver, string outputColumnName, out QueryColumnBase outputColumn);
        bool TryGetQueryOutputColumnAt(BatchOutputColumnTypeResolver batchResolver, int outputColumnIndex, out QueryColumnBase outputColumn);
        bool TryGetQuerySingleOutputColumn(BatchOutputColumnTypeResolver batchResolver, out QueryColumnBase outputColumn);
    }
}