using System;
using System.Collections.Generic;
using System.Data;

namespace StoreLake.Sdk.CodeGeneration
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
                    throw new StoreLakeSdkException("NotImplemented:" + "Column type table [" + treg.TableName + "] column [" + creg.ColumnName + "] type (" + creg.ColumnDbType + ")");
                }

                DataColumn column = table.Columns.Add(creg.ColumnName, columnType);
                column.AllowDBNull = creg.IsNullable;
                column.AutoIncrement = creg.IsIdentity;

            }
        }

        internal static void RegisterForeignKey(DataSet ds, StoreLakeForeignKeyRegistration foreignKey)
        {
            //throw new NotImplementedException("xForeignKeyName:" + xForeignKeyName.Value + " ON " + fk_reg.DefiningTableName + "() REFERENCES " + fk_reg.ForeignTableName);
            DataTable definining_table = ds.Tables[foreignKey.DefiningTableName, foreignKey.DefiningTableSchema];
            DataTable foreign_table = ds.Tables[foreignKey.ForeignTableName, foreignKey.ForeignTableSchema];
            if (definining_table == null)
            {
                throw new StoreLakeSdkException("Foreign key 'Defining' table not found. Table [" + foreignKey.DefiningTableSchema + "] Key [" + foreignKey.ForeignKeyName + "]");
            }
            if (foreign_table == null)
            {
                throw new StoreLakeSdkException("Foreign key 'Foreign' table not found. Table [" + foreignKey.ForeignTableName + "] Key [" + foreignKey.ForeignKeyName + "]");
            }
            if (foreignKey.DefiningColumns == null || foreignKey.DefiningColumns.Count == 0)
            {
                throw new StoreLakeSdkException("Foreign key 'Defining' columns not specified. Table [" + foreignKey.DefiningTableSchema + "] Key [" + foreignKey.ForeignKeyName + "]");
            }
            if (foreignKey.ForeignColumns == null || foreignKey.ForeignColumns.Count == 0)
            {
                throw new StoreLakeSdkException("Foreign key 'Foreign' columns not specified. Table [" + foreignKey.ForeignTableName + "] Key [" + foreignKey.ForeignKeyName + "]");
            }

            if (foreignKey.DefiningColumns.Count != foreignKey.ForeignColumns.Count)
            {
                throw new StoreLakeSdkException("Foreign key mismatched columns count. Table [" + foreignKey.ForeignTableName + "] Key [" + foreignKey.ForeignKeyName + "]");
            }
            List<DataColumn> defining_columns = new List<DataColumn>();
            List<DataColumn> foreign_columns = new List<DataColumn>();
            for (int ix = 0; ix < foreignKey.DefiningColumns.Count; ix++)
            {
                string defining_column_name = foreignKey.DefiningColumns[ix].ColumnName;
                string foreign_column_name = foreignKey.ForeignColumns[ix].ColumnName;
                DataColumn defining_column = definining_table.Columns[defining_column_name];
                DataColumn foreign_column = foreign_table.Columns[foreign_column_name];

                defining_columns.Add(defining_column);
                foreign_columns.Add(foreign_column);
            }


            ForeignKeyConstraint fk = new ForeignKeyConstraint(foreignKey.ForeignKeyName, foreign_columns.ToArray(), defining_columns.ToArray());
            ForeignKeyConstraint found = FindForeignKeyConstraint(definining_table.Constraints, fk);
            
            if (found == null)
            {
                //Console.WriteLine("[" + foreignKey.ForeignKeyName + "] ON [" + foreignKey.DefiningTableName + "]" + " REFERENCES [" + foreignKey.ForeignTableName + "]");

                definining_table.Constraints.Add(fk);
            }
            //definining_table.ParentRelations.Add(new DataRelation() { });
        }
        // System.Data.ConstraintCollection
        internal static ForeignKeyConstraint FindForeignKeyConstraint(ConstraintCollection lst, ForeignKeyConstraint fk)
        {
            int count = lst.Count;
            for (int i = 0; i < count; i++)
            {
                // name?
                if (string.Equals(((Constraint)lst[i]).ConstraintName, fk.ConstraintName))
                {
                    return (ForeignKeyConstraint)lst[i];
                }
                // columns?
                if (((Constraint)lst[i]).Equals(fk))
                {
                    return (ForeignKeyConstraint)lst[i];
                }
            }
            return null;
        }
        internal static void RegisterDefaultConstraint(DataSet ds, StoreLakeDefaultContraintRegistration defaultContraint)
        {
            var table = ds.Tables[defaultContraint.TableName, defaultContraint.TableSchema];
            var column = table.Columns[defaultContraint.ColumnName];
            if (column == null)
            {
                throw new StoreLakeSdkException("Column not found. Table [" + table.TableName + "] column [" + defaultContraint.ColumnName + "]");
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
                    else if (column.DataType == typeof(byte[]) && defaultContraint.ValueBytes != null)
                    {
                        //column.DefaultValue = defaultContraint.ValueBytes;
                    }
                    else
                    {
                        throw new StoreLakeSdkException("NotImplemented:" + "Column type table [" + table.TableName + "] column [" + column.ColumnName + "] type (" + column.DataType.Name + ")");
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
            }
        }

        internal static void RegisterPrimaryKey(DataSet ds, StoreLakeTableKeyRegistration primaryKey)
        {
            var table = ds.Tables[primaryKey.TableName, primaryKey.TableSchema];
            if (table == null)
            {
                throw new InvalidOperationException($"Table registration '{primaryKey.TableSchema}.{primaryKey.TableName}' for primary key '{primaryKey.KeyName}' could not be found. ");
            }

            List<DataColumn> pkColumns = new List<DataColumn>();
            foreach (StoreLakeKeyColumnRegistration pkcol in primaryKey.Columns)
            {
                var column = table.Columns[pkcol.ColumnName];
                if (column == null)
                {
                    throw new StoreLakeSdkException("Column not found. Table [" + table.TableName + "] column [" + pkcol.ColumnName + "]");
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
                    throw new StoreLakeSdkException("Column not found. Table [" + table.TableName + "] column [" + pkcol.ColumnName + "]");
                }
                pkColumns.Add(column);
            }

            table.Constraints.Add(new UniqueConstraint(uqKey.KeyName, pkColumns.ToArray(), false));
        }
    }

}
