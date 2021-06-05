using System.Data;

namespace StoreLake.Sdk.SqlDom
{
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
        public ColumnTypeMetadata(DbType columnDbType, bool allowNull)
        {
            ColumnDbType = columnDbType;
            AllowNull = allowNull;
        }
    }

}