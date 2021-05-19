using System.Collections.Generic;
using System.Diagnostics;

namespace StoreLake.Sdk.CodeGeneration
{
    [DebuggerDisplay("[{ProcedureName}]")]
    internal class StoreLakeProcedureRegistration
    {
        public string ProcedureSchemaName { get; set; }
        public string ProcedureName { get; set; }

        public string ProcedureBodyScript { get; set; }

        public List<StoreLakeParameterRegistration> Parameters = new List<StoreLakeParameterRegistration>();
        public List<StoreLakeAnnotationRegistration> Annotations = new List<StoreLakeAnnotationRegistration>();

    }
}