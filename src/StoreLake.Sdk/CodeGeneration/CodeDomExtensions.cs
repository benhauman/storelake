namespace StoreLake.Sdk.CodeGeneration
{
    using System;
    using System.CodeDom;
    using System.Text;

    internal static class CodeDomExtensions
    {

        internal static bool IsStatic(this MemberAttributes attributes)
        {
            return (attributes & MemberAttributes.Static) == MemberAttributes.Static;
        }

        internal static bool IsPrivate(this MemberAttributes attributes)
        {
            return (attributes & MemberAttributes.Private) == MemberAttributes.Private;
        }

        internal static bool IsPublic(this MemberAttributes attributes)
        {
            return (attributes & MemberAttributes.Public) == MemberAttributes.Public;
        }

        internal static bool IsOverride(this MemberAttributes attributes)
        {
            return (attributes & MemberAttributes.Override) == MemberAttributes.Override;
        }

        private static string AsTraceText(MemberAttributes attributes)
        {
            StringBuilder text = new StringBuilder();
            text.Append("" + attributes + " : ");
            if ((attributes & MemberAttributes.Public) == MemberAttributes.Public)
            {
                text.Append(" Public ");
            }
            if ((attributes & MemberAttributes.Private) == MemberAttributes.Private)
            {
                text.Append(" Private ");
            }

            if ((attributes & MemberAttributes.Static) == MemberAttributes.Static)
            {
                text.Append(" Static ");
            }

            if ((attributes & MemberAttributes.Override) == MemberAttributes.Override)
            {
                text.Append(" Override ");
            }

            if ((attributes & MemberAttributes.Family) == MemberAttributes.Family)
            {
                text.Append(" Protected ");
            }

            if ((attributes & MemberAttributes.Final) == MemberAttributes.Final)
            {
                text.Append(" sealed ");
            }

            return text.ToString();
        }

        internal static int CountOfType(this CodeCompileUnit ccu)
        {
            if (ccu.Namespaces.Count == 1)
                return ccu.Namespaces[0].Types.Count;

            int count = 0;
            for (int ix = 0; ix < ccu.Namespaces.Count; ix++)
            {
                count += ccu.Namespaces[ix].Types.Count;
            }

            return count;
        }

        internal static CodeParameterDeclarationExpression GetMethodParameterByName(CodeMemberMethod method_decl, string name)
        {
            for (int ix = 0; ix < method_decl.Parameters.Count; ix++)
            {
                CodeParameterDeclarationExpression prm = method_decl.Parameters[ix];
                if (prm.Name == name)
                {
                    return prm;
                }
            }
            throw new NotSupportedException("Parrameter '" + name + "' could not be found on method:" + method_decl.Name);
        }
    }
}
