using System;
using System.Collections.Generic;
using System.Data;

namespace Dibix.TestStore.Database
{
    internal static class DatabaseRegistration
    {

        internal static void RegisterTable(DataSet ds, StoreLakeTableRegistration treg)
        {
            System.Data.DataTable table = new System.Data.DataTable(treg.TableName, treg.TableSchema);
            ds.Tables.Add(table);

            foreach (var creg in treg.Columns)
            {
                Type columnType;
                if (creg.ColumnDbType == SqlDbType.Int)
                {
                    columnType = typeof(int);
                }
                else if (creg.ColumnDbType == SqlDbType.BigInt)
                {
                    columnType = typeof(long);
                }
                else if (creg.ColumnDbType == SqlDbType.NVarChar)
                {
                    columnType = typeof(string);
                }
                else if (creg.ColumnDbType == SqlDbType.NChar)
                {
                    columnType = typeof(string);
                }
                else if (creg.ColumnDbType == SqlDbType.SmallInt)
                {
                    columnType = typeof(short);
                }
                else if (creg.ColumnDbType == SqlDbType.TinyInt)
                {
                    columnType = typeof(byte);
                }
                else if (creg.ColumnDbType == SqlDbType.Bit)
                {
                    columnType = typeof(bool);
                }
                else if (creg.ColumnDbType == SqlDbType.Decimal)
                {
                    columnType = typeof(decimal);
                }
                else if (creg.ColumnDbType == SqlDbType.Float)
                {
                    columnType = typeof(float);
                }
                else if (creg.ColumnDbType == SqlDbType.DateTime)
                {
                    columnType = typeof(DateTime);
                }
                else if (creg.ColumnDbType == SqlDbType.Date)
                {
                    columnType = typeof(DateTime);
                }
                else if (creg.ColumnDbType == SqlDbType.Time)
                {
                    columnType = typeof(int);
                }
                else if (creg.ColumnDbType == SqlDbType.Xml)
                {
                    columnType = typeof(string);
                }
                else if (creg.ColumnDbType == SqlDbType.UniqueIdentifier)
                {
                    columnType = typeof(Guid);
                }
                else if (creg.ColumnDbType == SqlDbType.VarBinary)
                {
                    columnType = typeof(byte[]);
                }
                else if (creg.ColumnDbType == SqlDbType.Binary)
                {
                    columnType = typeof(byte[]);
                }
                else if (creg.ColumnDbType == SqlDbType.Image)
                {
                    columnType = typeof(byte[]);
                }
                else if (creg.ColumnDbType == SqlDbType.Timestamp) //[rowversion]
                {
                    columnType = typeof(byte[]);
                }
                else
                {
                    throw new NotImplementedException("Column type table [" + treg.TableName + "] column [" + creg.ColumnName + "] type (" + creg.ColumnDbType + ")");
                }

                DataColumn column = table.Columns.Add(creg.ColumnName, columnType);
                column.AllowDBNull = creg.IsNullable;
                column.AutoIncrement = creg.IsIdentity;

            }
        }

        internal static void RegisterDefaultConstraint(DataSet ds, StoreLakeDefaultContraintRegistration defaultContraint)
        {
            var table = ds.Tables[defaultContraint.TableName, defaultContraint.TableSchema];
            var column = table.Columns[defaultContraint.ColumnName];
            if (column == null)
            {
                throw new NotImplementedException("Column not found. Table [" + table.TableName + "] column [" + defaultContraint.ColumnName + "]");
            }
            if (defaultContraint.IsScalarValue)
            {
                if (column.DataType == typeof(int))
                {
                    column.DefaultValue = defaultContraint.ValueInt32.Value;
                }
                else if (column.DataType == typeof(decimal) && defaultContraint.ValueDecimal.HasValue)
                {
                    column.DefaultValue = defaultContraint.ValueDecimal.Value;
                }
                else
                {
                    if (column.DataType == typeof(bool) && defaultContraint.ValueInt32.HasValue && defaultContraint.ValueInt32.Value == 1)
                    {
                        column.DefaultValue = true;
                    }
                    else if (column.DataType == typeof(bool) && defaultContraint.ValueInt32.HasValue && defaultContraint.ValueInt32.Value == 0)
                    {
                        column.DefaultValue = false;
                    }
                    else if (column.DataType == typeof(short) && defaultContraint.ValueInt32.HasValue)
                    {
                        short defaultValueInt16 = (short)defaultContraint.ValueInt32.Value;
                        column.DefaultValue = defaultValueInt16;
                    }
                    else if (column.DataType == typeof(byte) && defaultContraint.ValueInt32.HasValue)
                    {
                        byte defaultValueInt8 = (byte)defaultContraint.ValueInt32.Value;
                        column.DefaultValue = defaultValueInt8;
                    }
                    else if (column.DataType == typeof(string) && defaultContraint.ValueString != null)
                    {
                        column.DefaultValue = defaultContraint.ValueString;
                    }
                    else if (column.DataType == typeof(DateTime) && defaultContraint.ValueDateTime.HasValue)
                    {
                        column.DefaultValue = defaultContraint.ValueDateTime.Value;
                    }
                    else
                    {
                        throw new NotImplementedException("Column type table [" + table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
                    }
                }
            }
            else
            {
                if (defaultContraint.IsBuiltInFunctionExpression && defaultContraint.DefaultExpressionScript == "GETUTCDATE")
                {
                    column.DefaultValue = DateTime.UtcNow; // Hmmm !?!?
                                                           //column.Expression = "new TimeProvider()";
                                                           //column.Expression = "Convert(new TimeProvider().Xx() + 0, 'System.String')";
                }
                else if (defaultContraint.IsBuiltInFunctionExpression && defaultContraint.DefaultExpressionScript == "HOST_NAME")
                {
                    column.DefaultValue = System.Environment.MachineName; // Hmmm !?!?
                }
                else if (defaultContraint.IsBuiltInFunctionExpression && defaultContraint.DefaultExpressionScript == "@@SPID")
                {
                    column.DefaultValue = System.Diagnostics.Process.GetCurrentProcess().Id; // Hmmm !?!?
                }
                else
                {
                    column.Expression = defaultContraint.DefaultExpressionScript;
                }
                //throw new NotImplementedException("Default expression for column table [" + table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
            }
        }

        internal static void RegisterPrimaryKey(DataSet ds, StoreLakeTableKeyRegistration primaryKey)
        {
            var table = ds.Tables[primaryKey.TableName, primaryKey.TableSchema];

            List<DataColumn> pkColumns = new List<DataColumn>();
            foreach (StoreLakeKeyColumnRegistration pkcol in primaryKey.Columns)
            {
                var column = table.Columns[pkcol.ColumnName];
                if (column == null)
                {
                    throw new NotImplementedException("Column not found. Table [" + table.TableName + "] column [" + pkcol.ColumnName + "]");
                }
                column.ReadOnly = true;
                pkColumns.Add(column);
            }

            table.Constraints.Add(new UniqueConstraint(primaryKey.KeyName, pkColumns.ToArray(), true));
            //table.PrimaryKey = pkColumns.ToArray();
        }

        internal static void RegisterUniqueKey(DataSet ds, StoreLakeTableKeyRegistration uqKey)
        {
            var table = ds.Tables[uqKey.TableName, uqKey.TableSchema];

            List<DataColumn> pkColumns = new List<DataColumn>();
            foreach (StoreLakeKeyColumnRegistration pkcol in uqKey.Columns)
            {
                var column = table.Columns[pkcol.ColumnName];
                if (column == null)
                {
                    throw new NotImplementedException("Column not found. Table [" + table.TableName + "] column [" + pkcol.ColumnName + "]");
                }
                pkColumns.Add(column);
            }

            table.Constraints.Add(new UniqueConstraint(uqKey.KeyName, pkColumns.ToArray(), false));
        }
    }
}
