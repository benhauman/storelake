namespace StoreLake.Sdk.CodeGeneration
{
    using System;
    using System.CodeDom;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using StoreLake.Sdk.SqlDom;

    /// <summary>
    /// Generates the typed table code for 'CREATE TABLE' scripts, the same way the dacpac import does for 'SqlTable' elements.
    /// </summary>
    public static class TableGenerator
    {
        public static string GenerateTablesCode(string namespaceName, params string[] createTableScripts)
        {
            if (string.IsNullOrEmpty(namespaceName))
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }
            if (createTableScripts == null || createTableScripts.Length == 0)
            {
                throw new ArgumentNullException(nameof(createTableScripts));
            }

            DataSet ds = new DataSet() { Namespace = "dbo" };
            RegistrationResult rr = new RegistrationResult(ds, false, false);
            DacPacRegistration dacpac = new DacPacRegistration(namespaceName + ".dacpac", false)
            {
                DacPacAssemblyLogicalName = namespaceName + ".dacpac",
                TestStoreAssemblyNamespace = namespaceName,
                TestStoreExtensionSetName = namespaceName.Replace(".", "").Replace("-", ""),
            };

            foreach (string createTableScript in createTableScripts)
            {
                foreach (CreateTableStatement stmt in ParseCreateTableStatements(createTableScript))
                {
                    RegisterTable(rr, dacpac, stmt);
                }
            }

            string schemaContent;
            using (StringWriter xsdWriter = new StringWriter())
            {
                ds.WriteXmlSchema(xsdWriter);
                schemaContent = xsdWriter.ToString();
            }

            Microsoft.CSharp.CSharpCodeProvider codeProvider = new Microsoft.CSharp.CSharpCodeProvider();
            CompilerParameters comparam = new CompilerParameters();
            CodeCompileUnit ccu_tables = new CodeCompileUnit();
            CodeCompileUnit ccu_tabletypes = new CodeCompileUnit();
            CodeCompileUnit ccu_procedures = new CodeCompileUnit();
            AssemblyResolver assemblyResolver = new AssemblyResolver(Path.GetTempPath(), new string[0], Path.GetTempPath());
            SchemaExportCode.GenerateTablesCode(assemblyResolver, comparam, rr, dacpac, schemaContent, namespaceName, "TestStore", codeProvider, ccu_tables, ccu_tabletypes, ccu_procedures);

            using (StringWriter code = new StringWriter())
            {
                codeProvider.GenerateCodeFromCompileUnit(ccu_tables, code, null);
                return code.ToString();
            }
        }

        private static IEnumerable<CreateTableStatement> ParseCreateTableStatements(string createTableScript)
        {
            TSqlScript script = (TSqlScript)ScriptDomFacade.Parse(createTableScript);
            return script.Batches.SelectMany(b => b.Statements).OfType<CreateTableStatement>();
        }

        private static void RegisterTable(RegistrationResult rr, DacPacRegistration dacpac, CreateTableStatement stmt)
        {
            StoreLakeTableRegistration treg = new StoreLakeTableRegistration()
            {
                TableSchema = stmt.SchemaObjectName.SchemaIdentifier != null ? stmt.SchemaObjectName.SchemaIdentifier.Dequote() : "dbo",
                TableName = stmt.SchemaObjectName.BaseIdentifier.Dequote(),
            };

            List<string> pkColumns = new List<string>();
            string pkName = null;
            foreach (ColumnDefinition col in stmt.Definition.ColumnDefinitions)
            {
                string columnName = col.ColumnIdentifier.Dequote();
                NullableConstraintDefinition nullable = col.Constraints.OfType<NullableConstraintDefinition>().FirstOrDefault();
                treg.Columns.Add(new StoreLakeColumnRegistration()
                {
                    ColumnName = columnName,
                    ColumnDbType = ResolveColumnDbType(columnName, col.DataType),
                    IsNullable = nullable == null || nullable.Nullable,
                    IsIdentity = col.IdentityOptions != null,
                });

                UniqueConstraintDefinition columnPk = col.Constraints.OfType<UniqueConstraintDefinition>().FirstOrDefault(c => c.IsPrimaryKey);
                if (columnPk != null)
                {
                    pkName = columnPk.ConstraintIdentifier != null ? columnPk.ConstraintIdentifier.Dequote() : null;
                    pkColumns.Add(columnName);
                }
            }

            UniqueConstraintDefinition tablePk = stmt.Definition.TableConstraints.OfType<UniqueConstraintDefinition>().FirstOrDefault(c => c.IsPrimaryKey);
            if (tablePk != null)
            {
                pkName = tablePk.ConstraintIdentifier != null ? tablePk.ConstraintIdentifier.Dequote() : null;
                pkColumns.AddRange(tablePk.Columns.Select(c => c.Column.MultiPartIdentifier.Identifiers.Last().Dequote()));
            }

            rr.registered_tables.Add(treg.TableName, dacpac);
            dacpac.registered_tables.Add(treg.TableName, true);
            DatabaseRegistration.RegisterTable(rr.ds, treg);

            if (pkColumns.Count > 0)
            {
                StoreLakeTableKeyRegistration pk = new StoreLakeTableKeyRegistration()
                {
                    TableSchema = treg.TableSchema,
                    TableName = treg.TableName,
                    KeySchema = treg.TableSchema,
                    KeyName = pkName ?? ("PK_" + treg.TableName),
                };
                pk.Columns.AddRange(pkColumns.Select(c => new StoreLakeKeyColumnRegistration() { ColumnName = c }));
                DatabaseRegistration.RegisterPrimaryKey(rr.ds, pk);
            }
        }

        private static SqlDbType ResolveColumnDbType(string columnName, DataTypeReference dataType)
        {
            string typeName = string.Join("].[", dataType.Name.Identifiers.Select(i => i.Value.ToLowerInvariant()));
            return SchemaImportDacPac.ParseKnownDbTypeFullName(columnName, "[" + typeName + "]");
        }
    }
}
