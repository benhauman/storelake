using System;
using System.Data.Common;


namespace StoreLake.TestStore
{
    internal class StoreLakeDbTransaction : DbTransaction
    {

        public override void Commit()
        {
            //throw new NotImplementedException();
        }

        public DbConnection DbConnection_Property { get; set; }
        protected override DbConnection DbConnection
        {
            get { return DbConnection_Property; }
        }

        public System.Data.IsolationLevel IsolationLevel_Property { get; set; }
        public override System.Data.IsolationLevel IsolationLevel
        {
            get { return IsolationLevel_Property; }
        }

        internal Action Rollback_Override { get; set; }
        public override void Rollback()
        {
            if (Rollback_Override != null)
            {
                Rollback_Override();
            }
            else
            {
                // throw new NotImplementedException("Rollback not expected. Check for missing commit or exception.");
            }
        }
    }
}