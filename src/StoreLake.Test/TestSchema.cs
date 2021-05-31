using StoreLake.Sdk.SqlDom;
using System;
using System.Collections.Generic;
using System.Data;

namespace StoreLake.Test
{
    class TestSchema : ISchemaMetadataProvider
    {
        internal readonly IDictionary<string, IColumnSourceMetadata> tables = new SortedDictionary<string, IColumnSourceMetadata>();
        internal readonly IDictionary<string, IColumnSourceMetadata> views = new SortedDictionary<string, IColumnSourceMetadata>();
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
                key = TestTable.CreateKey(schemaName, objectName).ToUpperInvariant();
            }
            if (tables.TryGetValue(key, out IColumnSourceMetadata sourceT))
                return sourceT;
            if (views.TryGetValue(key, out IColumnSourceMetadata sourceV))
                return sourceV;

            throw new NotImplementedException(key);
            //return null;

        }

        IColumnSourceMetadata ISchemaMetadataProvider.TryGetFunctionTableMetadata(string schemaName, string objectName)
        {
            string key = TestTable.CreateKey(schemaName, objectName).ToUpperInvariant();

            if (functions.TryGetValue(key, out IColumnSourceMetadata source))
                return source;

            throw new NotImplementedException(key);

        }

        internal TestSchema AddTable(TestTable source)
        {
            string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]").ToUpperInvariant();
            tables.Add(key, source);
            return this;
        }

        internal TestSchema AddView(TestView source)
        {
            string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]").ToUpperInvariant();
            views.Add(key, source);
            return this;
        }

        internal TestSchema AddFunction(TestFunction source)
        {
            string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]").ToUpperInvariant();
            functions.Add(key, source);
            return this;
        }

    }


    abstract class TestSourceBase : IColumnSourceMetadata
    {
        internal readonly string Key;
        internal readonly string SchemaName;
        internal readonly string ObjectName;
        protected TestSourceBase(string schemaName, string objectName)
        {
            Key = CreateKey(schemaName, objectName);
            SchemaName = schemaName;
            ObjectName = objectName;
        }
        protected TestSourceBase(string objectName)
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
            if (string.IsNullOrEmpty(columnName))
                throw new ArgumentNullException(nameof(columnName));
            return OnTryGetColumnTypeByName(columnName);
        }
        protected virtual DbType? OnTryGetColumnTypeByName(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                throw new ArgumentNullException(nameof(columnName));
            string key = columnName.ToUpperInvariant();
            return columns.TryGetValue(key, out TestColumn column)
                ? column.ColumnDbType
                : null;
        }
        protected void AddSourceColumn(string name, DbType columnDbType)
        {
            columns.Add(name.ToUpperInvariant(), new TestColumn(name, columnDbType));
        }

    }

    sealed class TestTable : TestSourceBase
    {
        public TestTable(string schemaName, string objectName)
            : base(schemaName, objectName)
        {
        }


        internal TestTable AddColumn(string name, DbType columnDbType)
        {
            AddSourceColumn(name, columnDbType);
            return this;
        }
    }

    sealed class TestFunction : TestSourceBase, IBatchParameterMetadata
    {
        private readonly Action<TestFunction> loader;
        internal readonly string FunctionBodyScript;
        public TestFunction(string schemaName, string objectName, string functionBodyScript, Action<TestFunction> loader)
            : base(schemaName, objectName)
        {
            FunctionBodyScript = functionBodyScript;
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

        internal void AddFunctionColumn(string name, DbType columnDbType)
        {
            AddSourceColumn(name, columnDbType);
            //return this;
        }
    }

    sealed class TestView : TestSourceBase, IBatchParameterMetadata
    {
        private readonly Action<TestView> loader;
        internal readonly string Body;
        public TestView(string schemaName, string objectName, string body, Action<TestView> loader)
            : base(schemaName, objectName)
        {
            Body = body;
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

        //Dictionary<string, DbType> parameters = new Dictionary<string, DbType>();
        //internal void AddParameter(string parameterName, DbType parameterDbType)
        //{
        //    parameters.Add(parameterName.ToUpperInvariant(), parameterDbType);
        //}

        DbType? IBatchParameterMetadata.TryGetParameterType(string parameterName)
        {
            //if (parameters.TryGetValue(parameterName.ToUpperInvariant(), out DbType parameterType))
            //{
            //    return parameterType;
            //}
            //else
            //{
            //    return null;
            //}
            throw new NotImplementedException(parameterName);
        }

        internal void AddViewColumn(string outputColumnName, DbType columnDbType)
        {
            AddSourceColumn(outputColumnName, columnDbType);
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
