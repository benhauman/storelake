using System.Diagnostics;

namespace StoreLake.Sdk.CodeGeneration
{
    [DebuggerDisplay("[{ParameterName}] {ParameterTypeName}")]
    public sealed class StoreLakeParameterRegistration
    {
        public string ParameterName { get; set; }
        public string ParameterTypeName { get; set; }
    }
}