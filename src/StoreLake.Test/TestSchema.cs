using StoreLake.Sdk.SqlDom;
using System;
using System.Collections.Generic;
using System.Data;

namespace StoreLake.Test
{
    class TestSchema : ISchemaMetadataProvider
    {
        internal readonly IDictionary<string, IColumnSourceMetadata> tables = new SortedDictionary<string, IColumnSourceMetadata>(StringComparer.OrdinalIgnoreCase);
        internal readonly IDictionary<string, IColumnSourceMetadata> views = new SortedDictionary<string, IColumnSourceMetadata>(StringComparer.OrdinalIgnoreCase);
        internal readonly IDictionary<string, IColumnSourceMetadata> functions = new SortedDictionary<string, IColumnSourceMetadata>(StringComparer.OrdinalIgnoreCase);
        internal readonly IDictionary<string, IColumnSourceMetadata> udts = new SortedDictionary<string, IColumnSourceMetadata>(StringComparer.OrdinalIgnoreCase);
        IColumnSourceMetadata ISchemaMetadataProvider.TryGetColumnSourceMetadata(string schemaName, string objectName)
        {
            string key;
            if (string.IsNullOrEmpty(schemaName))
            {
                if (objectName[0] == '@')
                    key = objectName;
                else
                    return null;
            }
            else
            {
                key = TestTable.CreateKey(schemaName, objectName);
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
            string key = TestTable.CreateKey(schemaName, objectName);

            if (functions.TryGetValue(key, out IColumnSourceMetadata source))
                return source;

            throw new NotImplementedException(key);

        }
        IColumnSourceMetadata ISchemaMetadataProvider.TryGetUserDefinedTableTypeMetadata(string schemaName, string objectName)
        {
            string key;
            if (string.IsNullOrEmpty(schemaName))
            {
                if (objectName[0] == '@')
                    key = objectName;
                else
                    return null;
            }
            else
            {
                key = TestTable.CreateKey(schemaName, objectName);
            }
            if (udts.TryGetValue(key, out IColumnSourceMetadata sourceT))
                return sourceT;

            throw new NotImplementedException(key);
            //return null;

        }

        internal TestSchema AddTable(TestTable source)
        {
            string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]");
            tables.Add(key, source);
            return this;
        }
        internal TestSchema AddUDT(TestTable source)
        {
            string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]");
            udts.Add(key, source);
            return this;
        }

        internal TestSchema AddView(TestView source)
        {
            string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]");
            views.Add(key, source);
            return this;
        }

        internal TestSchema AddFunction(TestFunction source)
        {
            string key = ("[" + source.SchemaName + "].[" + source.ObjectName + "]");
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
            if (string.IsNullOrEmpty(objectName))
                throw new ArgumentNullException(nameof(objectName));
            Key = objectName;
            ObjectName = objectName;
        }

        internal static string CreateKey(string schemaName, string objectName)
        {
            return ("[" + schemaName + "].[" + objectName + "]");
        }

        internal readonly IDictionary<string, TestColumn> columns = new SortedDictionary<string, TestColumn>(StringComparer.OrdinalIgnoreCase);


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
            string key = columnName;
            return columns.TryGetValue(key, out TestColumn column)
                ? column.ColumnDbType
                : null;
        }
        protected void AddSourceColumn(string name, DbType columnDbType)
        {
            columns.Add(name, new TestColumn(name, columnDbType));
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

        Dictionary<string, DbType> parameters = new Dictionary<string, DbType>(StringComparer.OrdinalIgnoreCase);
        internal void AddParameter(string parameterName, DbType parameterDbType)
        {
            parameters.Add(parameterName, parameterDbType);
        }

        DbType? IBatchParameterMetadata.TryGetParameterType(string parameterName)
        {
            if (parameters.TryGetValue(parameterName, out DbType parameterType))
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
        //    parameters.Add(parameterName, parameterDbType);
        //}

        DbType? IBatchParameterMetadata.TryGetParameterType(string parameterName)
        {
            //if (parameters.TryGetValue(parameterName, out DbType parameterType))
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
