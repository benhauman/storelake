using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace StoreLake.Sdk.CodeGeneration
{
    [DebuggerDisplay("{DacPacAssemblyAssemblyName} : {DacPacAssemblyLogicalName}")]
    internal sealed class DacPacRegistration
    {
        internal readonly string FilePath;
        internal readonly bool IsReferencedPackage; // referenced dacpac do not generates models. this can be changed with 'dependencymodelgeneration'=true;
        internal DacPacRegistration(string filePath, bool isReferencedPackage)
        {
            FilePath = filePath;
            IsReferencedPackage = isReferencedPackage;
        }

        internal string DacPacAssemblyAssemblyName { get; set; }
        internal string DacPacAssemblyFileName { get; set; }
        internal string DacPacAssemblyLogicalName { get; set; }
        public string UniqueKey { get; internal set; }
        public int DacPacDependencyLevel { get; internal set; }
        public string TestStoreAssemblyFullFileName { get; internal set; }
        public string TestStoreAssemblyNamespace { get; internal set; }
        public string TestStoreExtensionSetName { get; internal set; }

        internal readonly IDictionary<string, DacPacRegistration> referenced_dacpacs = new SortedDictionary<string, DacPacRegistration>(); // <logicalname, dacpac.filename>
        internal readonly IDictionary<string, bool> registered_tables = new SortedDictionary<string, bool>(); // < ;
        internal readonly IDictionary<string, string> referenced_assemblies = new SortedDictionary<string, string>(); // assemblyname/assemblylocation;
        internal readonly IDictionary<string, StoreLakeCheckConstraintRegistration> registered_CheckConstraints = new SortedDictionary<string, StoreLakeCheckConstraintRegistration>();
        internal readonly IDictionary<string, StoreLakeProcedureRegistration> registered_Procedures = new SortedDictionary<string, StoreLakeProcedureRegistration>();
    }


    public sealed class RegistrationResult
    {
        internal RegistrationResult(DataSet ds, bool forceReferencePackageRegeneration, bool generateMissingReferences)
        {
            this.ds = ds;
            ForceReferencePackageRegeneration = forceReferencePackageRegeneration;
            GenerateMissingReferences = generateMissingReferences;
        }
        internal readonly bool ForceReferencePackageRegeneration;
        internal readonly bool GenerateMissingReferences;
        internal readonly DataSet ds;
        internal readonly IDictionary<string, DacPacRegistration> registered_dacpacs = new SortedDictionary<string, DacPacRegistration>(); // <logicalname, dacpac.filename>
        internal readonly IDictionary<string, DacPacRegistration> procesed_files = new SortedDictionary<string, DacPacRegistration>(); // <logicalname, dacpac.filename>
        internal readonly IDictionary<string, DacPacRegistration> registered_tables = new SortedDictionary<string, DacPacRegistration>(); // <tablename, dacpac.logicalname>
    }


    public static class SchemaImportDacPac // 'Dedicated Administrator Connection (for Data Tier Application) Package'
    {
        public static RegistrationResult ImportDacPac(string inputdir, string dacpacFullFileName, bool forceReferencePackageRegeneration, bool generateMissingReferences)
        {
            //string databaseName = "DemoTestData";
            DataSet ds = new DataSet() { Namespace = "[dbo]" }; // see 'https://www.codeproject.com/articles/30490/how-to-manually-create-a-typed-datatable'
            RegistrationResult ctx = new RegistrationResult(ds, forceReferencePackageRegeneration, generateMissingReferences);
            RegisterDacpac(" ", ctx, inputdir, dacpacFullFileName, false);
            return ctx;
        }


        private static DacPacRegistration RegisterDacpac(string outputprefix, RegistrationResult ctx, string inputdir, string filePath, bool isReferencedPackage)
        {
            DacPacRegistration dacpac;
            if (ctx.procesed_files.TryGetValue(filePath.ToUpperInvariant(), out dacpac))
            {
                return dacpac;
            }
            dacpac = new DacPacRegistration(filePath, isReferencedPackage);
            ctx.procesed_files.Add(filePath.ToUpperInvariant(), dacpac);
            Console.WriteLine(outputprefix + filePath);
            using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("model.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream stream = entry.Open())
                        {
                            var xdoc = XDocument.Load(stream);
                            XElement xDataSchemaModel = xdoc.Root;

                            XElement xHeader = xDataSchemaModel.Element(XName.Get("Header", "http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02"));

                            XElement xReference_Assembly = xHeader.Elements().Single(e => e.Name.LocalName == "CustomData"
                                && e.Attributes().Any(a => a.Name.LocalName == "Category" && a.Value == "Reference")
                                && e.Attributes().Any(a => a.Name.LocalName == "Type" && a.Value == "Assembly")
                                );
                            XElement xReference_Assembly_FileName = xReference_Assembly.Elements().Single(e => e.Name.LocalName == "Metadata"
                                    && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "FileName")
                                    && e.Attributes().Any(a => a.Name.LocalName == "Value")
                                    );

                            XElement xReference_Assembly_AssemblyName = xReference_Assembly.Elements().Single(e => e.Name.LocalName == "Metadata"
                                    && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "AssemblyName")
                                    && e.Attributes().Any(a => a.Name.LocalName == "Value")
                                    );
                            dacpac.DacPacAssemblyAssemblyName = xReference_Assembly_AssemblyName.Attributes().Single(a => a.Name.LocalName == "Value").Value;
                            dacpac.UniqueKey = dacpac.DacPacAssemblyAssemblyName.ToUpperInvariant();
                            dacpac.DacPacAssemblyFileName = xReference_Assembly_FileName.Attributes().Single(a => a.Name.LocalName == "Value").Value.Replace(@"\\", @"\");


                            string dacpacDllFileName = Path.GetFileName(dacpac.DacPacAssemblyFileName);
                            string dacpacDllFullFileName = Path.Combine(inputdir, dacpacDllFileName);
                            Console.WriteLine("Load '" + dacpacDllFullFileName + "'...");
                            if (!File.Exists(dacpacDllFullFileName))
                            {
                                throw new StoreLakeSdkException("File could not be found:" + dacpacDllFullFileName);
                            }

                            XElement xReference_Assembly_LogicalName = xReference_Assembly.Elements().Single(e => e.Name.LocalName == "Metadata"
                                   && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "LogicalName")
                                   && e.Attributes().Any(a => a.Name.LocalName == "Value")
                                   );
                            dacpac.DacPacAssemblyLogicalName = xReference_Assembly_LogicalName.Attributes().Single(a => a.Name.LocalName == "Value").Value;


                            // <CustomData Category="Reference" Type="SqlSchema">
                            foreach (XElement xReferenceSchema in xHeader.Elements().Where(e => e.Name.LocalName == "CustomData"
                                && e.Attributes().Any(a => a.Name.LocalName == "Category" && a.Value == "Reference")
                                && e.Attributes().Any(a => a.Name.LocalName == "Type" && a.Value == "SqlSchema")
                                ))
                            {
                                var xFileName = xReferenceSchema.Elements().SingleOrDefault(e => e.Name.LocalName == "Metadata"
                                    && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "FileName")
                                    && e.Attributes().Any(a => a.Name.LocalName == "Value")
                                    );
                                var xLogicalName = xReferenceSchema.Elements().SingleOrDefault(e => e.Name.LocalName == "Metadata"
                                    && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "LogicalName")
                                    && e.Attributes().Any(a => a.Name.LocalName == "Value")
                                    );
                                var xExternalParts = xReferenceSchema.Elements().SingleOrDefault(e => e.Name.LocalName == "Metadata"
                                    && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ExternalParts")
                                    && e.Attributes().Any(a => a.Name.LocalName == "Value")
                                    );
                                var xSuppressMissingDependenciesErrors = xReferenceSchema.Elements().SingleOrDefault(e => e.Name.LocalName == "Metadata"
                                    && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "SuppressMissingDependenciesErrors")
                                    && e.Attributes().Any(a => a.Name.LocalName == "Value")
                                    );
                                if (xExternalParts != null)
                                {

                                }
                                else
                                {
                                    string logicalname = xLogicalName.Attributes().Single(a => a.Name.LocalName == "Value").Value;
                                    //Console.WriteLine(logicalname);
                                    string dacpacFileName = xFileName.Attributes().Single(a => a.Name.LocalName == "Value").Value;
                                    //Console.WriteLine(dacpacFileName);
                                    DacPacRegistration external_dacpac;
                                    if (ctx.registered_dacpacs.TryGetValue(logicalname.ToUpperInvariant(), out external_dacpac))
                                    {
                                        // already registered
                                    }
                                    else
                                    {
                                        external_dacpac = RegisterDacpac(outputprefix + "   ", ctx, inputdir, dacpacFileName, true);

                                    }


                                    dacpac.referenced_dacpacs.Add(logicalname.ToUpperInvariant(), external_dacpac);
                                }
                            }

                            if (ctx.registered_dacpacs.ContainsKey(dacpac.UniqueKey))
                            {
                                // registered as a dependency
                            }
                            else
                            {
                                dacpac.DacPacDependencyLevel = CalculateDacPacLevel(dacpac.referenced_dacpacs.Values);

                                Console.WriteLine(outputprefix + "   ===( " + dacpac.DacPacDependencyLevel + " )===> " + dacpac.DacPacAssemblyLogicalName);



                                XElement xModel = xDataSchemaModel.Element(XName.Get("Model", "http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02"));
                                //Console.WriteLine(xModel.Elements().Count());


                                AddTables(ctx, dacpac, xModel);
                                AddPrimaryKeys(ctx.ds, xModel);
                                AddUniqueKeys(ctx.ds, xModel);
                                AddUniqueIndexes(ctx.ds, xModel);
                                AddDefaultConstraints(ctx.ds, xModel);
                                AddForeignKeys(ctx.ds, xModel);
                                AddCheckConstraints(ctx.ds, dacpac, xModel);
                                AddProcedures(ctx.ds, dacpac, xModel);
                                AddInlineTableValuedFunctions(ctx.ds, dacpac, xModel);
                                // <Element Type="SqlMultiStatementTableValuedFunction" Name="[dbo].[hlsyssec_query_agentsystemacl]">
                                // <Element Type="SqlScalarFunction" Name="[dbo].[hlsystablecfgdeskfield_validate_attribute]">

                                ctx.registered_dacpacs.Add(dacpac.UniqueKey, dacpac);
                            }
                        }
                    }
                }
            }

            return dacpac;
        }

        private static int CalculateDacPacLevel(IEnumerable<DacPacRegistration> referenced_dacpacs)
        {
            int level = 0;
            if (referenced_dacpacs.Any())
            {
                level = referenced_dacpacs.Max(x => x.DacPacDependencyLevel);
            }

            level += 1;

            return level;
        }

        private static void CollectRelationParameters(XElement xRelationshipParent, string collectionName, Action<string, string> collector)
        {
            var xRelationship = xRelationshipParent.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == collectionName)).SingleOrDefault();
            if (xRelationship == null)
            {
                // no parameters
            }
            else
            {
                foreach (var xRelationship_Entry in xRelationship.Elements())
                {
                    foreach (var xRelationship_Entry_Element in xRelationship_Entry.Elements().Where(e => e.Name.LocalName == "Element" && e.Attributes().Any(t => t.Name.LocalName == "Type" && t.Value == "SqlSubroutineParameter")))
                    {
                        var xRelationship_Entry_Element_Name = xRelationship_Entry_Element.Attributes().Single(a => a.Name.LocalName == "Name");
                        string[] name_tokens = xRelationship_Entry_Element_Name.Value.Split('.');
                        string parameter_name = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");

                        // <Property Name="IsReadOnly" Value="True" />
                        var xProperty_IsReadOnly = xRelationship_Entry_Element.Elements().FirstOrDefault(e => e.Name.LocalName == "Property" && e.Attributes().Any(t => t.Name.LocalName == "Name" && t.Value == "IsReadOnly"));
                        bool? isReadOnly;
                        if (xProperty_IsReadOnly != null)
                        {
                            var xProperty_IsReadOnly_Value = xProperty_IsReadOnly.Attributes().Single(a => a.Name.LocalName == "Value");
                            isReadOnly = bool.Parse(xProperty_IsReadOnly_Value.Value);
                        }
                        else
                        {
                            isReadOnly = null;
                        }

                        var Type_SqlTypeSpecifier = ReadParameterRelationshipTypeElement(xRelationship_Entry_Element, "Type");

                        var type_name = ReadTypeSpecifierRelationshipReferencesName(Type_SqlTypeSpecifier, "Type");

                        collector(parameter_name, type_name);
                    }
                }
            }
        }

        private static XElement ReadParameterRelationshipTypeElement(XElement xRelationshipParent, string collectionName)
        {
            var xRelationship = xRelationshipParent.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == collectionName)).Single();
            var xRelationship_Entry = xRelationship.Elements().Single();
            {
                var xRelationship_Entry_Element = xRelationship_Entry.Elements().Single(e => e.Name.LocalName == "Element" && e.Attributes().Any(t => t.Name.LocalName == "Type" && (t.Value == "SqlTypeSpecifier" || t.Value == "SqlXmlTypeSpecifier")));
                {
                    return xRelationship_Entry_Element;
                }
            }
        }

        private static string ReadTypeSpecifierRelationshipReferencesName(XElement xRelationshipParent, string collectionName)
        {
            var xRelationship = xRelationshipParent.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == collectionName)).Single();
            var xRelationship_Entry = xRelationship.Elements().Single();
            {
                var xRelationship_Entry_Element = xRelationship_Entry.Elements().Single(e => e.Name.LocalName == "References" && e.Attributes().Any(t => t.Name.LocalName == "Name"));
                {
                    var xaReferences_Name = xRelationship_Entry_Element.Attributes().Single(t => t.Name.LocalName == "Name");

                    string[] name_tokens = xaReferences_Name.Value.Split('.');
                    string references_name = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");

                    return references_name;
                }
            }
        }

        private static void AddProcedures(DataSet ds, DacPacRegistration dacpac, XElement xModel)
        {
            // <Element Type="SqlProcedure" Name="[dbo].[hlbpm_query_cmdbflowattributes]">
            foreach (var xProcedure in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlProcedure")))
            {
                XAttribute xProcedureName = xProcedure.Attributes().Where(x => x.Name == "Name").First();
                string[] name_tokens = xProcedureName.Value.Split('.');
                string procedure_name = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                string schema_name = null;
                if (name_tokens.Length > 1)
                    schema_name = name_tokens[0].Replace("[", "").Replace("]", "");

                StoreLakeProcedureRegistration procedure_reg = new StoreLakeProcedureRegistration();
                procedure_reg.ProcedureSchemaName = schema_name;
                procedure_reg.ProcedureName = procedure_name;
                // BodyScript
                // <Relationship Name="Parameters">
                CollectRelationParameters(xProcedure, "Parameters", (parameter_name, type_name) =>
                {
                    //ck_reg.DefiningColumns.Add(new StoreLakeKeyColumnRegistration { ColumnName = columnName });
                    procedure_reg.Parameters.Add(new StoreLakeParameterRegistration()
                    {
                        ParameterName = parameter_name,
                        ParameterTypeName = type_name
                    });
                });

                dacpac.registered_Procedures.Add(xProcedureName.Value, procedure_reg);
            }
        }

        private static void AddInlineTableValuedFunctions(DataSet ds, DacPacRegistration dacpac, XElement xModel)
        {
            // <Element Type="SqlInlineTableValuedFunction" Name="[dbo].[hlsyssec_query_agentobjectmsk]">
            foreach (var xFunction in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlInlineTableValuedFunction")))
            {
                XAttribute xFunctionName = xFunction.Attributes().Where(x => x.Name == "Name").First();
            }
        }

        private static void AddCheckConstraints(DataSet ds, DacPacRegistration dacpac, XElement xModel)
        {
            //     <Element Type="SqlCheckConstraint" Name="[dbo].[CK_hlcmdocumentstorage_formmgmt]">
            foreach (var xCheckConstraint in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlCheckConstraint")))
            {
                XAttribute xCheckConstraintName = xCheckConstraint.Attributes().Where(x => x.Name == "Name").First();

                StoreLakeCheckConstraintRegistration ck_reg = new StoreLakeCheckConstraintRegistration();

                string[] name_tokens = xCheckConstraintName.Value.Split('.');
                ck_reg.CheckConstraintName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                ck_reg.CheckConstraintSchema = (name_tokens.Length == 3) ? name_tokens[0] : "dbo";

                SqlObjectName defining_table = CollectElementRelationshipTable(xCheckConstraint, "DefiningTable");
                ck_reg.DefiningTableName = defining_table.ObjectName;
                ck_reg.DefiningTableSchema = defining_table.SchemaName;

                CollectRelationReferencesColumns(xCheckConstraint, "CheckExpressionDependencies", (columnName) =>
                {
                    ck_reg.DefiningColumns.Add(new StoreLakeKeyColumnRegistration { ColumnName = columnName });
                });

                var xCheckExpressionScript = xCheckConstraint.Elements().Single(e => e.Name.LocalName == "Property" && e.Attributes().Any(t => t.Name.LocalName == "Name" && t.Value == "CheckExpressionScript"));
                var xCheckExpressionScript_Value = xCheckExpressionScript.Elements().Single(e => e.Name.LocalName == "Value");
                ck_reg.CheckExpressionScript = xCheckExpressionScript_Value.Value;

                dacpac.registered_CheckConstraints.Add(xCheckConstraintName.Value, ck_reg);
                //DatabaseRegistration.RegisterCheckConstraint(ds, ck_reg);
            }
        }
        private static void AddForeignKeys(DataSet ds, XElement xModel)
        {
            //     <Element Type="SqlForeignKeyConstraint" Name="[dbo].[FK_hlcmdatamodelassociationsearch_associationid]">
            foreach (var xForeignKey in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlForeignKeyConstraint")))
            {
                XAttribute xForeignKeyName = xForeignKey.Attributes().Where(x => x.Name == "Name").First();

                StoreLakeForeignKeyRegistration fk_reg = new StoreLakeForeignKeyRegistration();

                string[] name_tokens = xForeignKeyName.Value.Split('.');
                fk_reg.ForeignKeyName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                fk_reg.ForeignKeySchema = (name_tokens.Length == 3) ? name_tokens[0] : "dbo";

                SqlObjectName foreign_table = CollectElementRelationshipTable(xForeignKey, "ForeignTable");
                fk_reg.ForeignTableName = foreign_table.ObjectName;
                fk_reg.ForeignTableSchema = foreign_table.SchemaName;

                SqlObjectName defining_table = CollectElementRelationshipTable(xForeignKey, "DefiningTable");
                fk_reg.DefiningTableName = defining_table.ObjectName;
                fk_reg.DefiningTableSchema = defining_table.SchemaName;

                CollectRelationReferencesColumns(xForeignKey, "Columns", (columnName) =>
                {
                    fk_reg.DefiningColumns.Add(new StoreLakeKeyColumnRegistration { ColumnName = columnName });
                });
                CollectRelationReferencesColumns(xForeignKey, "ForeignColumns", (columnName) =>
                {
                    fk_reg.ForeignColumns.Add(new StoreLakeKeyColumnRegistration { ColumnName = columnName });
                });

                DatabaseRegistration.RegisterForeignKey(ds, fk_reg);
            }
        }

        private static SqlObjectName CollectElementRelationshipTable(XElement xForeignKey, string relationshipItemName)
        {
            var xDefiningTable = xForeignKey.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == relationshipItemName));
            var xDefiningTable_Entry = xDefiningTable.Elements().Single(e => e.Name.LocalName == "Entry");
            var xDefiningTable_Entry_References = xDefiningTable_Entry.Elements().Single(e => e.Name.LocalName == "References");
            var xDefiningTable_Entry_References_Name = xDefiningTable_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
            string[] name_tokens = xDefiningTable_Entry_References_Name.Value.Split('.');
            return new SqlObjectName()
            {
                ObjectName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", ""),
                SchemaName = (name_tokens.Length == 2) ? name_tokens[0] : "dbo"
            };
        }

        class SqlObjectName
        {
            public string SchemaName;
            public string ObjectName;
        }
        private static void AddDefaultConstraints(DataSet ds, XElement xModel)
        {
            //     <Element Type="SqlDefaultConstraint" Name="[dbo].[DF_hlsysagent_active]">
            foreach (var xDefaultConstraint in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlDefaultConstraint")))
            {
                XAttribute xDefaultConstraintName = xDefaultConstraint.Attributes().Where(x => x.Name == "Name").First();

                string[] name_tokens = xDefaultConstraintName.Value.Split('.');

                bool skip_df = false;
                StoreLakeDefaultContraintRegistration df_reg = new StoreLakeDefaultContraintRegistration()
                {
                    ConstraintName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", ""),
                    ConstraintSchema = (name_tokens.Length == 2) ? name_tokens[0] : "dbo",
                };

                var xForColumn = xDefaultConstraint.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ForColumn"));
                var xForColumn_Entry = xForColumn.Elements().Single(e => e.Name.LocalName == "Entry");
                var xForColumn_Entry_References = xForColumn_Entry.Elements().Single(e => e.Name.LocalName == "References");
                var xForColumn_Entry_References_Name = xForColumn_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                name_tokens = xForColumn_Entry_References_Name.Value.Split('.');
                df_reg.ColumnName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");

                SqlObjectName defining_table = CollectElementRelationshipTable(xDefaultConstraint, "DefiningTable");
                df_reg.TableName = defining_table.ObjectName;
                df_reg.TableSchema = defining_table.SchemaName;


                // (180 /* Open */)
                var xDefaultExpressionScript = xDefaultConstraint.Elements().Single(e => e.Name.LocalName == "Property" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "DefaultExpressionScript"));
                var xDefaultExpressionScript_Value = xDefaultExpressionScript.Elements().Single(e => e.Name.LocalName == "Value");
                string defaultExpressionScript_Value = xDefaultExpressionScript_Value.Value;

                string defaultExpressionScript_Value_NoParentheses = defaultExpressionScript_Value.Replace("(", "").Replace(")", "");
                defaultExpressionScript_Value_NoParentheses = RemoveCommentsFromExpressionText(defaultExpressionScript_Value_NoParentheses);
                defaultExpressionScript_Value_NoParentheses = defaultExpressionScript_Value_NoParentheses.Trim();
                int scalarValueInt32;
                decimal scalarValueDecimal;
                if (int.TryParse(defaultExpressionScript_Value_NoParentheses, out scalarValueInt32))
                {
                    df_reg.IsScalarValue = true;
                    df_reg.ValueInt32 = scalarValueInt32;
                }
                else if (decimal.TryParse(defaultExpressionScript_Value_NoParentheses, out scalarValueDecimal))
                {
                    df_reg.IsScalarValue = true;
                    df_reg.ValueDecimal = scalarValueDecimal;
                }
                else if (string.Equals(defaultExpressionScript_Value_NoParentheses, "NULL", StringComparison.OrdinalIgnoreCase))
                {
                    skip_df = true;
                }
                else if (string.Equals(defaultExpressionScript_Value_NoParentheses, "GETUTCDATE", StringComparison.OrdinalIgnoreCase))
                {
                    df_reg.IsBuiltInFunctionExpression = true;
                    df_reg.DefaultExpressionScript = "GETUTCDATE";
                }
                else if (string.Equals(defaultExpressionScript_Value_NoParentheses, "HOST_NAME", StringComparison.OrdinalIgnoreCase))
                {
                    df_reg.IsBuiltInFunctionExpression = true;
                    df_reg.DefaultExpressionScript = "HOST_NAME";
                }
                else if (string.Equals(defaultExpressionScript_Value_NoParentheses, "@@SPID", StringComparison.OrdinalIgnoreCase))
                {
                    df_reg.IsBuiltInFunctionExpression = true;
                    df_reg.DefaultExpressionScript = "@@SPID";
                }
                else if (defaultExpressionScript_Value_NoParentheses != null && defaultExpressionScript_Value_NoParentheses.StartsWith("N'") && defaultExpressionScript_Value_NoParentheses.EndsWith("'"))
                {
                    defaultExpressionScript_Value_NoParentheses = defaultExpressionScript_Value_NoParentheses.Remove(0, 2);
                    defaultExpressionScript_Value_NoParentheses = defaultExpressionScript_Value_NoParentheses.Remove(defaultExpressionScript_Value_NoParentheses.Length - 1, 1);
                    df_reg.IsScalarValue = true;
                    df_reg.ValueString = defaultExpressionScript_Value_NoParentheses;
                }
                else if (defaultExpressionScript_Value_NoParentheses != null && defaultExpressionScript_Value_NoParentheses.StartsWith("DATETIMEFROMPARTS"))
                {
                    df_reg.IsScalarValue = true;
                    df_reg.ValueDateTime = Parse_DATETIMEFROMPARTS(defaultExpressionScript_Value);
                }
                else if (string.Equals(defaultExpressionScript_Value, "CAST(N'' AS VARBINARY(MAX))", StringComparison.OrdinalIgnoreCase))
                {
                    // empty byte array
                    df_reg.IsScalarValue = true;
                    df_reg.ValueBytes = new byte[0];
                }
                else
                {
                    throw new StoreLakeSdkException("Oops [" + df_reg.ConstraintName + "] " + defaultExpressionScript_Value + "");
                }



                if (skip_df)
                {

                }
                else
                {
                    DatabaseRegistration.RegisterDefaultConstraint(ds, df_reg);
                }
            }
        }

        private static DateTime Parse_DATETIMEFROMPARTS(string input)
        {
            // (DATETIMEFROMPARTS((1900),(1),(1),(0),(0),(0),(0)))
            string text = input;
            if (text.StartsWith("(DATETIMEFROMPARTS")) // Parenthesis?
            {
                text = text.Substring(1, text.Length - 2);
            }

            // DATETIMEFROMPARTS : length:17
            text = text.Remove(0, 17).Trim();
            text = text.Remove(0, 1); // function parameter list open '('
            text = text.Remove(text.Length - 1, 1); // function parameter list close ')'
            text = text.Replace('(', ' ').Replace(')', ' ');
            string[] tokens = text.Split(',');
            int yea = int.Parse(tokens[0].Trim());
            int mon = int.Parse(tokens[1].Trim());
            int day = int.Parse(tokens[2].Trim());
            int hou = int.Parse(tokens[3].Trim());
            int min = int.Parse(tokens[4].Trim());
            int sec = int.Parse(tokens[5].Trim());


            DateTime dt = new DateTime(yea, mon, day, hou, min, sec, 0);

            return dt;
        }

        private static string RemoveCommentsFromExpressionText(string input)
        {
            if (input == null)
                return null;
            if (input.Length < 4) // /**/
                return input;
            string output = input;
            int start_ix;
            do
            {
                start_ix = output.IndexOf("/*");
                if (start_ix >= 0)
                {
                    int last_ix = output.IndexOf("*/", start_ix);
                    if (last_ix < 0)
                    {
                        last_ix = output.Length - 1; // no end
                    }
                    output = output.Remove(start_ix, last_ix - start_ix + 2);
                }
            } while (start_ix >= 0);
            return output;
        }
        private static void AddUniqueIndexes(DataSet ds, XElement xModel)
        {
            foreach (var xIndex in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlIndex")))
            {
                // <Property Name="IsUnique" Value="True" />
                var xIndex_Property_IsUnique = xIndex.Elements().SingleOrDefault(e => e.Name.LocalName == "Property" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "IsUnique"));
                if (xIndex_Property_IsUnique != null)
                {
                    bool isUnique = string.Equals("True", xIndex_Property_IsUnique.Attributes().Single(a => a.Name.LocalName == "Value").Value, StringComparison.OrdinalIgnoreCase);
                    if (isUnique)
                    {
                        StoreLakeTableKeyRegistration uqreg = new StoreLakeTableKeyRegistration();

                        var xIndex_Name = xIndex.Attributes().Single(a => a.Name.LocalName == "Name");
                        string[] name_tokens = xIndex_Name.Value.Replace("[", "").Replace("]", "").Split('.');
                        uqreg.KeyName = name_tokens[name_tokens.Length - 1];
                        uqreg.KeySchema = (name_tokens.Length == 3) ? name_tokens[0] : "dbo";

                        // HasFilter => skip
                        // <Property Name="FilterPredicate">
                        var xIndex_Property_FilterPredicate = xIndex.Elements().SingleOrDefault(e => e.Name.LocalName == "Property" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "FilterPredicate"));
                        if (xIndex_Property_FilterPredicate != null)
                        {
                            // skip for now filtered indices
                            //Console.WriteLine("SKIP:" + uqreg.KeyName);
                        }
                        else
                        {
                            var xIndexedObject = xIndex.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "IndexedObject"));
                            var xIndexedObject_Entry = xIndexedObject.Elements().Single(e => e.Name.LocalName == "Entry");
                            var xIndexedObject_Entry_References = xIndexedObject_Entry.Elements().Single(e => e.Name.LocalName == "References");
                            var xIndexedObject_Entry_References_Name = xIndexedObject_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                            name_tokens = xIndexedObject_Entry_References_Name.Value.Split('.');
                            uqreg.TableName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                            uqreg.TableSchema = (name_tokens.Length == 2) ? name_tokens[0] : "dbo";

                            var xColumnSpecifications = xIndex.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ColumnSpecifications"));
                            foreach (var xColumnSpecifications_Entry in xColumnSpecifications.Elements().Where(e => e.Name.LocalName == "Entry"))
                            {
                                var xSqlIndexedColumnSpecification = xColumnSpecifications_Entry.Elements().Single(e => e.Name.LocalName == "Element");
                                var xColumn = xSqlIndexedColumnSpecification.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Column"));

                                var xColumn_Entry = xColumn.Elements().Single(e => e.Name.LocalName == "Entry");
                                var xColumn_Entry_References = xColumn_Entry.Elements().Single(e => e.Name.LocalName == "References");
                                var xColumn_Entry_References_Name = xColumn_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");

                                name_tokens = xColumn_Entry_References_Name.Value.Split('.');

                                StoreLakeKeyColumnRegistration pkcol_reg = new StoreLakeKeyColumnRegistration()
                                {
                                    ColumnName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "")
                                };

                                uqreg.Columns.Add(pkcol_reg);
                            }

                            DatabaseRegistration.RegisterUniqueKey(ds, uqreg);
                        }
                    }
                }
            }
        }
        private static void AddUniqueKeys(DataSet ds, XElement xModel)
        {
            // <Element Type="SqlUniqueConstraint" Name="[dbo].[UQ_hlsysagent_name]">
            foreach (var xUniqueConstraint in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlUniqueConstraint")))
            {
                // <Property Name="IsUnique" Value="True" />
                //var xUniqueConstraint_Property_IsUnique = xUniqueConstraint.Elements().SingleOrDefault(e => e.Name.LocalName == "Property" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "IsUnique"));
                //if (xUniqueConstraint_Property_IsUnique != null)
                {
                    //bool isUnique = string.Equals("True", xUniqueConstraint_Property_IsUnique.Attributes().Single(a => a.Name.LocalName == "Value").Value, StringComparison.OrdinalIgnoreCase);
                    //if (isUnique)
                    {
                        StoreLakeTableKeyRegistration uqreg = new StoreLakeTableKeyRegistration();

                        var xUniqueConstraint_Name = xUniqueConstraint.Attributes().Single(a => a.Name.LocalName == "Name");
                        string[] name_tokens = xUniqueConstraint_Name.Value.Replace("[", "").Replace("]", "").Split('.');
                        uqreg.KeyName = name_tokens[name_tokens.Length - 1];
                        uqreg.KeySchema = (name_tokens.Length == 3) ? name_tokens[0] : "dbo";

                        // HasFilter => skip
                        // <Property Name="FilterPredicate">
                        //var xUniqueConstraint_Property_FilterPredicate = xUniqueConstraint.Elements().SingleOrDefault(e => e.Name.LocalName == "Property" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "FilterPredicate"));
                        //if (xUniqueConstraint_Property_FilterPredicate != null)
                        //{
                        //    // skip for now filtered indices
                        //    Console.WriteLine("SKIP:" + uqreg.KeyName);
                        //}
                        //else
                        {
                            var xDefiningTable = xUniqueConstraint.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "DefiningTable"));
                            var xDefiningTable_Entry = xDefiningTable.Elements().Single(e => e.Name.LocalName == "Entry");
                            var xDefiningTable_Entry_References = xDefiningTable_Entry.Elements().Single(e => e.Name.LocalName == "References");
                            var xDefiningTable_Entry_References_Name = xDefiningTable_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                            name_tokens = xDefiningTable_Entry_References_Name.Value.Split('.');
                            uqreg.TableName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                            uqreg.TableSchema = (name_tokens.Length == 2) ? name_tokens[0] : "dbo";

                            var xColumnSpecifications = xUniqueConstraint.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ColumnSpecifications"));
                            foreach (var xColumnSpecifications_Entry in xColumnSpecifications.Elements().Where(e => e.Name.LocalName == "Entry"))
                            {
                                var xSqlIndexedColumnSpecification = xColumnSpecifications_Entry.Elements().Single(e => e.Name.LocalName == "Element");
                                var xColumn = xSqlIndexedColumnSpecification.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Column"));

                                var xColumn_Entry = xColumn.Elements().Single(e => e.Name.LocalName == "Entry");
                                var xColumn_Entry_References = xColumn_Entry.Elements().Single(e => e.Name.LocalName == "References");
                                var xColumn_Entry_References_Name = xColumn_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");

                                name_tokens = xColumn_Entry_References_Name.Value.Split('.');

                                StoreLakeKeyColumnRegistration pkcol_reg = new StoreLakeKeyColumnRegistration()
                                {
                                    ColumnName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "")
                                };

                                uqreg.Columns.Add(pkcol_reg);
                            }

                            DatabaseRegistration.RegisterUniqueKey(ds, uqreg);
                        }
                    }
                }
            }
        }
        private static void AddPrimaryKeys(DataSet ds, XElement xModel)
        {
            // <Element Type="SqlPrimaryKeyConstraint" Name="[dbo].[PK_hlsysholidaydate]">
            foreach (var xPrimaryKey in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlPrimaryKeyConstraint")))
            {
                StoreLakeTableKeyRegistration pkreg = new StoreLakeTableKeyRegistration();

                var xPrimaryKey_Name = xPrimaryKey.Attributes().Single(a => a.Name.LocalName == "Name");
                string[] name_tokens = xPrimaryKey_Name.Value.Split('.');
                pkreg.KeyName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                pkreg.KeySchema = (name_tokens.Length == 2) ? name_tokens[0] : "dbo";


                var xDefiningTable = xPrimaryKey.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "DefiningTable"));
                var xDefiningTable_Entry = xDefiningTable.Elements().Single(e => e.Name.LocalName == "Entry");
                var xDefiningTable_Entry_References = xDefiningTable_Entry.Elements().Single(e => e.Name.LocalName == "References");
                var xDefiningTable_Entry_References_Name = xDefiningTable_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");

                name_tokens = xDefiningTable_Entry_References_Name.Value.Split('.');


                pkreg.TableName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                pkreg.TableSchema = (name_tokens.Length == 2) ? name_tokens[0] : "dbo";

                var xColumnSpecifications = xPrimaryKey.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ColumnSpecifications"));
                foreach (var xColumnSpecifications_Entry in xColumnSpecifications.Elements().Where(e => e.Name.LocalName == "Entry"))
                {
                    var xSqlIndexedColumnSpecification = xColumnSpecifications_Entry.Elements().Single(e => e.Name.LocalName == "Element");
                    var xColumn = xSqlIndexedColumnSpecification.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Column"));

                    var xColumn_Entry = xColumn.Elements().Single(e => e.Name.LocalName == "Entry");
                    var xColumn_Entry_References = xColumn_Entry.Elements().Single(e => e.Name.LocalName == "References");
                    var xColumn_Entry_References_Name = xColumn_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");

                    name_tokens = xColumn_Entry_References_Name.Value.Split('.');

                    StoreLakeKeyColumnRegistration pkcol_reg = new StoreLakeKeyColumnRegistration()
                    {
                        ColumnName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "")
                    };

                    pkreg.Columns.Add(pkcol_reg);
                }

                //Console.WriteLine(pkreg.KeyName);
                DatabaseRegistration.RegisterPrimaryKey(ds, pkreg);
            }
        }

        private static void AddTables(RegistrationResult ctx, DacPacRegistration dacpac, XElement xModel)
        {
            //     <Element Type="SqlTable" Name="[dbo].[hlsysagent]" Disambiguator="8">
            foreach (var xTable in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlTable")))
            {
                XAttribute xTableName = xTable.Attributes().Where(x => x.Name == "Name").First();
                //foreach (var xElementType2 in xx.Attributes())
                {

                    //Console.WriteLine(xTableName.Value);
                }
                var xRelationship_Schema = xTable.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Schema")).First();
                var xRelationship_Columns = xTable.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Columns")).First();

                string[] name_tokens = xTableName.Value.Split('.');
                string table_name = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                //if (string.Equals(tableName, table_name, StringComparison.OrdinalIgnoreCase))
                {
                    //Console.WriteLine(table_name);
                    StoreLakeTableRegistration treg = new StoreLakeTableRegistration()
                    {
                        TableName = table_name,
                        TableSchema = (name_tokens.Length == 2) ? name_tokens[0] : "dbo",
                    };

                    CollectTableColumns(xRelationship_Columns, treg, false);
                    CollectTableColumns(xRelationship_Columns, treg, true);

                    ctx.registered_tables.Add(treg.TableName, dacpac);
                    dacpac.registered_tables.Add(treg.TableName, true);
                    DatabaseRegistration.RegisterTable(ctx.ds, treg);
                }
            }
        }

        private static void CollectRelationReferencesColumns(XElement xRelationship, string collectionName, Action<string> collector)
        {
            var xRelationship_Columns = xRelationship.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == collectionName)).First();
            foreach (var xRelationship_Columns_Entry in xRelationship_Columns.Elements())
            {
                var xElement_References = xRelationship_Columns_Entry.Elements().Single(e => e.Name.LocalName == "References");
                {
                    var xColumnName = xElement_References.Attributes().Single(a => a.Name.LocalName == "Name");
                    string[] name_tokens = xColumnName.Value.Split('.');
                    string column_name = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                    collector(column_name);
                }
            }
        }

        private static void CollectTableColumns(XElement xRelationship_Columns, StoreLakeTableRegistration treg, bool processComputed)
        {
            foreach (var xRelationship_Columns_Entry in xRelationship_Columns.Elements())
            {
                var xElement_Column = xRelationship_Columns_Entry.Elements().Single(e => e.Name.LocalName == "Element" && e.Attributes().Any(a => a.Name.LocalName == "Type"));
                var xElement_Column_Type = xElement_Column.Attributes().Single(a => a.Name.LocalName == "Type");
                bool isComputedColumn = string.Equals(xElement_Column_Type.Value, "SqlComputedColumn", StringComparison.Ordinal); // && a.Value == "SqlSimpleColumn" SqlComputedColumn
                if ((isComputedColumn && processComputed) || (!isComputedColumn && !processComputed))
                {
                    var xColumnName = xElement_Column.Attributes().Single(a => a.Name.LocalName == "Name");
                    string[] name_tokens = xColumnName.Value.Split('.');
                    string column_name = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");

                    StoreLakeColumnRegistration creg = new StoreLakeColumnRegistration()
                    {
                        ColumnName = column_name
                    };

                    var xIsNullable = xElement_Column.Elements().FirstOrDefault(e => e.Name.LocalName == "Property" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "IsNullable"));
                    if (xIsNullable != null)
                    {
                        var xValue = xIsNullable.Attributes().Single(a => a.Name.LocalName == "Value");
                        creg.IsNullable = string.Equals(xValue.Value, "False", StringComparison.OrdinalIgnoreCase) ? false : XmlConvert.ToBoolean(xValue.Value);
                    }
                    else
                    {
                        creg.IsNullable = true;
                    }

                    // <Property Name="IsIdentity" Value="True" />
                    var xIsIdentity = xElement_Column.Elements().FirstOrDefault(e => e.Name.LocalName == "Property" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "IsIdentity"));
                    if (xIsIdentity != null)
                    {
                        var xValue = xIsIdentity.Attributes().Single(a => a.Name.LocalName == "Value");
                        creg.IsIdentity = string.Equals(xValue.Value, "True", StringComparison.OrdinalIgnoreCase);
                        if (creg.IsIdentity)
                        {
                            // [hlcmhypermedialinks](id)
                        }
                    }
                    else
                    {
                        creg.IsIdentity = false;
                    }

                    if (isComputedColumn)
                    {
                        // <Element Type="SqlTable" Name="[dbo].[hlsysholidaydate]"
                        // <Element Type="SqlSimpleColumn" Name="[dbo].[hlsysholidaydate].[holidayid]">
                        // <Element Type="SqlComputedColumn" Name="[dbo].[hlsysuserlanguage].[parentlcid]"

                        var xExpressionScript = xElement_Column.Elements().Single(e => e.Name.LocalName == "Property" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ExpressionScript"));
                        var xExpressionScript_Value = xExpressionScript.Elements().Single(e => e.Name.LocalName == "Value");
                        string expressionScript_Value = xExpressionScript_Value.Value;

                        List<string> ExpressionDependencies_ColumnNames = new List<string>();
                        var xExpressionDependencies = xElement_Column.Elements().SingleOrDefault(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ExpressionDependencies"));
                        if (xExpressionDependencies == null)
                        {
                            // constant (no dependency on other column)
                            int scalarValueInt32;
                            if (int.TryParse(expressionScript_Value.Replace("(", "").Replace(")", ""), out scalarValueInt32))
                            {
                                creg.ColumnDbType = System.Data.SqlDbType.Int;
                            }
                            else
                            {
                                throw new StoreLakeSdkException("Oops [" + treg.TableName + "] COMPUTED [" + creg.ColumnName + "] AS " + expressionScript_Value + "");
                            }
                        }
                        else
                        {
                            foreach (var xExpressionDependencies_Entry in xExpressionDependencies.Elements().Where(x => x.Name.LocalName == "Entry"))
                            {
                                var xExpressionDependencies_Entry_References = xExpressionDependencies_Entry.Elements().Single(x => x.Name.LocalName == "References");
                                var xDependencyColumnName = xExpressionDependencies_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                                name_tokens = xDependencyColumnName.Value.Split('.');
                                string dependecy_column_name = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                                ExpressionDependencies_ColumnNames.Add(dependecy_column_name);

                                creg.ColumnDbType = treg.Columns.Single(x => x.ColumnName == dependecy_column_name).ColumnDbType;
                            }
                        }
                        //Console.WriteLine("Skip [" + treg.TableName + "] COMPUTED [" + creg.ColumnName + "] AS [" + string.Join(",", ExpressionDependencies_ColumnNames.ToArray()) + "]"); ;
                    }
                    else
                    {
                        var xTypeSpecifier = xElement_Column.Elements().FirstOrDefault(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "TypeSpecifier"));
                        var xTypeSpecifier_Entry = xTypeSpecifier.Elements().Single(x => x.Name.LocalName == "Entry");
                        var xTypeSpecifier_Entry_Element = xTypeSpecifier_Entry.Elements().Single(x => x.Name.LocalName == "Element");
                        var xTypeSpecifier_Entry_Element_Relationship = xTypeSpecifier_Entry_Element.Elements().Single(x => x.Name.LocalName == "Relationship");
                        var xTypeSpecifier_Entry_Element_Relationship_Entry = xTypeSpecifier_Entry_Element_Relationship.Elements().Single(x => x.Name.LocalName == "Entry");
                        var xTypeSpecifier_Entry_Element_Relationship_Entry_References = xTypeSpecifier_Entry_Element_Relationship_Entry.Elements().Single(x => x.Name.LocalName == "References");
                        var xColumnTypeName = xTypeSpecifier_Entry_Element_Relationship_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                        if (string.Equals(xColumnTypeName.Value, "[int]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Int;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[bigint]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.BigInt;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[nvarchar]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.NVarChar;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[nchar]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.NChar;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[sys].[sysname]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.NVarChar;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[smallint]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.SmallInt;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[tinyint]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.TinyInt;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[bit]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Bit;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[datetime]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.DateTime;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[date]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Date;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[time]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Time;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[xml]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Xml;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[uniqueidentifier]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.UniqueIdentifier;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[decimal]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Decimal;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[float]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Float;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[varbinary]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.VarBinary;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[binary]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Binary;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[image]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Image;
                        }
                        else if (string.Equals(xColumnTypeName.Value, "[rowversion]", StringComparison.Ordinal))
                        {
                            creg.ColumnDbType = System.Data.SqlDbType.Timestamp;
                        }
                        else
                        {
                            throw new StoreLakeSdkException("Column type table [" + treg.TableName + "] column [" + creg.ColumnName + "] type (" + xColumnTypeName.Value + ")");
                        }
                        //foreach (var e in xElement_Column.Elements())
                        //{
                        //    Console.WriteLine("<" + e.Name.LocalName);
                        //    foreach (var a in e.Attributes())
                        //    {
                        //        Console.WriteLine("   " + a.Name.LocalName + "=" + a.Value);
                        //    }
                        //}
                    }

                    treg.Columns.Add(creg);
                }// not a computedcolumn
            }
        }
    }

    public sealed class StoreLakeSdkException : Exception
    {
        public StoreLakeSdkException(string message) : base(message)
        {

        }
    }
}

