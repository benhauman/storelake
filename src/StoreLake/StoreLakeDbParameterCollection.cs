using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace Dibix.TestStore
{
    internal sealed class StoreLakeDbParameterCollection : DbParameterCollection
    {
        public List<StoreLakeDbParameter> items = new List<StoreLakeDbParameter>();
        public override int Add(object value)
        {
            var item = (StoreLakeDbParameter)value;
            items.Add(item);
            return items.IndexOf(item);
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                items.Add(((StoreLakeDbParameter)value));
        }

        public override void Clear()
        {
            throw new NotImplementedException();
        }

        public override bool Contains(string value)
        {
            return this.items.Any(x => x.ParameterName == value);
        }

        public override bool Contains(object value)
        {
            throw new NotImplementedException();
        }

        public override void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public override int Count
        {
            get { return this.items.Count; }
        }

        public override System.Collections.IEnumerator GetEnumerator()
        {
            return this.items.GetEnumerator();
        }

        protected override DbParameter GetParameter(string parameterName)
        {
            DbParameter prm = this.items.FirstOrDefault(x => x.ParameterNameProperty == parameterName);
            if (prm == null)
            {
                prm = this.items.FirstOrDefault(x => x.ParameterNameProperty == ("@" + parameterName));
                if (prm == null)
                {
                    prm = this.items.FirstOrDefault(x => ("@" + x.ParameterNameProperty) == parameterName);
                }
            }
            if (prm == null)
                throw new InvalidOperationException("Parameter not found:" + parameterName);
            return prm;
        }

        protected override DbParameter GetParameter(int index)
        {
            return this.items[index]; // zero based
        }

        public override int IndexOf(string parameterName)
        {
            throw new NotImplementedException();
        }

        public override int IndexOf(object value)
        {
            throw new NotImplementedException();
        }

        public override void Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        public override bool IsFixedSize
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsReadOnly
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsSynchronized
        {
            get { throw new NotImplementedException(); }
        }

        public override void Remove(object value)
        {
            throw new NotImplementedException();
        }

        public override void RemoveAt(string parameterName)
        {
            throw new NotImplementedException();
        }

        public override void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            throw new NotImplementedException();
        }

        protected override void SetParameter(int index, DbParameter value)
        {
            throw new NotImplementedException();
        }

        public override object SyncRoot
        {
            get { throw new NotImplementedException(); }
        }
    }
}