namespace StoreLake.Test
{
    using System;
    using System.CodeDom;
    using System.CodeDom.Compiler;
    using System.Data;
    using System.IO;
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
            byte[] bytes = (byte[])value;
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

        private static Assembly GenerateAndCompileStore()
        {
            DataSet ds = new DataSet() { Namespace = "dbo" };
            RegistrationResult rr = new RegistrationResult(ds, false, false);
            DacPacRegistration dacpac = new DacPacRegistration("Sample.dacpac", false)
            {
                DacPacAssemblyLogicalName = "Sample.dacpac",
                TestStoreAssemblyNamespace = "Sample.TestStore",
                TestStoreExtensionSetName = "Sample",
            };

            StoreLakeTableRegistration treg = new StoreLakeTableRegistration() { TableSchema = "dbo", TableName = "sample" };
            treg.Columns.Add(new StoreLakeColumnRegistration() { ColumnName = "id", ColumnDbType = SqlDbType.Int, IsNullable = false });
            treg.Columns.Add(new StoreLakeColumnRegistration() { ColumnName = "name", ColumnDbType = SqlDbType.NVarChar, IsNullable = false });
            treg.Columns.Add(new StoreLakeColumnRegistration() { ColumnName = "description", ColumnDbType = SqlDbType.NVarChar, IsNullable = true });
            treg.Columns.Add(new StoreLakeColumnRegistration() { ColumnName = "chgver", ColumnDbType = SqlDbType.Timestamp, IsNullable = false });
            rr.registered_tables.Add(treg.TableName, dacpac);
            dacpac.registered_tables.Add(treg.TableName, true);
            DatabaseRegistration.RegisterTable(ds, treg);

            StoreLakeTableKeyRegistration pk = new StoreLakeTableKeyRegistration() { TableSchema = "dbo", TableName = "sample", KeyName = "PK_sample", KeySchema = "dbo" };
            pk.Columns.Add(new StoreLakeKeyColumnRegistration() { ColumnName = "id" });
            DatabaseRegistration.RegisterPrimaryKey(ds, pk);

            string schemaContent;
            using (StringWriter xsdWriter = new StringWriter())
            {
                ds.WriteXmlSchema(xsdWriter);
                schemaContent = xsdWriter.ToString();
            }

            Microsoft.CSharp.CSharpCodeProvider codeProvider = new Microsoft.CSharp.CSharpCodeProvider();
            CompilerParameters comparam = new CompilerParameters() { GenerateInMemory = true, GenerateExecutable = false };

            CodeCompileUnit ccu_tables = new CodeCompileUnit();
            CodeCompileUnit ccu_tabletypes = new CodeCompileUnit();
            CodeCompileUnit ccu_procedures = new CodeCompileUnit();
            AssemblyResolver assemblyResolver = new AssemblyResolver(Path.GetTempPath(), new string[0], Path.GetTempPath());
            SchemaExportCode.GenerateTablesCode(assemblyResolver, comparam, rr, dacpac, schemaContent, "Sample", "TestStore", codeProvider, ccu_tables, ccu_tabletypes, ccu_procedures);

            foreach (Assembly reference in new[] { typeof(System.ComponentModel.MarshalByValueComponent).Assembly, typeof(DataTable).Assembly, typeof(TypedTableBase<>).Assembly, typeof(System.Xml.Serialization.IXmlSerializable).Assembly, typeof(Enumerable).Assembly })
            {
                string fileName = Path.GetFileName(reference.Location);
                bool alreadyReferenced = comparam.ReferencedAssemblies.Cast<string>().Concat(ccu_tables.ReferencedAssemblies.Cast<string>())
                    .Any(r => string.Equals(Path.GetFileName(r), fileName, StringComparison.OrdinalIgnoreCase));
                if (!alreadyReferenced)
                {
                    comparam.ReferencedAssemblies.Add(reference.Location);
                }
            }

            CompilerResults results = codeProvider.CompileAssemblyFromDom(comparam, ccu_tables, ccu_tabletypes);
            if (results.Errors.HasErrors)
            {
                using (StringWriter code = new StringWriter())
                {
                    codeProvider.GenerateCodeFromCompileUnit(ccu_tables, code, null);
                    string errors = string.Join(Environment.NewLine, results.Errors.Cast<CompilerError>().Where(e => !e.IsWarning).Select(e => e.Line + ": " + e.ErrorText));
                    Assert.Fail("Generated store does not compile:" + Environment.NewLine + errors + Environment.NewLine + code);
                }
            }
            return results.CompiledAssembly;
        }
    }
}
