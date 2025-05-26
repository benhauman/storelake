namespace StoreLake.Sdk.SqlDom
{
    using System.Data;

    public interface ISchemaMetadataProvider
    {
        IColumnSourceMetadata TryGetColumnSourceMetadata(string schemaName, string objectName);
        IColumnSourceMetadata TryGetFunctionTableMetadata(string schemaName, string objectName);
        IColumnSourceMetadata TryGetUserDefinedTableTypeMetadata(string schemaName, string objectName);
    }

    public interface IColumnSourceMetadata
    {
        ColumnTypeMetadata TryGetColumnTypeByName(string columnName);
    }

    public sealed class ColumnTypeMetadata
    {
        public readonly DbType ColumnDbType;
        public readonly bool AllowNull;
        public readonly string UserDefinedTableTypeSchema;
        public readonly string UserDefinedTableTypeName;
        public readonly bool IsUserDefinedTableType;

        public ColumnTypeMetadata(DbType columnDbType, bool allowNull)
        {
            ColumnDbType = columnDbType;
            AllowNull = allowNull;
            IsUserDefinedTableType = false;
        }
        public ColumnTypeMetadata(bool allowNull, string userDefinedTableTypeSchema, string userDefinedTableTypeName)
        {
            ColumnDbType = DbType.Object;
            AllowNull = allowNull;
            UserDefinedTableTypeSchema = userDefinedTableTypeSchema;
            UserDefinedTableTypeName = userDefinedTableTypeName;
            IsUserDefinedTableType = true;
        }
    }
}