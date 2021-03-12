﻿using System.Data;

namespace StoreLake.Sdk.Database
{
    internal sealed class StoreLakeColumnRegistration
    {
        public string ColumnName { get; set; }
        public bool IsNullable { get; set; }
        public bool IsIdentity { get; set; }
        public SqlDbType ColumnDbType { get; set; }
    }

}
