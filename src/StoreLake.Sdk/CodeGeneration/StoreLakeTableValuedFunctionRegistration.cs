using System.Collections.Generic;

namespace StoreLake.Sdk.CodeGeneration
{
    internal class StoreLakeTableValuedFunctionRegistration
    {
        public bool IsInline { get; set; }
        public string FunctionBodyScript { get; set; }

        public string FunctionName { get; internal set; }
        public string FunctionSchema { get; internal set; }

        public List<StoreLakeParameterRegistration> Parameters = new List<StoreLakeParameterRegistration>();
        public List<StoreLakeColumnRegistration> Columns { get; set; } = new List<StoreLakeColumnRegistration>();

    }
}