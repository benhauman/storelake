namespace StoreLake.TestStore.Server
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;

    public sealed class DynamicSqlTextCompare : IComparable
    {
        private readonly List<Func<DbCommand, bool>> impl = new List<Func<DbCommand, bool>>();
        private Action<DbCommand> OnFalseHandler = (cmd) => { }; // nothing

        public DynamicSqlTextCompare()
        {
        }
        //public DynamicSqlCompare(Func<DbCommand, bool> cmp)
        //{
        //    this.impl.Add(cmp);
        //}
        public DynamicSqlTextCompare And(Func<DbCommand, bool> cmp)
        {
            this.impl.Add(cmp);
            return this;
        }
        public DynamicSqlTextCompare OnFalse(Action<DbCommand> handler)
        {
            this.OnFalseHandler = handler;
            return this;
        }

        public int CompareTo(object obj)
        {
            var cmd = (DbCommand)obj;
            if (impl.Count == 0)
            {
                throw new NotSupportedException("empty compare-registry");
                //return -1; // empty compare-registry
            }
            //else
            {
                for (int ix = 0; ix < impl.Count; ix++)
                {
                    Func<DbCommand, bool> cmp = impl[ix];
                    if (!cmp(cmd))
                    {
                        OnFalseHandler(cmd);
                        cmp(cmd); // just for : debug it again
                        return 1;
                    }
                }
            }

            return 0;
        }
    }
}