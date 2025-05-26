namespace StoreLake.Sdk.CodeGeneration
{
    using System.Collections.Generic;
    using System.Diagnostics;

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