using StoreLake.Sdk.SqlDom;
using System;
using System.Collections.Generic;
using System.Data;

namespace StoreLake.Test
{
    class TestSchema : ISchemaMetadataProvider
    {
        internal readonly IDictionary<string, IColumnSourceMetadata> sources = new SortedDictionary<string, IColumnSourceMetadata>();
        internal readonly IDictionary<string, IColumnSourceMetadata> functions = new SortedDictionary<string, IColumnSourceMetadata>();
        IColumnSourceMetadata ISchemaMetadataProvider.TryGetColumnSourceMetadata(string schemaName, string objectName)
        {
            string key;
            if (string.IsNullOrEmpty(schemaName))
            {
                if (objectName[0] == '@')
                    key = objectName.ToUpperInvariant();
                else
                    return null;
            }
            else
            {
                key = TestSource.CreateKey(schemaName, objectName).ToUpperInvariant();
            }
            if (sources.TryGetValue(key, out IColumnSourceMetadata source))
                return source;

            throw new NotImplementedException(key);
            //return null;

        }

        IColumnSourceMetadata ISchemaMetadataProvider.TryGetFunctionTableMetadata(string schemaName, string objectName)
        {
            string key = TestSource.CreateKey(schemaName, objectName).ToUpperInvariant();

            if (functions.TryGetValue(key, out IColumnSourceMetadata source))
                return source;

            throw new NotImplementedException(key);

        }

        internal TestSchema AddSource(TestSource source)
        {
            string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]").ToUpperInvariant();
            sources.Add(key, source);
            return this;
        }

        internal TestSchema AddFunction(TestFunction source)
        {
            string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]").ToUpperInvariant();
            functions.Add(key, source);
            return this;
        }

    }


    class TestSource : IColumnSourceMetadata
    {
        internal readonly string Key;
        internal readonly string SchemaName;
        internal readonly string ObjectName;
        public TestSource(string schemaName, string objectName)
        {
            Key = CreateKey(schemaName, objectName);
            SchemaName = schemaName;
            ObjectName = objectName;
        }
        public TestSource(string objectName)
        {
            Key = objectName.ToUpperInvariant();
            ObjectName = objectName;
        }

        internal static string CreateKey(string schemaName, string objectName)
        {
            return ("[" + schemaName + "].[" + objectName + "]").ToUpperInvariant();
        }

        internal readonly IDictionary<string, TestColumn> columns = new SortedDictionary<string, TestColumn>();


        DbType? IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
        {
            return OnTryGetColumnTypeByName(columnName);
        }
        protected virtual DbType? OnTryGetColumnTypeByName(string columnName)
        {
            string key = columnName.ToUpperInvariant();
            return columns.TryGetValue(key, out TestColumn column)
                ? column.ColumnDbType
                : null;
        }

        internal TestSource AddColumn(string name, DbType columnDbType)
        {
            columns.Add(name.ToUpperInvariant(), new TestColumn(name, columnDbType));
            return this;
        }
    }

    class TestFunction : TestSource, IBatchParameterMetadata
    {
        private readonly Action<TestFunction> loader;
        public TestFunction(string schemaName, string objectName, Action<TestFunction> loader)
            : base(schemaName, objectName)
        {
            this.loader = loader;
        }

        private bool _loaded;


        protected override DbType? OnTryGetColumnTypeByName(string columnName)
        {
            if (!_loaded)
            {
                loader(this);
                _loaded = true;
            }
            return base.OnTryGetColumnTypeByName(columnName);
        }

        Dictionary<string, DbType> parameters = new Dictionary<string, DbType>();
        internal void AddParameter(string parameterName, DbType parameterDbType)
        {
            parameters.Add(parameterName.ToUpperInvariant(), parameterDbType);
        }

        DbType? IBatchParameterMetadata.TryGetParameterType(string parameterName)
        {
            if (parameters.TryGetValue(parameterName.ToUpperInvariant(), out DbType parameterType))
            {
                return parameterType;
            }
            else
            {
                return null;
            }
        }
    }

    class TestColumn
    {
        internal readonly string ColumnName;
        internal readonly DbType ColumnDbType;

        public TestColumn(string columnName, DbType columnDbType)
        {
            ColumnName = columnName;
            ColumnDbType = columnDbType;

        }
    }
}
