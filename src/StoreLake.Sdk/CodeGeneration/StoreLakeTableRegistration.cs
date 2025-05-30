﻿namespace StoreLake.Sdk.CodeGeneration
{
    using System.Collections.Generic;

    internal sealed class StoreLakeTableRegistration
    {
        public string TableSchema { get; set; }
        public string TableName { get; set; }

        public List<StoreLakeColumnRegistration> Columns { get; set; } = new List<StoreLakeColumnRegistration>();
        //public StoreLakeKeyRegistration PrimaryKey { get; set; }
    }
}
