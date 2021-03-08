using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Dibix.TestStore.Database
{
    [DebuggerDisplay("PK:{PK()}")]
    public abstract class StoreLakeDatabaseEntityBaseT<TDatabase>
        where TDatabase : StoreLakeDatabase
    {
        private TDatabase _db;
        private DataRow _row;
        private bool _isRowStateDetached;

        protected StoreLakeDatabaseEntityBaseT()
        {
        }

        internal void SetupRow(TDatabase db, DataRow row, bool isRowStateDetached)
        {
            _db = db;
            _row = row;
            _isRowStateDetached = isRowStateDetached;
        }

        protected TDatabase Database { get { return _db; } }

        protected internal DataRow ValidateRow()
        {
            DataTable table = _db.GetTableByName(_row.Table.TableName);
            if (_isRowStateDetached)
            {
                if (_row.RowState == DataRowState.Detached)
                {
                    return _row;
                }
                else
                {
                    throw new InvalidOperationException("Row state:" + _row.RowState);
                }
            }
            DataRowCollection rows = table.Rows;
            int rowIndex = rows.IndexOf(_row);
            if (rowIndex < 0)
            {
                throw new InvalidOperationException();
            }

            return rows[rowIndex];
        }

        internal void RowAttached()
        {
            if (_row.RowState == DataRowState.Detached)
            {
                throw new InvalidOperationException("Row state:" + _row.RowState);
            }
            _isRowStateDetached = false;
        }
    }
    [DebuggerDisplay("PK:{PK()}")]
    public abstract class StoreLakeDatabaseEntity<TKey, TDatabase> : StoreLakeDatabaseEntityBaseT<TDatabase>, IValidadatableDatabaseEntity<TDatabase>
    where TKey : IComparable
    where TDatabase : StoreLakeDatabase
    {
        protected StoreLakeDatabaseEntity()
        {
        }

        public virtual void ValidateDatabaseEntity(TDatabase db)
        {
            if (PK().CompareTo(default(TKey)) == 0)
            {
                throw new NotSupportedException("Invalid PK:" + PK());
            }
        }

        protected int GetValueInt32([CallerMemberName] string columnName = "")
        {
            DataRow row = ValidateRow();

            var value = row[columnName];
            DataColumn column = row.Table.Columns[columnName];
            if (column.DataType == typeof(int))
            {
                row[columnName] = value;
            }
            else
            {
                throw new InvalidOperationException("Wrong value type (" + typeof(int).Name + ") for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }
            if (row.IsNull(column))
            {
                throw new InvalidOperationException("NULL value for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }

            return (int)value;
        }

        protected short GetValueInt16([CallerMemberName] string columnName = "")
        {
            DataRow row = ValidateRow();

            var value = row[columnName];
            DataColumn column = row.Table.Columns[columnName];
            if (column.DataType == typeof(short))
            {
                row[columnName] = value;
            }
            else
            {
                throw new InvalidOperationException("Wrong value type (" + typeof(short).Name + ") for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }
            if (row.IsNull(column))
            {
                throw new InvalidOperationException("NULL value for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }


            return (short)value;
        }


        protected string GetValueString([CallerMemberName] string columnName = "")
        {
            DataRow row = ValidateRow();

            var value = row[columnName];
            DataColumn column = row.Table.Columns[columnName];
            if (column.DataType == typeof(string))
            {
                row[columnName] = value;
            }
            else
            {
                throw new InvalidOperationException("Wrong value type (" + typeof(string).Name + ") for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }

            return (string)value;
        }

        protected bool GetValueBool([CallerMemberName] string columnName = "")
        {
            DataRow row = ValidateRow();

            var value = row[columnName];
            DataColumn column = row.Table.Columns[columnName];
            if (column.DataType == typeof(bool))
            {
                row[columnName] = value;
            }
            else
            {
                throw new InvalidOperationException("Wrong value type (" + typeof(bool).Name + ") for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }

            if (row.IsNull(column))
            {
                throw new InvalidOperationException("NULL value for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }

            return (bool)value;
        }

        protected void SetValueInt32(int value, [CallerMemberName] string columnName = "")
        {
            DataRow row = ValidateRow();
            DataColumn column = row.Table.Columns[columnName];
            if (column.DataType == typeof(int))
            {
                row[columnName] = value;
            }
            else
            {
                throw new InvalidOperationException("Wrong value type (" + typeof(int).Name + ") for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }
        }
        protected void SetValueInt16(short value, [CallerMemberName] string columnName = "")
        {
            DataRow row = ValidateRow();
            DataColumn column = row.Table.Columns[columnName];
            if (column.DataType == typeof(short))
            {
                row[columnName] = value;
            }
            else
            {
                throw new InvalidOperationException("Wrong value type (" + typeof(short).Name + ") for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }
        }

        protected void SetValueString(string value, [CallerMemberName] string columnName = "")
        {
            DataRow row = ValidateRow();
            DataColumn column = row.Table.Columns[columnName];
            if (column.DataType == typeof(string))
            {
                row[columnName] = value;
            }
            else
            {
                throw new InvalidOperationException("Wrong value type (" + typeof(string).Name + ") for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }
        }

        protected void SetValueBool(bool value, [CallerMemberName] string columnName = "")
        {
            DataRow row = ValidateRow();
            DataColumn column = row.Table.Columns[columnName];
            if (column.DataType == typeof(bool))
            {
                row[columnName] = value;
            }
            else
            {
                throw new InvalidOperationException("Wrong value type (" + typeof(bool).Name + ") for table [" + row.Table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }
        }

        public abstract TKey PK();

        protected static void NotNull<TOwner, TProperty>(TOwner owner, System.Linq.Expressions.Expression<Func<TOwner, TProperty>> propertyExpression)
            where TOwner : StoreLakeDatabaseEntity<TKey, TDatabase>
            where TProperty : class
        {
            var memberSelector = (System.Linq.Expressions.MemberExpression)propertyExpression.Body;
            TProperty value = (TProperty)((PropertyInfo)memberSelector.Member).GetValue(owner);
            if (default(TProperty) == value)
            {
                string propertyName = memberSelector.Member.Name;// TypeHelper.PropertyNameOf<TOwner, TProperty>(propertyExpression);
                throw new InvalidOperationException("No value for '" + propertyName + "'");
            }
        }
    }
}
