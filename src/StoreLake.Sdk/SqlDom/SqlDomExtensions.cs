using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace StoreLake.Sdk.SqlDom
{
    public static class SqlDomExtensions
    {
        public static string AsText(this StatementList fragment)
        {
            System.Text.StringBuilder buffer = new System.Text.StringBuilder();
            for (int ix = 0; ix < fragment.Statements.Count; ix++)
            {
                buffer.AppendLine(fragment.Statements[ix].AsText());
            }

            var text = buffer.ToString();
            return text;
        }
        public static string AsText(this TSqlFragment fragment)
        {
            if (fragment.FirstTokenIndex == -1 || fragment.LastTokenIndex == -1)
            {
                if (fragment is StatementList stmts)
                    return AsText(stmts);
                return ""; // ? StatementList
            }

            System.Text.StringBuilder buffer = new System.Text.StringBuilder();
            for (int ix = fragment.FirstTokenIndex; ix <= fragment.LastTokenIndex; ix++)
            {
                buffer.Append(fragment.ScriptTokenStream[ix].Text);
            }

            var text = buffer.ToString();
            return text;
        }

        internal static string Dequote(this Identifier id)
        {
            if (id.QuoteType == QuoteType.NotQuoted)
                return id.Value;
            return Identifier.DecodeIdentifier(id.Value, out QuoteType _);
        }

        internal static string WhatIsThis(this TSqlFragment fragment)
        {
            return fragment.GetType().Name + " # " + fragment.AsText();
        }
    }
}