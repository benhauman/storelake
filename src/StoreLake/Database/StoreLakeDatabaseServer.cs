using System.Data;

namespace StoreLake.TestStore.Database
{
    public static class StoreLakeDatabaseServer
    {
        public static StoreLakeDatabaseBuilder<DataSet> CreateDatabase(string databaseName)
        {
            return CreateDatabase<DataSet>(databaseName);
        }
        public static StoreLakeDatabaseBuilder<TDataSet> CreateDatabase<TDataSet>(string databaseName) where TDataSet : DataSet, new()
        {
            return new StoreLakeDatabaseBuilder<TDataSet>(databaseName);
        }
    }
}
