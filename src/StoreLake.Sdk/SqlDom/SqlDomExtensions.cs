using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace StoreLake.Sdk.SqlDom
{
    internal static class SqlDomExtensions
    {
        public static string AsText(this TSqlFragment fragment)
        {
            System.Text.StringBuilder buffer = new System.Text.StringBuilder();
            for (int ix = fragment.FirstTokenIndex; ix <= fragment.LastTokenIndex; ix++)
            {
                buffer.Append(fragment.ScriptTokenStream[ix].Text);
            }

            var text = buffer.ToString();
            return text;
        }
    }
}