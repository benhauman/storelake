using System.Collections.Generic;
using System.Diagnostics;

namespace StoreLake.Sdk.CodeGeneration
{
    [DebuggerDisplay("{TableTypeSchema}.{TableTypeName}")]
    internal class StoreLakeTableTypeRegistration
    {
        public string TableTypeSchema { get; set; }
        public string TableTypeName { get; set; }

        public List<StoreLakeColumnRegistration> Columns { get; set; } = new List<StoreLakeColumnRegistration>();

    }
}