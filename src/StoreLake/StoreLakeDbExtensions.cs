using System.Data.Common;


namespace Dibix.TestStore
{
    internal static class StoreLakeDbExtensions
    {
        public static DbParameter TryGetParameter(this DbCommand command, string parameterName)
        {
            foreach (DbParameter x in command.Parameters)
            {
                DbParameter prm = (x.ParameterName == parameterName) || (x.ParameterName == ("@" + parameterName)) ? x : null;
                if (prm != null)
                    return prm;
            }
            return null;
        }

    }
}