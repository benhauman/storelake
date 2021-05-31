using System.Collections.Generic;

namespace StoreLake.Sdk.CodeGeneration
{
    internal class StoreLakeInlineTableValuedFunctionRegistration
    {
        public string FunctionBodyScript { get; set; }

        public string FunctionName { get; internal set; }
        public string FunctionSchema { get; internal set; }

        public List<StoreLakeParameterRegistration> Parameters = new List<StoreLakeParameterRegistration>();

    }
}