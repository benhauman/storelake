namespace StoreLake.Test
{
    using System;
    using System.CodeDom.Compiler;
    using System.Data;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using StoreLake.Sdk.CodeGeneration;

    // https://github.com/benhauman/storelake/issues/7
    [TestClass]
    public class RowVersion_Tests
    {
        private static Type s_extensionsType;
        private static Type s_tableType;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            Assembly asm = GenerateAndCompileStore();
            s_extensionsType = asm.GetTypes().Single(t => t.Name.EndsWith("Extensions", StringComparison.Ordinal));
            s_tableType = asm.GetTypes().Single(t => t.Name == "sampleDataTable");
        }

        [TestMethod]
        public void AddRowWithValues_RowVersionParameter_IsOptional()
        {
            ParameterInfo prm = GetAddRowWithValues().GetParameters().Single(p => p.Name == "chgver");
            Assert.IsTrue(prm.IsOptional, "Parameter 'chgver' should be optional.");
        }

        [TestMethod]
        public void AddRowWithValues_WithoutRowVersion_GeneratesIncreasingValues()
        {
            DataTable table = CreateTable();

            DataRow row1 = AddRow(table, 1, "a");
            DataRow row2 = AddRow(table, 2, "b");

            ulong v1 = ReadRowVersion(row1);
            ulong v2 = ReadRowVersion(row2);
            Assert.IsTrue(v1 > 0, "First rowversion should be greater than 0.");
            Assert.IsTrue(v2 > v1, "Rowversion should increase on every insert.");
        }

        [TestMethod]
        public void Update_ChangesRowVersion()
        {
            DataTable table = CreateTable();
            DataRow row1 = AddRow(table, 1, "a");
            DataRow row2 = AddRow(table, 2, "b");
            ulong v2 = ReadRowVersion(row2);

            row1["name"] = "a2";

            ulong v1_updated = ReadRowVersion(row1);
            Assert.IsTrue(v1_updated > v2, "Rowversion should be set to a new database-wide maximum on update.");
            Assert.AreEqual(v2, ReadRowVersion(row2), "Rowversion of an unchanged row should stay the same.");
        }

        [TestMethod]
        public void Update_InEditBlock_ChangesRowVersion()
        {
            DataTable table = CreateTable();
            DataRow row1 = AddRow(table, 1, "a");
            ulong v1 = ReadRowVersion(row1);

            row1.BeginEdit();
            row1["name"] = "a2";
            row1["description"] = "d";
            row1.EndEdit();

            ulong v1_updated = ReadRowVersion(row1);
            Assert.IsTrue(v1_updated > v1, "Rowversion should change on update.");

            row1["name"] = "a3";
            Assert.IsTrue(ReadRowVersion(row1) > v1_updated, "Rowversion should change on every update.");
        }

        [TestMethod]
        public void RowVersion_IsDatabaseWide()
        {
            DataSet ds = CreateDataSet();
            DataTable table = ds.Tables["sample"];
            DataRow row1 = AddRow(table, 1, "a");
            DataRow row2 = AddRow(table, 2, "b");

            Assert.AreNotEqual(ReadRowVersion(row1), ReadRowVersion(row2));
            Assert.AreEqual(2, table.Rows.Count);
        }

        [TestMethod]
        public void GetRowVersionValue_ReturnsLastAssignedRowVersion()
        {
            DataTable table = CreateTable();
            Assert.AreEqual(0UL, ToUInt64(GetRowVersionValue(table)), "No rowversion assigned yet.");

            DataRow row1 = AddRow(table, 1, "a");
            Assert.AreEqual(ReadRowVersion(row1), ToUInt64(GetRowVersionValue(table)));

            DataRow row2 = AddRow(table, 2, "b");
            Assert.AreEqual(ReadRowVersion(row2), ToUInt64(GetRowVersionValue(table)));

            row1["name"] = "a2";
            Assert.AreEqual(ReadRowVersion(row1), ToUInt64(GetRowVersionValue(table)), "Update assigns the new database rowversion.");
        }

        [TestMethod]
        public void GetRowVersionValueAsLong_ReturnsCounter()
        {
            DataTable table = CreateTable();
            Assert.AreEqual(0L, GetRowVersionValueAsLong(table));

            DataRow row1 = AddRow(table, 1, "a");
            DataRow row2 = AddRow(table, 2, "b");
            Assert.AreEqual(2L, GetRowVersionValueAsLong(table));
            Assert.AreEqual((long)ReadRowVersion(row2), GetRowVersionValueAsLong(table));

            row1["name"] = "a2";
            Assert.AreEqual((long)ReadRowVersion(row1), GetRowVersionValueAsLong(table));
            Assert.AreEqual((long)ToUInt64(GetRowVersionValue(table)), GetRowVersionValueAsLong(table));
        }

        private static byte[] GetRowVersionValue(DataTable table)
        {
            MethodInfo method = s_tableType.GetMethod("GetRowVersionValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.IsNotNull(method, "Table should offer public 'GetRowVersionValue()'.");
            Assert.AreEqual(typeof(byte[]), method.ReturnType);
            return (byte[])method.Invoke(table, null);
        }

        private static long GetRowVersionValueAsLong(DataTable table)
        {
            MethodInfo method = s_tableType.GetMethod("GetRowVersionValueAsLong", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.IsNotNull(method, "Table should offer public 'GetRowVersionValueAsLong()'.");
            Assert.AreEqual(typeof(long), method.ReturnType);
            return (long)method.Invoke(table, null);
        }

        private static MethodInfo GetAddRowWithValues()
        {
            return s_tableType.GetMethod("AddRowWithValues", BindingFlags.Public | BindingFlags.Instance);
        }

        private static DataRow AddRow(DataTable table, int id, string name)
        {
            MethodInfo method = GetAddRowWithValues();
            object[] args = method.GetParameters().Select(p =>
            {
                if (p.Name == "id") return (object)id;
                if (p.Name == "name") return name;
                return Type.Missing;
            }).ToArray();
            return (DataRow)method.Invoke(table, BindingFlags.OptionalParamBinding | BindingFlags.InvokeMethod, null, args, null);
        }

        private static ulong ReadRowVersion(DataRow row)
        {
            object value = row["chgver"];
            Assert.IsInstanceOfType(value, typeof(byte[]), "Rowversion should be generated.");
            return ToUInt64((byte[])value);
        }

        private static ulong ToUInt64(byte[] bytes)
        {
            Assert.IsNotNull(bytes);
            Assert.AreEqual(8, bytes.Length, "Rowversion should be 8 bytes.");
            ulong result = 0;
            foreach (byte b in bytes)
            {
                result = (result << 8) | b; // big-endian like SQL Server
            }
            return result;
        }

        private static DataTable CreateTable()
        {
            return CreateDataSet().Tables["sample"];
        }

        private static DataSet CreateDataSet()
        {
            DataSet ds = new DataSet() { Namespace = "dbo" };
            MethodInfo init = s_extensionsType.GetMethod("InitDataSetClass", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(init, "InitDataSetClass");
            init.Invoke(null, new object[] { ds });
            return ds;
        }

        private const string SampleTableDDL = @"
CREATE TABLE [dbo].[sample]
(
    [id]          INT            NOT NULL CONSTRAINT [PK_sample] PRIMARY KEY,
    [name]        NVARCHAR(255)  NOT NULL,
    [description] NVARCHAR(MAX)  NULL,
    [chgver]      ROWVERSION     NOT NULL
)";

        private static Assembly GenerateAndCompileStore()
        {
            string code = TableGenerator.GenerateTablesCode("Sample.TestStore", SampleTableDDL);

            CompilerParameters comparam = new CompilerParameters() { GenerateInMemory = true, GenerateExecutable = false };
            foreach (Assembly reference in new[] { typeof(System.ComponentModel.MarshalByValueComponent).Assembly, typeof(DataTable).Assembly, typeof(TypedTableBase<>).Assembly, typeof(System.Xml.Serialization.IXmlSerializable).Assembly, typeof(Enumerable).Assembly })
            {
                comparam.ReferencedAssemblies.Add(reference.Location);
            }

            using (Microsoft.CSharp.CSharpCodeProvider codeProvider = new Microsoft.CSharp.CSharpCodeProvider())
            {
                CompilerResults results = codeProvider.CompileAssemblyFromSource(comparam, code);
                if (results.Errors.HasErrors)
                {
                    string errors = string.Join(Environment.NewLine, results.Errors.Cast<CompilerError>().Where(e => !e.IsWarning).Select(e => e.Line + ": " + e.ErrorText));
                    Assert.Fail("Generated store does not compile:" + Environment.NewLine + errors + Environment.NewLine + code);
                }
                return results.CompiledAssembly;
            }
        }

    }
}
