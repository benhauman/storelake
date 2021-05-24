namespace StoreLake.Sdk.SqlDom
{
    public interface ISchemaMetadataProvider
    {
        IColumnSourceMetadata TryGetColumnSourceMetadata(string schemaName, string objectName);
    }

    public interface IColumnSourceMetadata
    {
        System.Data.DbType? TryGetColumnTypeByName(string columnName);
    }
}