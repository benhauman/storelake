namespace StoreLake.TestStore.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    public sealed class StoreLakeDatabaseBuilder<TDataSet>
        where TDataSet : DataSet, new()
    {
        private readonly string databaseName;
        private readonly List<Action<DataSet>> extensions = new List<Action<DataSet>>();
        private readonly SortedDictionary<string, Action<DataSet>> registered_extensions = new SortedDictionary<string, Action<DataSet>>();
        public StoreLakeDatabaseBuilder(string databaseName)
        {
            this.databaseName = databaseName;
        }

        public TDataSet Build()
        {
            TDataSet db = new TDataSet();
            db.DataSetName = databaseName;
            foreach (var extension in extensions)
            {
                extension(db);
            }
            return db;
        }

        public StoreLakeDatabaseBuilder<TDataSet> UseAction(Action<DataSet> extension)
        {
            string key = extension.Method.DeclaringType.FullName + "::" + extension.Method.Name;
            return RegisterExtensionRegistrar(key, extension);
        }

        private StoreLakeDatabaseBuilder<TDataSet> RegisterExtensionRegistrar(string key, Action<DataSet> extension)
        {
            if (registered_extensions.ContainsKey(key))
            {
                // already registered (directly or as a depedency)
            }
            else
            {
                registered_extensions.Add(key, extension);
                extensions.Add(extension);
            }
            return this;
        }

        public StoreLakeDatabaseBuilder<TDataSet> Use(Func<DataSet, DataSet> extension)
        {
            string key = extension.Method.DeclaringType.FullName + "::" + extension.Method.Name;
            Action<DataSet> registrar = x => extension(x);
            return RegisterExtensionRegistrar(key, registrar);
        }
    }

    //public sealed class StoreLakeDatabase : DataSet
    //{
    //    public StoreLakeDatabase(string databaseName) : base(databaseName)
    //    {

    //    }
    //}
}
