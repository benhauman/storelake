namespace StoreLake.TestStore
{
    using System;
    using System.Data.Common;
    using System.Diagnostics;

    [DebuggerDisplay("{ParameterName}:{DbType}")]
    internal sealed class StoreLakeDbParameter : DbParameter
    {
        public System.Data.DbType DbTypeProperty;
        public override System.Data.DbType DbType
        {
            get
            {
                return DbTypeProperty;
            }
            set
            {
                DbTypeProperty = value;
            }
        }

        public override System.Data.ParameterDirection Direction { get; set; }

        public override bool IsNullable
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public string ParameterNameProperty;
        public override string ParameterName
        {
            get
            {
                return ParameterNameProperty;
            }
            set
            {
                ParameterNameProperty = value;
            }
        }

        public override void ResetDbType()
        {
            DbTypeProperty = System.Data.DbType.AnsiString;
        }

        public override int Size { get; set; }

        public override string SourceColumn
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override bool SourceColumnNullMapping
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override System.Data.DataRowVersion SourceVersion
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public object ValueProperty;
        public override object Value
        {
            get
            {
                return ValueProperty;
            }
            set
            {
                ValueProperty = value;
            }
        }
    }
}