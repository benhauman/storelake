using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace StoreLake.TestStore
{
    internal sealed class StoreLakeDbDataReader : DbDataReader
    {
        public int currentRowIndex = -1;
        public List<IEnumerable<object>> Data_Rows;

        public StoreLakeDbDataReader()
        {
            this.Data_Rows = new List<IEnumerable<object>>();
        }

        public static StoreLakeDbDataReader CreateInstance(Action<StoreLakeDbDataReader> setup)
        {
            var instance = new StoreLakeDbDataReader();
            setup(instance);
            return instance;
        }

        public bool opened = true;
        public override void Close()
        {
            this.opened = false;
        }

        public override int Depth
        {
            get { throw new NotImplementedException(); }
        }

        public override int FieldCount
        {
            get { throw new NotImplementedException(); }
        }

        public override bool GetBoolean(int ordinal)
        {
            return (bool)GetValue(ordinal);
        }

        public override byte GetByte(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override char GetChar(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override string GetDataTypeName(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return Convert.ToDateTime(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
        }

        public override decimal GetDecimal(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override double GetDouble(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override System.Collections.IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public override Type GetFieldType(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override float GetFloat(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override Guid GetGuid(int ordinal)
        {
            return (Guid)GetValue(ordinal);
        }

        public override short GetInt16(int ordinal)
        {
            return (short)GetValue(ordinal);
        }

        public override int GetInt32(int ordinal)
        {
            return Convert.ToInt32(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
        }

        public override long GetInt64(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override string GetName(int ordinal)
        {
            throw new NotImplementedException();
        }

        public SortedDictionary<string, int> Data_Columns = new SortedDictionary<string, int>();
        public void TestAddColumn(string name, int ordinal)
        {
            this.Data_Columns.Add(name, ordinal);
        }
        public override int GetOrdinal(string name)
        {
            return Data_Columns[name];
        }

        public override System.Data.DataTable GetSchemaTable()
        {
            throw new NotImplementedException();
        }

        public override string GetString(int ordinal)
        {
            return Convert.ToString(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
        }

        public override object GetValue(int ordinal)
        {
            return CurrentDataRow.ElementAt(ordinal);
        }

        public override int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }

        public StoreLakeDbDataReader TestAddRow(params object[] rowValues)
        {
            if (rowValues.Length == 0)
                throw new InvalidOperationException("No values");

            if (Data_Rows == null)
                Data_Rows = new List<IEnumerable<object>>();
            Data_Rows.Add(rowValues);
            return this;
        }

        public override bool HasRows
        {
            get { return (Data_Rows != null && Data_Rows.Any()); }
        }

        public override bool IsClosed
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsDBNull(int ordinal)
        {
            return CurrentDataRow.ElementAt(ordinal) == DBNull.Value;
        }

        public override bool NextResult()
        {
            throw new NotImplementedException();
        }

        private IEnumerable<object> CurrentDataRow
        {
            get
            {
                if (currentRowIndex < 0)
                    throw new InvalidOperationException("Reader.Read not used!");
                return Data_Rows[currentRowIndex];
            }
        }

        public override bool Read()
        {
            int newIdx = currentRowIndex + 1;
            if (newIdx >= Data_Rows.Count)
                return false;

            currentRowIndex = newIdx;
            return true;
        }

        public override int RecordsAffected
        {
            get { throw new NotImplementedException(); }
        }

        public override object this[string name]
        {
            get
            {
                int ordinal = this.Data_Columns[name];
                return CurrentDataRow.ElementAt(ordinal);
            }
        }

        public override object this[int ordinal]
        {
            get
            {
                return CurrentDataRow.ElementAt(ordinal);
            }
        }
    }
}