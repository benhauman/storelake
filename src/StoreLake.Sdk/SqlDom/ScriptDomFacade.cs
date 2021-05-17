using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StoreLake.Sdk.SqlDom
{
    public static class ScriptDomFacade
    {
        public static TSqlFragment Parse(string script)
        {
            return Load(new StringReader(script));
        }
        internal static TSqlFragment Load(TextReader reader)
        {
            TSqlParser parser = new TSql140Parser(true);
            using (reader)
            {
                TSqlFragment fragment = parser.Parse(reader, out IList<ParseError> errors);
                if (errors.Count > 0)
                    throw new InvalidOperationException($@"Error parsing SQL statement
{String.Join(Environment.NewLine, errors.Select(x => $"{x.Message} at {x.Line},{x.Column}"))}");

                return fragment;
            }
        }

        internal static string GenerateScript(TSqlFragment fragment) => GenerateScript(fragment, null);
        private static string GenerateScript(TSqlFragment fragment, Action<SqlScriptGenerator> configuration)
        {
            SqlScriptGenerator generator = new Sql140ScriptGenerator();
            configuration?.Invoke(generator);
            generator.GenerateScript(fragment, out string output);
            return output;
        }
    }
}