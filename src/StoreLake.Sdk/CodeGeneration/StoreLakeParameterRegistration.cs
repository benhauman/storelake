﻿using System.Data;
using System.Diagnostics;

namespace StoreLake.Sdk.CodeGeneration
{
    [DebuggerDisplay("[{ParameterName}] {ParameterTypeName}")]
    public sealed class StoreLakeParameterRegistration
    {
        public string ParameterName { get; set; }
        public string ParameterTypeFullName { get; set; }

        public SqlDbType ParameterDbType { get; set; }
        public bool AllowNull { get; set; }
        public string StructureTypeSchemaName { get; set; }
        public string StructureTypeName { get; set; }

        public string StructureTypeClassName { get; set; }
        public bool IsNULLSpecified { get; internal set; }
        //public string StructureTypeNamespaceName { get; set; }

        public bool IsOutput { get; set; }
    }
}