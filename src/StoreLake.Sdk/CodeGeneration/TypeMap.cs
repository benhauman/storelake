using StoreLake.Sdk.SqlDom;
using System;
using System.Collections.Generic;
using System.Data;
using System.Xml.Linq;

namespace StoreLake.Sdk.CodeGeneration
{
    public static class TypeMap
    {
        internal static IDictionary<string, string> _builtinTypeAlias = new SortedDictionary<string, string>() {
            { typeof(bool).FullName, "bool" },
            { typeof(byte).FullName, "byte" },
            { typeof(char).FullName, "char" },
            { typeof(Decimal).FullName, "decimal" },
            { typeof(double).FullName, "double" },
            { typeof(float).FullName, "float" }, // Single
            { typeof(int).FullName, "int" },
            { typeof(long).FullName, "long" },
            { typeof(short).FullName, "short" },
            { typeof(string).FullName, "string" },
        };
        internal static readonly IDictionary<string, string> notnull_nullable_map = new Dictionary<string, string>() {
                        { typeof(int).FullName, "int?" },
                        { typeof(short).FullName, "short?" },
                        { typeof(bool).FullName, "bool?" },
                        { typeof(DateTime).FullName, "System.DateTime?" },
                        { typeof(Decimal).FullName, "decimal?" },
                        { typeof(Guid).FullName, "System.Guid?" },
                        { typeof(long).FullName, "long?" },
                        { typeof(float).FullName, "float?" }, // Single
                        { typeof(byte).FullName, "byte?" },
                        { typeof(string).FullName, "string" },
                        //{ typeof(byte[]).FullName, "byte[]" },
                    };

        internal static IDictionary<string, ParameterTypeMap> s_ParameterTypeMap = InitializeParameterTypeMap();
        private static IDictionary<string, ParameterTypeMap> AddParameterTypeMap<TNotNull, TNull>(IDictionary<string, ParameterTypeMap> dict)
        {
            dict.Add(typeof(TNotNull).AssemblyQualifiedName, new ParameterTypeMap(typeof(TNotNull), typeof(TNull)));
            return dict;
        }
        private static IDictionary<string, ParameterTypeMap> InitializeParameterTypeMap()
        {
            IDictionary<string, ParameterTypeMap> dict = new SortedDictionary<string, ParameterTypeMap>();
            AddParameterTypeMap<string, string>(dict);
            AddParameterTypeMap<bool, bool?>(dict);
            AddParameterTypeMap<byte, byte?>(dict);
            AddParameterTypeMap<short, short?>(dict);
            AddParameterTypeMap<int, int?>(dict);
            AddParameterTypeMap<long, long?>(dict);
            AddParameterTypeMap<DateTime, DateTime?>(dict);
            AddParameterTypeMap<Guid, Guid?>(dict);
            AddParameterTypeMap<decimal, decimal?>(dict);
            AddParameterTypeMap<byte[], byte[]>(dict);
            return dict;
        }

        internal static ProcedureCodeParameter GetParameterClrType(StoreLakeParameterRegistration parameter)
        {
            if (parameter.ParameterDbType == SqlDbType.Structured)
            {
                return ProcedureCodeParameter.CreateUdt(parameter.ParameterTypeFullName, parameter.StructureTypeSchemaName, parameter.StructureTypeName);
            }
            return GetParameterClrType(parameter.ParameterDbType, parameter.ParameterTypeFullName);
        }
        public static ProcedureCodeParameter GetParameterClrType(SqlDbType parameter_ParameterDbType, string parameter_ParameterTypeFullName)
        {
            // https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-data-type-mappings
            if (parameter_ParameterDbType == SqlDbType.Structured)
            {
                var name_tokens = ReadSchemaObjectName(1, parameter_ParameterTypeFullName);
                return ProcedureCodeParameter.CreateUdt(parameter_ParameterTypeFullName, name_tokens.SchemaName, name_tokens.ObjectName);
            }
            if (parameter_ParameterDbType == SqlDbType.Bit)
                return ProcedureCodeParameter.Create<bool, bool?>(DbType.Boolean);
            if (parameter_ParameterDbType == SqlDbType.NVarChar)
                return ProcedureCodeParameter.Create<string, string>(DbType.String);
            if (parameter_ParameterDbType == SqlDbType.Int)
                return ProcedureCodeParameter.Create<int, int?>(DbType.Int32);
            if (parameter_ParameterDbType == SqlDbType.BigInt)
                return ProcedureCodeParameter.Create<long, long?>(DbType.Int64);
            if (parameter_ParameterDbType == SqlDbType.SmallInt)
                return ProcedureCodeParameter.Create<short, short?>(DbType.Int16);
            if (parameter_ParameterDbType == SqlDbType.TinyInt)
                return ProcedureCodeParameter.Create<byte, byte?>(DbType.Byte);
            if (parameter_ParameterDbType == SqlDbType.UniqueIdentifier)
                return ProcedureCodeParameter.Create<Guid, Guid?>(DbType.Guid);
            if (parameter_ParameterDbType == SqlDbType.Xml)
                return ProcedureCodeParameter.Create<System.Xml.Linq.XElement, System.Xml.Linq.XElement>(DbType.Xml);
            if (parameter_ParameterDbType == SqlDbType.DateTime)
                return ProcedureCodeParameter.Create<DateTime, DateTime?>(DbType.DateTime);
            if (parameter_ParameterDbType == SqlDbType.VarBinary)
                return ProcedureCodeParameter.Create<byte[], byte[]>(DbType.Binary);
            if (parameter_ParameterDbType == SqlDbType.Decimal)
                return ProcedureCodeParameter.Create<decimal, decimal?>(DbType.Decimal);
            if (parameter_ParameterDbType == SqlDbType.NChar)
                return ProcedureCodeParameter.Create<string, string>(DbType.StringFixedLength); // taskmanagement
            if (parameter_ParameterDbType == SqlDbType.Date)
                return ProcedureCodeParameter.Create<DateTime, DateTime?>(DbType.DateTime);

            throw new NotImplementedException("" + parameter_ParameterDbType);
        }

        private sealed class SchemaObjectXName
        {
            internal string FullName;
            internal int PartsCount;

            internal string SchemaName; // 0
            internal string ObjectName; // 1
            internal string ItemName; // 2 column or parameter
        }

        private static SchemaObjectXName ReadSchemaObjectName(int min, string fullName)
        {
            string[] name_tokens = fullName.Split('.');
            if (name_tokens.Length < min)
            {
                throw new InvalidOperationException("Too less parts specified. Expected:" + min + " :" + fullName);
            }

            SchemaObjectXName soName = new SchemaObjectXName() { FullName = fullName, PartsCount = name_tokens.Length };

            if (name_tokens.Length == 1)
            {
                soName.SchemaName = "dbo";
                soName.ObjectName = DequoteName(name_tokens[0]);
                soName.ItemName = null;
            }
            else if (name_tokens.Length == 2)
            {
                soName.SchemaName = DequoteName(name_tokens[0]);
                soName.ObjectName = DequoteName(name_tokens[1]);
                soName.ItemName = null;
            }
            else if (name_tokens.Length == 3)
            {
                soName.SchemaName = DequoteName(name_tokens[0]);
                soName.ObjectName = DequoteName(name_tokens[1]);
                soName.ItemName = DequoteName(name_tokens[2]);
            }
            else
            {
                throw new NotSupportedException(fullName);
            }
            //if (soName.SchemaName[0] != '[')
            //{
            //    // Schema name must be the same as in DataSet.Namespace otherwise problem in DataSet schema generation
            //}
            return soName;
        }

        private static string DequoteName(string quotedName)
        {
            if (string.IsNullOrEmpty(quotedName))
                return quotedName;
            if (quotedName[0] == '[' && quotedName[quotedName.Length - 1] == ']')
            {
                return quotedName.Substring(1, quotedName.Length - 2);
            }
            return quotedName;
        }


        public static Type ResolveColumnClrType(DbType columnDbType)
        {
            if (columnDbType == DbType.Int32)
                return typeof(int);
            if (columnDbType == DbType.String)
                return typeof(string);
            if (columnDbType == DbType.Byte)
                return typeof(byte);
            if (columnDbType == DbType.Int16)
                return typeof(short);
            if (columnDbType == DbType.Guid)
                return typeof(Guid);
            if (columnDbType == DbType.DateTime)
                return typeof(DateTime);
            if (columnDbType == DbType.Boolean)
                return typeof(bool);
            if (columnDbType == DbType.Binary)
                return typeof(byte[]);
            if (columnDbType == DbType.Int64)
                return typeof(long);
            if (columnDbType == DbType.Decimal)
                return typeof(decimal);
            if (columnDbType == DbType.Xml)
                return typeof(XElement);
            if (columnDbType == DbType.DateTimeOffset)
                return typeof(DateTime); // ?DateTimeOffset
            throw new NotImplementedException("" + columnDbType);
        }
    }

    internal sealed class ParameterTypeMap
    {
        public readonly Type TypeNotNull;
        public readonly Type TypeNull;
        public ParameterTypeMap(Type typeNotNull, Type typeNull)
        {
            TypeNotNull = typeNotNull;
            TypeNull = typeNull;
        }
    }

}
