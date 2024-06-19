using Microsoft.SqlServer.TransactSql.ScriptDom;
using StoreLake.Sdk.SqlDom;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

        internal readonly IDictionary<string, DacPacRegistration> referenced_dacpacs = new SortedDictionary<string, DacPacRegistration>(StringComparer.OrdinalIgnoreCase); // <logicalname, dacpac.filename>
        internal readonly IDictionary<string, bool> registered_tables = new SortedDictionary<string, bool>(); // < ;
        internal readonly IDictionary<string, StoreLakeTableTypeRegistration> registered_tabletypes = new SortedDictionary<string, StoreLakeTableTypeRegistration>(); // < ;
        internal readonly IDictionary<string, string> referenced_assemblies = new SortedDictionary<string, string>(); // assemblyname/assemblylocation;
        internal readonly IDictionary<string, StoreLakeCheckConstraintRegistration> registered_CheckConstraints = new SortedDictionary<string, StoreLakeCheckConstraintRegistration>();
        internal readonly IDictionary<string, StoreLakeProcedureRegistration> registered_Procedures = new SortedDictionary<string, StoreLakeProcedureRegistration>();
        internal readonly IDictionary<string, StoreLakeViewRegistration> registered_views = new SortedDictionary<string, StoreLakeViewRegistration>();
        internal readonly IDictionary<string, StoreLakeTableValuedFunctionRegistration> registered_TableValuedFunctions = new SortedDictionary<string, StoreLakeTableValuedFunctionRegistration>();
    }


    public sealed class RegistrationResult : SqlDom.ISchemaMetadataProvider
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
        internal readonly IDictionary<string, DacPacRegistration> registered_dacpacs = new SortedDictionary<string, DacPacRegistration>(StringComparer.OrdinalIgnoreCase); // <logicalname, dacpac.filename>
        internal readonly IDictionary<string, DacPacRegistration> procesed_files = new SortedDictionary<string, DacPacRegistration>(StringComparer.OrdinalIgnoreCase); // <logicalname, dacpac.filename>
        internal readonly IDictionary<string, DacPacRegistration> registered_tables = new SortedDictionary<string, DacPacRegistration>(StringComparer.OrdinalIgnoreCase); // <tablename, dacpac.logicalname>
        internal readonly IDictionary<string, DacPacRegistration> registered_tabletypes = new SortedDictionary<string, DacPacRegistration>(StringComparer.OrdinalIgnoreCase); // <tablename, dacpac.logicalname>
        internal readonly IDictionary<string, DacPacRegistration> registered_views = new SortedDictionary<string, DacPacRegistration>(StringComparer.OrdinalIgnoreCase); // <tablename, dacpac.logicalname>
        internal readonly IDictionary<string, DacPacRegistration> registered_functions = new SortedDictionary<string, DacPacRegistration>(StringComparer.OrdinalIgnoreCase); // <tablename, dacpac.logicalname>
        // context
        internal readonly IDictionary<string, TableTypeRow> udt_rows = new SortedDictionary<string, TableTypeRow>();

        internal readonly bool DoResolveColumnTypes = true;
        internal ISchemaMetadataProvider SchemaMetadata()
        {
            return this;
        }

        internal readonly IDictionary<string, IColumnSourceMetadata> column_sources = PrepareColumnSources(new SortedDictionary<string, IColumnSourceMetadata>(StringComparer.OrdinalIgnoreCase));
        internal readonly IDictionary<string, IColumnSourceMetadata> function_sources = new SortedDictionary<string, IColumnSourceMetadata>(StringComparer.OrdinalIgnoreCase);
        internal readonly IDictionary<string, IColumnSourceMetadata> udt_sources = new SortedDictionary<string, IColumnSourceMetadata>(StringComparer.OrdinalIgnoreCase);

        private static IDictionary<string, IColumnSourceMetadata> PrepareColumnSources(IDictionary<string, IColumnSourceMetadata> sources)
        {
            AddKnownColumnSource(sources
                    , new SystemTableSource("sys.tables")
                                .AddColumn("name", DbType.String, false)
                                .AddColumn("object_id", DbType.Int32, false)
                        );
            AddKnownColumnSource(sources
                    , new SystemTableSource("sys.columns")
                                            .AddColumn("object_id", DbType.Int32, false)
                                            .AddColumn("name", DbType.String, false)
                                            .AddColumn("max_length", DbType.Int16, true)
                                            .AddColumn("precision", DbType.Byte, true)
                                            .AddColumn("user_type_id", DbType.Int32, false)
                                    );
            return AddKnownColumnSource(sources
                    , new SystemTableSource("sys.systypes")
                                            .AddColumn("xtype", DbType.Byte, false)
                                            .AddColumn("name", DbType.String, false)
                                    );
        }
        private static IDictionary<string, IColumnSourceMetadata> AddKnownColumnSource(IDictionary<string, IColumnSourceMetadata> sources, SystemTableSource source)
        {
            sources.Add(source.Fullname, source);
            return sources;
        }

        IColumnSourceMetadata ISchemaMetadataProvider.TryGetColumnSourceMetadata(string schemaName, string objectName)
        {
            string schemaNameSafe = string.IsNullOrEmpty(schemaName) ? ds.Namespace : schemaName;
            if (!string.Equals(schemaNameSafe, ds.Namespace, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(schemaName, "sys", StringComparison.OrdinalIgnoreCase))
                {
                    //return s_sys_tables;
                }
                else
                {
                    return null;
                }
            }

            string fullName = schemaNameSafe + "." + objectName;
            if (!column_sources.TryGetValue(fullName, out IColumnSourceMetadata column_source))
            {
                DataTable table;
                if (!registered_tables.TryGetValue(objectName, out DacPacRegistration dacpac))
                {
                    if (!registered_tables.TryGetValue(objectName, out dacpac))
                    {
                        StoreLakeViewRegistration view_reg;
                        if (!registered_views.TryGetValue(objectName, out dacpac))
                        {
                            return null; // table/view unknown
                        }
                        else
                        {
                            view_reg = dacpac.registered_views[objectName];
                        }
                        column_source = new SourceMetadataView(SchemaMetadata(), view_reg);
                    }
                    else
                    {
                        table = ds.Tables[objectName];
                        if (table == null)
                            throw new NotSupportedException("Table could not be found:" + "[" + schemaNameSafe + "].[" + objectName + "]");
                        column_source = new SourceMetadataTable(table);
                    }
                }
                else
                {
                    table = ds.Tables[objectName];
                    if (table == null)
                        throw new NotSupportedException("Table could not be found:" + "[" + schemaNameSafe + "].[" + objectName + "]");
                    column_source = new SourceMetadataTable(table);
                }

                column_sources.Add(fullName, column_source);
            }

            return column_source;
        }

        private sealed class SystemTableSource : IColumnSourceMetadata
        {
            private readonly IDictionary<string, OutputColumnDescriptor> output_columns = new SortedDictionary<string, OutputColumnDescriptor>(StringComparer.OrdinalIgnoreCase);

            internal readonly string Fullname;
            public SystemTableSource(string fullname)
            {
                Fullname = fullname;
            }
            public SystemTableSource AddColumn(string name, DbType columnDbType, bool allowNull)
            {
                output_columns.Add(name, new OutputColumnDescriptor(name, new ColumnTypeMetadata(columnDbType, allowNull)));
                return this;
            }
            ColumnTypeMetadata IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
            {
                if (output_columns.TryGetValue(columnName, out OutputColumnDescriptor coldesc))
                {
                    return coldesc.ColumnType;
                }

                return null;
            }
        }

        IColumnSourceMetadata ISchemaMetadataProvider.TryGetFunctionTableMetadata(string schemaName, string objectName)
        {
            string schemaNameSafe = string.IsNullOrEmpty(schemaName) ? ds.Namespace : schemaName;
            if (!string.Equals(schemaNameSafe, ds.Namespace, StringComparison.OrdinalIgnoreCase))
                return null;

            string fullName = schemaNameSafe + "." + objectName;
            if (!function_sources.TryGetValue(fullName, out IColumnSourceMetadata column_source))
            {
                if (!registered_functions.TryGetValue(objectName, out DacPacRegistration dacpac))
                {
                    return null; // function unknown
                }
                else
                {
                    if (!dacpac.registered_TableValuedFunctions.TryGetValue(objectName, out StoreLakeTableValuedFunctionRegistration function_reg))
                        throw new NotSupportedException("Function could not be found:" + "[" + schemaNameSafe + "].[" + objectName + "]");
                    column_source = new SourceMetadataFunction(SchemaMetadata(), function_reg);
                }

                function_sources.Add(fullName, column_source);
            }

            return column_source;
        }

        IColumnSourceMetadata ISchemaMetadataProvider.TryGetUserDefinedTableTypeMetadata(string schemaName, string objectName)
        {
            string schemaNameSafe = string.IsNullOrEmpty(schemaName) ? ds.Namespace : schemaName;
            if (!string.Equals(schemaNameSafe, ds.Namespace, StringComparison.OrdinalIgnoreCase))
                return null;

            string fullName = "[" + schemaNameSafe + "].[" + objectName + "]";
            if (!udt_sources.TryGetValue(fullName, out IColumnSourceMetadata column_source))
            {
                if (!registered_tabletypes.TryGetValue(fullName, out DacPacRegistration dacpac))
                {
                    return null; // function unknown
                }
                else
                {
                    if (!dacpac.registered_tabletypes.TryGetValue(fullName, out StoreLakeTableTypeRegistration function_reg))
                        throw new NotSupportedException("Function could not be found:" + "[" + schemaNameSafe + "].[" + objectName + "]");
                    column_source = new SourceMetadataUDT(function_reg);
                }

                udt_sources.Add(fullName, column_source);
            }

            return column_source;
        }
    }

    [DebuggerDisplay("{table.TableName}")]
    class SourceMetadataUDT : IColumnSourceMetadata
    {
        private readonly StoreLakeTableTypeRegistration udt_reg;
        public SourceMetadataUDT(StoreLakeTableTypeRegistration udt_reg)
        {
            this.udt_reg = udt_reg;
        }
        ColumnTypeMetadata IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
        {
            StoreLakeColumnRegistration column = udt_reg.Columns.FirstOrDefault(x => x.ColumnName == columnName);
            if (column != null)
            {
                var prmType = TypeMap.GetParameterClrType(column.ColumnDbType, "?");
                if (prmType.IsUserDefinedTableType || prmType.ParameterDbType == DbType.Object)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    return new ColumnTypeMetadata(prmType.ParameterDbType, column.IsNullable);
                }
            }
            return null;
        }
    }

    [DebuggerDisplay("{FunctionName}")]
    class SourceMetadataFunction : IColumnSourceMetadata
    {
        private readonly StoreLakeTableValuedFunctionRegistration function_reg;
        private readonly string FunctionName;
        private readonly ISchemaMetadataProvider SchemaMetadata;
        public SourceMetadataFunction(ISchemaMetadataProvider schemaMetadata, StoreLakeTableValuedFunctionRegistration fn)
        {
            SchemaMetadata = schemaMetadata;
            function_reg = fn;
            FunctionName = fn.FunctionName;
        }

        class FunctionParameters : IBatchParameterMetadata
        {
            private StoreLakeTableValuedFunctionRegistration function_reg;

            public FunctionParameters(StoreLakeTableValuedFunctionRegistration function_reg)
            {
                this.function_reg = function_reg;
            }

            ColumnTypeMetadata IBatchParameterMetadata.TryGetParameterType(string parameterName)
            {
                var prm = function_reg.Parameters.FirstOrDefault(x => string.Equals(x.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
                if (prm != null)
                {
                    var prmType = TypeMap.GetParameterClrType(prm);
                    //SourceMetadataTable.GetColumnDbType
                    //ProcedureGenerator.ResolveToDbDataType()
                    if (prmType.IsUserDefinedTableType || prmType.ParameterDbType == DbType.Object)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        return new ColumnTypeMetadata(prmType.ParameterDbType, prm.AllowNull);
                    }
                }

                return null;
            }
        }

        private readonly IDictionary<string, OutputColumnDescriptor> output_columns = new SortedDictionary<string, OutputColumnDescriptor>(StringComparer.OrdinalIgnoreCase);
        ColumnTypeMetadata IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
        {
            if (output_columns.Count == 0)
            {
                if (function_reg.IsInline)
                {
                    ProcedureGenerator.LoadFunctionOutputColumns(SchemaMetadata, new FunctionParameters(function_reg), function_reg.FunctionBodyScript, (col) =>
                    {
                        output_columns.Add(col.OutputColumnName, col);
                    });
                }
                else
                {
                    foreach (var column in function_reg.Columns)
                    {
                        var prmType = TypeMap.GetParameterClrType(column.ColumnDbType, "?");
                        if (prmType.IsUserDefinedTableType || prmType.ParameterDbType == DbType.Object)
                        {
                            throw new NotImplementedException();
                        }
                        else
                        {
                            OutputColumnDescriptor col = new OutputColumnDescriptor(column.ColumnName, new ColumnTypeMetadata(prmType.ParameterDbType, column.IsNullable));
                            output_columns.Add(column.ColumnName, col);
                        }
                    }
                }
            }
            if (output_columns.TryGetValue(columnName, out OutputColumnDescriptor coldesc))
            {
                return coldesc.ColumnType;
            }

            return null;
        }
    }

    [DebuggerDisplay("{ViewName}")]
    class SourceMetadataView : IColumnSourceMetadata
    {
        private readonly StoreLakeViewRegistration view_reg;
        private readonly string ViewName;
        private readonly ISchemaMetadataProvider SchemaMetadata;
        public SourceMetadataView(ISchemaMetadataProvider schemaMetadata, StoreLakeViewRegistration vw)
        {
            SchemaMetadata = schemaMetadata;
            view_reg = vw;
            ViewName = vw.ViewName;
        }

        private readonly IDictionary<string, OutputColumnDescriptor> output_columns = new SortedDictionary<string, OutputColumnDescriptor>(StringComparer.OrdinalIgnoreCase);
        ColumnTypeMetadata IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
        {
            if (output_columns.Count == 0)
            {
                ProcedureGenerator.LoadViewOutputColumns(SchemaMetadata, view_reg.ViewQueryScript, (col) =>
                {
                    output_columns.Add(col.OutputColumnName, col);
                });
            }
            if (output_columns.TryGetValue(columnName, out OutputColumnDescriptor coldesc))
            {
                return coldesc.ColumnType;
            }

            return null;
        }
    }

    [DebuggerDisplay("{table.TableName}")]
    class SourceMetadataTable : IColumnSourceMetadata
    {
        private readonly DataTable table;
        public SourceMetadataTable(DataTable table)
        {
            this.table = table;
        }
        ColumnTypeMetadata IColumnSourceMetadata.TryGetColumnTypeByName(string columnName)
        {
            DataColumn column = table.Columns[columnName];
            if (column != null)
            {
                return new ColumnTypeMetadata(GetColumnDbType(column.DataType), column.AllowDBNull);
            }
            return null;
        }

        internal static DbType GetColumnDbType(Type dataType)
        {
            if (dataType == typeof(int))
                return DbType.Int32;
            if (dataType == typeof(string))
                return DbType.String;
            if (dataType == typeof(short))
                return DbType.Int16;
            if (dataType == typeof(byte))
                return DbType.Byte;
            if (dataType == typeof(bool))
                return DbType.Boolean;
            if (dataType == typeof(Guid))
                return DbType.Guid;
            if (dataType == typeof(Int64))
                return DbType.Int64;
            if (dataType == typeof(DateTime))
                return DbType.DateTime;
            if (dataType == typeof(decimal))
                return DbType.Decimal;
            if (dataType == typeof(byte[]))
                return DbType.Binary;
            throw new NotImplementedException(dataType.Name);
        }
    }

    internal sealed class TableTypeRow
    {
        internal CodeTypeDeclaration udt_row_type_decl;
        internal string ClrFullTypeName;
    }


    public static class SchemaImportDacPac // 'Dedicated Administrator Connection (for Data Tier Application) Package'
    {
        public static RegistrationResult ImportDacPac(string inputdir, string dacpacFullFileName, bool forceReferencePackageRegeneration, bool generateMissingReferences)
        {
            //string databaseName = "DemoTestData";
            DataSet ds = new DataSet() { Namespace = "dbo" }; // see 'https://www.codeproject.com/articles/30490/how-to-manually-create-a-typed-datatable'
            RegistrationResult ctx = new RegistrationResult(ds, forceReferencePackageRegeneration, generateMissingReferences);
            RegisterDacpac(" ", ctx, inputdir, dacpacFullFileName, false);
            return ctx;
        }


        private static DacPacRegistration RegisterDacpac(string outputprefix, RegistrationResult ctx, string inputdir, string filePath, bool isReferencedPackage)
        {
            DacPacRegistration dacpac;
            if (ctx.procesed_files.TryGetValue(filePath, out dacpac))
            {
                return dacpac;
            }
            dacpac = new DacPacRegistration(filePath, isReferencedPackage);
            ctx.procesed_files.Add(filePath, dacpac);
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
                            dacpac.UniqueKey = dacpac.DacPacAssemblyAssemblyName;
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
                                    if (ctx.registered_dacpacs.TryGetValue(logicalname, out external_dacpac))
                                    {
                                        // already registered
                                    }
                                    else
                                    {
                                        external_dacpac = RegisterDacpac(outputprefix + "   ", ctx, inputdir, dacpacFileName, true);

                                    }


                                    dacpac.referenced_dacpacs.Add(logicalname, external_dacpac);
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
                                AddInlineTableValuedFunctions(ctx, dacpac, xModel);
                                AddMultiStatementTableValuedFunction(ctx, dacpac, xModel);
                                // <Element Type="SqlScalarFunction" Name="[dbo].[hlsystablecfgdeskfield_validate_attribute]">

                                AddUserDefinedTableTypes(ctx, dacpac, xModel);
                                AddViews(ctx, dacpac, xModel);

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

        private static void CollectRelationParameters(SchemaObjectName sonObject, XElement xRelationshipParent, Action<StoreLakeParameterRegistration> collector)
        {
            var xRelationship = xRelationshipParent.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Parameters")).SingleOrDefault();
            if (xRelationship == null)
            {
                // no parameters
            }
            else
            {
                foreach (var xRelationship_Entry in xRelationship.Elements())
                {
                    foreach (var xSqlSubroutineParameter in xRelationship_Entry.Elements().Where(e => e.Name.LocalName == "Element" && e.Attributes().Any(t => t.Name.LocalName == "Type" && t.Value == "SqlSubroutineParameter")))
                    {
                        var xRelationship_Entry_Element = xSqlSubroutineParameter;
                        //var xRelationship_Entry_Element_Name = xRelationship_Entry_Element.Attributes().Single(a => a.Name.LocalName == "Name");
                        //string[] name_tokens = xRelationship_Entry_Element_Name.Value.Split('.');
                        //string parameter_name = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                        string parameter_name = ReadSchemaObjectName(3, xRelationship_Entry_Element).ItemName;

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
                        SchemaObjectName vtName = ReadTypeSpecifierRelationshipReferencesName(Type_SqlTypeSpecifier, "Type");

                        SqlDbType ColumnDbType = ParseKnownDbType(sonObject, parameter_name, vtName, out string structureTypeSchemaName, out string structureTypeName);
                        //if (!ColumnDbType.HasValue)
                        //{
                        //    ColumnDbType = SqlDbType.Structured;
                        //    //Console.WriteLine("    " + parameter_name + "  " + vtName);
                        //}

                        // <Property Name="DefaultExpressionScript">
                        // @objectid INT NULL = NULL
                        string DefaultExpressionScript;
                        var xProperty_DefaultExpressionScript = xRelationship_Entry_Element.Elements().FirstOrDefault(e => e.Name.LocalName == "Property" && e.Attributes().Any(t => t.Name.LocalName == "Name" && t.Value == "DefaultExpressionScript"));
                        if (xProperty_DefaultExpressionScript != null)
                        {
                            var xProperty_DefaultExpressionScript_Value = xProperty_DefaultExpressionScript.Elements().Single(x => x.Name.LocalName == "Value");
                            DefaultExpressionScript = xProperty_DefaultExpressionScript_Value.Value;
                        }
                        else
                        {
                            DefaultExpressionScript = null;
                        }

                        bool allowNull = false;
                        if (string.Equals(DefaultExpressionScript, "NULL", StringComparison.OrdinalIgnoreCase))
                        {
                            allowNull = true;
                        }


                        //collector(parameter_name, vtName, ColumnDbType, false, structureTypeSchemaName, structureTypeName);
                        StoreLakeParameterRegistration parameter = new StoreLakeParameterRegistration()
                        {
                            ParameterName = parameter_name,
                            ParameterTypeFullName = vtName.FullName,
                            ParameterDbType = ColumnDbType,
                            AllowNull = allowNull,
                            StructureTypeSchemaName = structureTypeSchemaName,
                            StructureTypeName = structureTypeName,

                            StructureTypeClassName = structureTypeName,
                            //StructureTypeNamespaceName = null,
                        };

                        collector(parameter);
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

        private static SchemaObjectName ReadTypeSpecifierRelationshipReferencesName(XElement xRelationshipParent, string collectionName)
        {
            var xRelationship = xRelationshipParent.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == collectionName)).Single();
            var xRelationship_Entry = xRelationship.Elements().Single();
            {
                var xRelationship_Entry_Element = xRelationship_Entry.Elements().Single(e => e.Name.LocalName == "References" && e.Attributes().Any(t => t.Name.LocalName == "Name"));
                {
                    //var xaReferences_Name = xRelationship_Entry_Element.Attributes().Single(t => t.Name.LocalName == "Name");
                    //return xaReferences_Name.Value;
                    return ReadSchemaObjectName(1, xRelationship_Entry_Element); // 1:builtin 2:udt
                }
            }
        }

        private sealed class SchemaObjectName
        {
            internal string FullName;
            internal int PartsCount;

            internal string SchemaName; // 0
            internal string ObjectName; // 1
            internal string ItemName; // 2 column or parameter
        }

        private static void AddUserDefinedTableTypes(RegistrationResult ctx, DacPacRegistration dacpac, XElement xModel)
        {
            // <Element Type="SqlTableType" Name="[dbo].[hlsys_udt_idset]">
            foreach (var xTableType in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlTableType")))
            {
                SchemaObjectName sonTableType = ReadSchemaObjectName(2, xTableType);

                StoreLakeTableTypeRegistration treg = new StoreLakeTableTypeRegistration()
                {
                    TableTypeSqlFullName = sonTableType.FullName,
                    TableTypeName = sonTableType.ObjectName,
                    TableTypeSchema = sonTableType.SchemaName,
                };

                // <Relationship Name="Columns">
                CollectTableColumns(xTableType, sonTableType, treg.Columns, false);
                CollectTableColumns(xTableType, sonTableType, treg.Columns, true);

                // <Relationship Name="Constraints">
                // <Element Type="SqlTableTypePrimaryKeyConstraint">
                CollectTableTypePrimaryKeyColumns(xTableType, (column_name) =>
                {
                    var column = treg.Columns.Single(x => x.ColumnName == column_name);
                    column.IsNullable = false;

                    if (treg.PrimaryKey == null)
                    {
                        treg.PrimaryKey = new StoreLakeTableTypeKey();
                    }
                    treg.PrimaryKey.ColumnNames.Add(column_name);
                });

                ctx.registered_tabletypes.Add(sonTableType.FullName, dacpac);
                dacpac.registered_tabletypes.Add(sonTableType.FullName, treg);
            }

        }

        private static void CollectTableTypePrimaryKeyColumns(XElement xTableType, Action<string> collector)
        {
            var xRelationship_Constraints = xTableType.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Constraints")).SingleOrDefault();
            if (xRelationship_Constraints == null)
                return; // no primary key
            foreach (var xRelationship_Constraints_Entry in xRelationship_Constraints.Elements().Where(e => e.Name.LocalName == "Entry"))
            {
                var xSqlTableTypePrimaryKeyConstraint = xRelationship_Constraints_Entry.Elements().SingleOrDefault(e => e.Name.LocalName == "Element" && e.Attributes().Any(a => a.Name.LocalName == "Type" && a.Value == "SqlTableTypePrimaryKeyConstraint"));
                if (xSqlTableTypePrimaryKeyConstraint != null) // SqlTableTypeDefaultConstraint
                {
                    var xRelationship_Columns = xSqlTableTypePrimaryKeyConstraint.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ColumnSpecifications")).Single();

                    foreach (var xRelationship_Columns_Entry in xRelationship_Columns.Elements())
                    {
                        var xElement_Column = xRelationship_Columns_Entry.Elements().Single(e => e.Name.LocalName == "Element" && e.Attributes().Any(a => a.Name.LocalName == "Type" && a.Value == "SqlTableTypeIndexedColumnSpecification"));

                        var xRelationship_Column = xElement_Column.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Column")).Single();

                        var xRelationship_Column_Entry = xRelationship_Column.Elements().Where(e => e.Name.LocalName == "Entry").Single();

                        var xRelationship_Column_Entry_References = xRelationship_Column_Entry.Elements().Where(e => e.Name.LocalName == "References").Single();

                        string column_name = ReadSchemaObjectName(3, xRelationship_Column_Entry_References).ItemName;

                        collector(column_name);
                    }
                }
            }
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
        private static SchemaObjectName ReadSchemaObjectName(int min, XElement xObject)
        {
            XAttribute xObject_Name = xObject.Attributes().Where(x => x.Name == "Name").Single();
            string fullName = xObject_Name.Value;

            string[] name_tokens = fullName.Split('.');
            if (name_tokens.Length < min)
            {
                throw new InvalidOperationException("Too less parts specified. Expected:" + min + " :" + fullName);
            }

            SchemaObjectName soName = new SchemaObjectName() { FullName = fullName, PartsCount = name_tokens.Length };

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

        private static void AddProcedures(DataSet ds, DacPacRegistration dacpac, XElement xModel)
        {
            // <Element Type="SqlProcedure" Name="[dbo].[hlbpm_query_cmdbflowattributes]">
            foreach (var xProcedure in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlProcedure")))
            {
                SchemaObjectName sonProcedure = ReadSchemaObjectName(2, xProcedure);

                // <Property Name="BodyScript">
                var xProperty_BodyScript = xProcedure.Elements().Single(e => e.Name.LocalName == "Property" && e.Attributes().Any(t => t.Name.LocalName == "Name" && t.Value == "BodyScript"));
                var xProperty_BodyScript_Value = xProperty_BodyScript.Elements().Single(e => e.Name.LocalName == "Value");
                XCData xcdata = (XCData)xProperty_BodyScript_Value.FirstNode;
                StoreLakeProcedureRegistration procedure_reg = new StoreLakeProcedureRegistration();
                procedure_reg.ProcedureSchemaName = sonProcedure.SchemaName;
                procedure_reg.ProcedureName = sonProcedure.ObjectName;
                procedure_reg.ProcedureBodyScript = xcdata.Value.Trim(); //xProperty_BodyScript_Value == null ? null : xProperty_BodyScript.Value;

                IList<ProcedureParameter> proc_params = null;
                var xSysCommentsObjectAnnotation = xProcedure.Elements().Single(e => e.Name.LocalName == "Annotation" && e.Attributes().Any(t => t.Name.LocalName == "Type" && t.Value == "SysCommentsObjectAnnotation"));
                if (xSysCommentsObjectAnnotation != null)
                {

                    var xSysCommentsObjectAnnotation_Property_HeaderContents = xSysCommentsObjectAnnotation.Elements().Single(e => e.Name.LocalName == "Property" && e.Attributes().Any(t => t.Name.LocalName == "Name" && t.Value == "HeaderContents"));
                    if (xSysCommentsObjectAnnotation_Property_HeaderContents != null)
                    {
                        var xaSysCommentsObjectAnnotation_Property_HeaderContents_Value = xSysCommentsObjectAnnotation_Property_HeaderContents.Attributes().Single(x => x.Name.LocalName == "Value");
                        string comments = xaSysCommentsObjectAnnotation_Property_HeaderContents_Value.Value;
                        CollectProcedureAnnotations(comments, (string key, string value) =>
                        {
                            procedure_reg.Annotations.Add(new StoreLakeAnnotationRegistration() { AnnotationKey = key, AnnotationValue = value });

                        });

                        //HeaderContentTokens = comments.Split(';');

                        // hlsp_approvalfulfilled
                        string proc_decl = comments + " SELECT 1";

                        TSqlScript sqlF = (TSqlScript)ScriptDomFacade.Parse(proc_decl);

                        CreateProcedureStatement stmt = (CreateProcedureStatement)sqlF.Batches[0].Statements[0];
                        proc_params = stmt.Parameters;
                    }
                }


                // <Relationship Name="Parameters">
                CollectRelationParameters(sonProcedure, xProcedure, (StoreLakeParameterRegistration parameter) =>
                {
                    if (proc_params != null)
                    {
                        var proc_param = proc_params.FirstOrDefault(x => string.Equals(x.VariableName.Value, parameter.ParameterName, StringComparison.OrdinalIgnoreCase));
                        if (proc_param.Nullable != null && proc_param.Nullable.Nullable)
                        {
                            // hlsp_approvalfulfilled
                            parameter.IsNULLSpecified = true;
                            parameter.AllowNull = true;
                        }

                        if (proc_param.Value != null)
                        {
                            // hlsp_approvalfulfilled
                            if (proc_param.Value is IntegerLiteral intLit)
                            {
                            }
                            else
                            {
                                NullLiteral nullLit = (NullLiteral)proc_param.Value;
                            }
                        }
                    }
                    procedure_reg.Parameters.Add(parameter);
                });

                dacpac.registered_Procedures.Add(sonProcedure.FullName, procedure_reg);
            }
        }

        private static void CollectProcedureAnnotations(string comments, Action<string, string> collector)
        {
            string[] lines = comments.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int ix = 0; ix < lines.Length; ix++)
            {
                string line = lines[ix].Trim();
                if (line.StartsWith("--")) // ?
                {
                    // comment line
                    int indexof = line.IndexOf("@Name ");
                    if (indexof >= 0)
                    {
                        string item_key = "Name";
                        string item_value = line.Substring(indexof + "@Name".Length).Trim();
                        collector(item_key, item_value);
                    }
                    else
                    {
                        indexof = line.IndexOf("@Return ");
                        if (indexof >= 0)
                        {
                            string item_key = "Return";
                            string item_value = line.Substring(indexof + "@Return ".Length).Trim();
                            collector(item_key, item_value);
                        }
                    }
                }
            }
        }

        private static void AddCheckConstraints(DataSet ds, DacPacRegistration dacpac, XElement xModel)
        {
            //     <Element Type="SqlCheckConstraint" Name="[dbo].[CK_hlcmdocumentstorage_formmgmt]">
            foreach (var xCheckConstraint in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlCheckConstraint")))
            {
                //XAttribute xCheckConstraintName = xCheckConstraint.Attributes().Where(x => x.Name == "Name").First();
                SchemaObjectName sonCK = ReadSchemaObjectName(2, xCheckConstraint);

                StoreLakeCheckConstraintRegistration ck_reg = new StoreLakeCheckConstraintRegistration();

                //string[] name_tokens = xCheckConstraintName.Value.Split('.');
                ck_reg.CheckConstraintName = sonCK.ObjectName;// name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                ck_reg.CheckConstraintSchema = sonCK.SchemaName;// (name_tokens.Length == 3) ? name_tokens[0] : "dbo";

                SchemaObjectName defining_table = CollectElementRelationshipTable(xCheckConstraint, "DefiningTable");
                ck_reg.DefiningTableName = defining_table.ObjectName;
                ck_reg.DefiningTableSchema = defining_table.SchemaName;

                CollectRelationReferencesColumns(xCheckConstraint, 0, false, "CheckExpressionDependencies", (columnName) =>
                 {
                     ck_reg.DefiningColumns.Add(new StoreLakeKeyColumnRegistration { ColumnName = columnName });
                 });

                var xCheckExpressionScript = xCheckConstraint.Elements().Single(e => e.Name.LocalName == "Property" && e.Attributes().Any(t => t.Name.LocalName == "Name" && t.Value == "CheckExpressionScript"));
                var xCheckExpressionScript_Value = xCheckExpressionScript.Elements().Single(e => e.Name.LocalName == "Value");
                ck_reg.CheckExpressionScript = xCheckExpressionScript_Value.Value;

                dacpac.registered_CheckConstraints.Add(sonCK.FullName, ck_reg);
                //DatabaseRegistration.RegisterCheckConstraint(ds, ck_reg);
            }
        }
        private static void AddForeignKeys(DataSet ds, XElement xModel)
        {
            //     <Element Type="SqlForeignKeyConstraint" Name="[dbo].[FK_hlcmdatamodelassociationsearch_associationid]">
            foreach (var xForeignKey in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlForeignKeyConstraint")))
            {
                //XAttribute xForeignKeyName = xForeignKey.Attributes().Where(x => x.Name == "Name").First();
                SchemaObjectName sonFK = ReadSchemaObjectName(2, xForeignKey);
                StoreLakeForeignKeyRegistration fk_reg = new StoreLakeForeignKeyRegistration();

                //string[] name_tokens = xForeignKeyName.Value.Split('.');
                fk_reg.ForeignKeyName = sonFK.ObjectName;// name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                fk_reg.ForeignKeySchema = sonFK.SchemaName;// (name_tokens.Length == 3) ? name_tokens[0] : "dbo";

                SchemaObjectName foreign_table = CollectElementRelationshipTable(xForeignKey, "ForeignTable");
                fk_reg.ForeignTableName = foreign_table.ObjectName;
                fk_reg.ForeignTableSchema = foreign_table.SchemaName;

                SchemaObjectName defining_table = CollectElementRelationshipTable(xForeignKey, "DefiningTable");
                fk_reg.DefiningTableName = defining_table.ObjectName;
                fk_reg.DefiningTableSchema = defining_table.SchemaName;

                CollectRelationReferencesColumns(xForeignKey, 1, false, "Columns", (columnName) =>
                 {
                     fk_reg.DefiningColumns.Add(new StoreLakeKeyColumnRegistration { ColumnName = columnName });
                 });
                CollectRelationReferencesColumns(xForeignKey, 1, true, "ForeignColumns", (columnName) =>
                {
                    fk_reg.ForeignColumns.Add(new StoreLakeKeyColumnRegistration { ColumnName = columnName });
                });

                DatabaseRegistration.RegisterForeignKey(ds, fk_reg);
            }
        }

        private static SchemaObjectName CollectElementRelationshipTable(XElement xForeignKey, string relationshipItemName)
        {
            var xDefiningTable = xForeignKey.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == relationshipItemName));
            var xDefiningTable_Entry = xDefiningTable.Elements().Single(e => e.Name.LocalName == "Entry");
            var xDefiningTable_Entry_References = xDefiningTable_Entry.Elements().Single(e => e.Name.LocalName == "References");
            return ReadSchemaObjectName(2, xDefiningTable_Entry_References);
            //var xDefiningTable_Entry_References_Name = xDefiningTable_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
            //string[] name_tokens = xDefiningTable_Entry_References_Name.Value.Split('.');
            //return new SchemaObjectName()
            //{
            //    ObjectName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", ""),
            //    SchemaName = (name_tokens.Length == 2) ? name_tokens[0] : "dbo"
            //};
        }

        private static void AddDefaultConstraints(DataSet ds, XElement xModel)
        {
            //     <Element Type="SqlDefaultConstraint" Name="[dbo].[DF_hlsysagent_active]">
            foreach (var xDefaultConstraint in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlDefaultConstraint")))
            {
                SchemaObjectName sonDF = ReadSchemaObjectName(2, xDefaultConstraint);
                //XAttribute xDefaultConstraintName = xDefaultConstraint.Attributes().Where(x => x.Name == "Name").First();

                //string[] name_tokens = xDefaultConstraintName.Value.Split('.');

                bool skip_df = false;
                StoreLakeDefaultContraintRegistration df_reg = new StoreLakeDefaultContraintRegistration()
                {
                    ConstraintName = sonDF.ObjectName,// name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", ""),
                    ConstraintSchema = sonDF.SchemaName,//(name_tokens.Length == 2) ? name_tokens[0] : "dbo",
                };

                var xForColumn = xDefaultConstraint.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ForColumn"));
                var xForColumn_Entry = xForColumn.Elements().Single(e => e.Name.LocalName == "Entry");
                var xForColumn_Entry_References = xForColumn_Entry.Elements().Single(e => e.Name.LocalName == "References");
                //var xForColumn_Entry_References_Name = xForColumn_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                //name_tokens = xForColumn_Entry_References_Name.Value.Split('.');
                df_reg.ColumnName = ReadSchemaObjectName(3, xForColumn_Entry_References).ItemName;// name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");

                SchemaObjectName defining_table = CollectElementRelationshipTable(xDefaultConstraint, "DefiningTable");
                df_reg.TableName = defining_table.ObjectName;
                df_reg.TableSchema = defining_table.SchemaName;

                var table = ds.Tables[df_reg.TableName, df_reg.TableSchema];
                var column = table.Columns[df_reg.ColumnName];
                if (column == null)
                {
                    throw new StoreLakeSdkException("Column not found. Table [" + table.TableName + "] column [" + df_reg.ColumnName + "]");
                }

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

                if (df_reg.IsScalarValue && column.DataType == typeof(DateTime) && df_reg.ValueInt32.HasValue) // DEFAULT (0)
                {
                    df_reg.IsBuiltInFunctionExpression = true;
                    df_reg.DefaultExpressionScript = "GETDATEX";
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
                        SchemaObjectName sonUQ = ReadSchemaObjectName(3, xIndex);
                        StoreLakeTableKeyRegistration uqreg = new StoreLakeTableKeyRegistration();

                        //var xIndex_Name = xIndex.Attributes().Single(a => a.Name.LocalName == "Name");
                        //string[] name_tokens = xIndex_Name.Value.Replace("[", "").Replace("]", "").Split('.');
                        uqreg.KeyName = sonUQ.ItemName;// name_tokens[name_tokens.Length - 1];
                        uqreg.KeySchema = sonUQ.SchemaName;// (name_tokens.Length == 3) ? name_tokens[0] : "dbo";

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
                            SchemaObjectName sonTable = ReadSchemaObjectName(2, xIndexedObject_Entry_References);
                            //var xIndexedObject_Entry_References_Name = xIndexedObject_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                            //name_tokens = xIndexedObject_Entry_References_Name.Value.Split('.');
                            uqreg.TableName = sonTable.ObjectName;// name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                            uqreg.TableSchema = sonTable.SchemaName;// (name_tokens.Length == 2) ? name_tokens[0] : "dbo";

                            var xColumnSpecifications = xIndex.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ColumnSpecifications"));
                            foreach (var xColumnSpecifications_Entry in xColumnSpecifications.Elements().Where(e => e.Name.LocalName == "Entry"))
                            {
                                var xSqlIndexedColumnSpecification = xColumnSpecifications_Entry.Elements().Single(e => e.Name.LocalName == "Element");
                                var xColumn = xSqlIndexedColumnSpecification.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Column"));

                                var xColumn_Entry = xColumn.Elements().Single(e => e.Name.LocalName == "Entry");
                                var xColumn_Entry_References = xColumn_Entry.Elements().Single(e => e.Name.LocalName == "References");
                                //var xColumn_Entry_References_Name = xColumn_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");

                                //name_tokens = xColumn_Entry_References_Name.Value.Split('.');

                                StoreLakeKeyColumnRegistration pkcol_reg = new StoreLakeKeyColumnRegistration()
                                {
                                    ColumnName = ReadSchemaObjectName(3, xColumn_Entry_References).ItemName //name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "")
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
                        SchemaObjectName sonUQ = ReadSchemaObjectName(2, xUniqueConstraint);
                        StoreLakeTableKeyRegistration uqreg = new StoreLakeTableKeyRegistration()
                        {
                            KeySchema = sonUQ.SchemaName,
                            KeyName = sonUQ.ObjectName
                        };

                        //var xUniqueConstraint_Name = xUniqueConstraint.Attributes().Single(a => a.Name.LocalName == "Name");
                        //string[] name_tokens = xUniqueConstraint_Name.Value.Replace("[", "").Replace("]", "").Split('.');
                        //uqreg.KeyName = name_tokens[name_tokens.Length - 1];
                        //uqreg.KeySchema = (name_tokens.Length == 3) ? name_tokens[0] : "dbo";

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
                            SchemaObjectName sonTable = ReadSchemaObjectName(2, xDefiningTable_Entry_References);
                            //var xDefiningTable_Entry_References_Name = xDefiningTable_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                            //name_tokens = xDefiningTable_Entry_References_Name.Value.Split('.');
                            //uqreg.TableName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                            //uqreg.TableSchema = (name_tokens.Length == 2) ? name_tokens[0] : "dbo";
                            uqreg.TableName = sonTable.ObjectName;
                            uqreg.TableSchema = sonTable.SchemaName;

                            var xColumnSpecifications = xUniqueConstraint.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ColumnSpecifications"));
                            foreach (var xColumnSpecifications_Entry in xColumnSpecifications.Elements().Where(e => e.Name.LocalName == "Entry"))
                            {
                                var xSqlIndexedColumnSpecification = xColumnSpecifications_Entry.Elements().Single(e => e.Name.LocalName == "Element");
                                var xColumn = xSqlIndexedColumnSpecification.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Column"));

                                var xColumn_Entry = xColumn.Elements().Single(e => e.Name.LocalName == "Entry");
                                var xColumn_Entry_References = xColumn_Entry.Elements().Single(e => e.Name.LocalName == "References");
                                //var xColumn_Entry_References_Name = xColumn_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");

                                //SchemaObjectName sonColumn = ReadSchemaObjectName(3, xColumn_Entry_References);
                                //name_tokens = xColumn_Entry_References_Name.Value.Split('.');

                                StoreLakeKeyColumnRegistration pkcol_reg = new StoreLakeKeyColumnRegistration()
                                {
                                    ColumnName = ReadSchemaObjectName(3, xColumn_Entry_References).ItemName// name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "")
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

                //var xPrimaryKey_Name = xPrimaryKey.Attributes().Single(a => a.Name.LocalName == "Name");
                //string[] name_tokens = xPrimaryKey_Name.Value.Split('.');
                //pkreg.KeyName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                //pkreg.KeySchema = (name_tokens.Length == 2) ? name_tokens[0] : "dbo";
                SchemaObjectName sonPk = ReadSchemaObjectName(2, xPrimaryKey);
                pkreg.KeyName = sonPk.ObjectName;
                pkreg.KeySchema = sonPk.SchemaName;


                var xDefiningTable = xPrimaryKey.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "DefiningTable"));
                var xDefiningTable_Entry = xDefiningTable.Elements().Single(e => e.Name.LocalName == "Entry");
                var xDefiningTable_Entry_References = xDefiningTable_Entry.Elements().Single(e => e.Name.LocalName == "References");

                //var xDefiningTable_Entry_References_Name = xDefiningTable_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                SchemaObjectName sonTable = ReadSchemaObjectName(2, xDefiningTable_Entry_References);
                //name_tokens = xDefiningTable_Entry_References_Name.Value.Split('.');
                //pkreg.TableName = name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                //pkreg.TableSchema = (name_tokens.Length == 2) ? name_tokens[0] : "dbo";
                pkreg.TableName = sonTable.ObjectName;
                pkreg.TableSchema = sonTable.SchemaName;

                var xColumnSpecifications = xPrimaryKey.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ColumnSpecifications"));
                foreach (var xColumnSpecifications_Entry in xColumnSpecifications.Elements().Where(e => e.Name.LocalName == "Entry"))
                {
                    var xSqlIndexedColumnSpecification = xColumnSpecifications_Entry.Elements().Single(e => e.Name.LocalName == "Element");
                    var xColumn = xSqlIndexedColumnSpecification.Elements().Single(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Column"));

                    var xColumn_Entry = xColumn.Elements().Single(e => e.Name.LocalName == "Entry");
                    var xColumn_Entry_References = xColumn_Entry.Elements().Single(e => e.Name.LocalName == "References");

                    //SchemaObjectName sonColumn = ReadSchemaObjectName(4, xColumn_Entry_References);
                    StoreLakeKeyColumnRegistration pkcol_reg = new StoreLakeKeyColumnRegistration()
                    {
                        ColumnName = ReadSchemaObjectName(3, xColumn_Entry_References).ItemName
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
                SchemaObjectName sonTable = ReadSchemaObjectName(2, xTable);

                StoreLakeTableRegistration treg = new StoreLakeTableRegistration()
                {
                    TableName = sonTable.ObjectName,
                    TableSchema = sonTable.SchemaName,
                };

                // <Relationship Name="Columns">
                CollectTableColumns(xTable, sonTable, treg.Columns, false);
                CollectTableColumns(xTable, sonTable, treg.Columns, true);

                ctx.registered_tables.Add(treg.TableName, dacpac);
                dacpac.registered_tables.Add(treg.TableName, true);
                DatabaseRegistration.RegisterTable(ctx.ds, treg);
            }
        }

        // 
        private static void AddMultiStatementTableValuedFunction(RegistrationResult ctx, DacPacRegistration dacpac, XElement xModel)
        {
            // <Element Type="SqlMultiStatementTableValuedFunction" Name="[dbo].[hlsysattachment_query_data_case]">
            // <Element Type="SqlMultiStatementTableValuedFunction" Name="[dbo].[hlsyssec_query_agentsystemacl]">
            foreach (var xFunction in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlMultiStatementTableValuedFunction")))
            {
                SchemaObjectName sonFunction = ReadSchemaObjectName(2, xFunction);

                StoreLakeTableValuedFunctionRegistration function_reg = new StoreLakeTableValuedFunctionRegistration()
                {
                    FunctionName = sonFunction.ObjectName,
                    FunctionSchema = sonFunction.SchemaName,
                    IsInline = false,
                };

                //FunctionBody
                var xRelationship_FunctionBody = xFunction.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "FunctionBody")).Single();
                var xRelationship_FunctionBody_Entry = xRelationship_FunctionBody.Elements().Single();
                var xSqlScriptFunctionImplementation = xRelationship_FunctionBody_Entry.Elements().Single();
                // <Property Name="BodyScript">
                var xProperty_BodyScript = xSqlScriptFunctionImplementation.Elements().Single(e => e.Name.LocalName == "Property" && e.Attributes().Any(t => t.Name.LocalName == "Name" && t.Value == "BodyScript"));
                var xProperty_BodyScript_Value = xProperty_BodyScript.Elements().Single(e => e.Name.LocalName == "Value");
                XCData xcdata = (XCData)xProperty_BodyScript_Value.FirstNode;

                function_reg.FunctionBodyScript = ProcedureGenerator.RemoveTrailingBlockComment(xcdata.Value.Trim());

                CollectRelationParameters(sonFunction, xFunction, (StoreLakeParameterRegistration parameter) =>
                {
                    function_reg.Parameters.Add(parameter);
                });

                //<Relationship Name="Columns">
                CollectTableColumns(xFunction, sonFunction, function_reg.Columns, false);
                CollectTableColumns(xFunction, sonFunction, function_reg.Columns, true);

                ctx.registered_functions.Add(function_reg.FunctionName, dacpac);
                dacpac.registered_TableValuedFunctions.Add(function_reg.FunctionName, function_reg);
            }
        }

        private static void AddInlineTableValuedFunctions(RegistrationResult ctx, DacPacRegistration dacpac, XElement xModel)
        {
            // <Element Type="SqlInlineTableValuedFunction" Name="[dbo].[hlsyssec_query_agentobjectmsk]">
            foreach (var xFunction in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlInlineTableValuedFunction")))
            {
                SchemaObjectName sonFunction = ReadSchemaObjectName(2, xFunction);

                StoreLakeTableValuedFunctionRegistration function_reg = new StoreLakeTableValuedFunctionRegistration()
                {
                    FunctionName = sonFunction.ObjectName,
                    FunctionSchema = sonFunction.SchemaName,
                    IsInline = true,
                };

                //FunctionBody
                var xRelationship_FunctionBody = xFunction.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "FunctionBody")).Single();
                var xRelationship_FunctionBody_Entry = xRelationship_FunctionBody.Elements().Single();
                var xSqlScriptFunctionImplementation = xRelationship_FunctionBody_Entry.Elements().Single();
                // <Property Name="BodyScript">
                var xProperty_BodyScript = xSqlScriptFunctionImplementation.Elements().Single(e => e.Name.LocalName == "Property" && e.Attributes().Any(t => t.Name.LocalName == "Name" && t.Value == "BodyScript"));
                var xProperty_BodyScript_Value = xProperty_BodyScript.Elements().Single(e => e.Name.LocalName == "Value");
                XCData xcdata = (XCData)xProperty_BodyScript_Value.FirstNode;

                function_reg.FunctionBodyScript = ProcedureGenerator.RemoveTrailingBlockComment(xcdata.Value.Trim());

                CollectRelationParameters(sonFunction, xFunction, (StoreLakeParameterRegistration parameter) =>
                {
                    function_reg.Parameters.Add(parameter);
                });

                ctx.registered_functions.Add(function_reg.FunctionName, dacpac);
                dacpac.registered_TableValuedFunctions.Add(function_reg.FunctionName, function_reg);
            }
        }

        private static void AddViews(RegistrationResult ctx, DacPacRegistration dacpac, XElement xModel)
        {
            // <Element Type="SqlView" Name="[dbo].[hlcmcontactvw]">
            foreach (var xView in xModel.Elements().Where(e => e.Attributes().Any(t => t.Name == "Type" && t.Value == "SqlView")))
            {
                SchemaObjectName sonView = ReadSchemaObjectName(2, xView);

                StoreLakeViewRegistration vreg = new StoreLakeViewRegistration()
                {
                    ViewName = sonView.ObjectName,
                    ViewSchema = sonView.SchemaName,
                };

                // QueryScript
                var xProperty_QueryScript = xView.Elements().Single(e => e.Name.LocalName == "Property" && e.Attributes().Any(t => t.Name.LocalName == "Name" && t.Value == "QueryScript"));
                var xProperty_QueryScript_Value = xProperty_QueryScript.Elements().Single(e => e.Name.LocalName == "Value");
                XCData xcdata = (XCData)xProperty_QueryScript_Value.FirstNode;
                vreg.ViewQueryScript = xcdata.Value.Trim();

                // <Relationship Name="Columns">
                var xRelationship_Columns = xView.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Columns")).Single();
                foreach (var xRelationship_Columns_Entry in xRelationship_Columns.Elements())
                {
                    var xElement_Column = xRelationship_Columns_Entry.Elements().Single(e => e.Name.LocalName == "Element" && e.Attributes().Any(a => a.Name.LocalName == "Type" && a.Value == "SqlComputedColumn"));

                    var sonColumn = ReadSchemaObjectName(3, xElement_Column);
                    StoreLakeViewColumnRegistration column = new StoreLakeViewColumnRegistration() { ColumnName = sonColumn.ItemName };

                    vreg.Columns.Add(column);

                    var xElement_Column_ExpressionDependencies = xElement_Column.Elements().SingleOrDefault(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "ExpressionDependencies"));
                    if (xElement_Column_ExpressionDependencies != null)
                    {
                        var xElement_Column_ExpressionDependencies_Entry = xElement_Column_ExpressionDependencies.Elements().Single(e => e.Name.LocalName == "Entry");
                        var xElement_Column_ExpressionDependencies_Entry_References = xElement_Column_ExpressionDependencies_Entry.Elements().Single(e => e.Name.LocalName == "References");
                        //5 due to CTE var sonRefColumn = ReadSchemaObjectName(3, xElement_Column_ExpressionDependencies_Entry_References);
                    }
                    else
                    {
                        // hlbreroleassignedtouservw : // SubQuery/CTE/(column rename via view definition)
                    }
                }

                ctx.registered_views.Add(vreg.ViewName, dacpac);
                dacpac.registered_views.Add(vreg.ViewName, vreg);
            }
        }

        private static void CollectRelationReferencesColumns(XElement xRelationship, int minColumnsCount, bool use_ExternalSource, string collectionName, Action<string> collector)
        {
            int columns_collected = 0;
            var xRelationship_Columns = xRelationship.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == collectionName)).First();
            foreach (var xRelationship_Columns_Entry in xRelationship_Columns.Elements())
            {
                var xElement_References = xRelationship_Columns_Entry.Elements().Single(e => e.Name.LocalName == "References");
                {
                    //<References ExternalSource="BuiltIns" Name="[bit]" />
                    if (xElement_References.Attributes().Any(a => a.Name.LocalName == "ExternalSource"))
                    {
                        // CONVERT(BIT,[dbo].[hlspdefinition_check_cmdb_assignment]([sptaskid],[cmdbflowid]))=(0)]
                        if (use_ExternalSource)
                        {
                            // <References ExternalSource="HelplineData.dacpac" Name="[dbo].[hlsyscasedef].[casedefid]" 
                            SchemaObjectName sonTableOrColumn = ReadSchemaObjectName(3, xElement_References); //name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                            columns_collected++;
                            collector(sonTableOrColumn.ItemName);
                        }
                        else
                        {
                            // skip
                        }
                    }
                    else
                    {
                        if (use_ExternalSource)
                        {

                        }
                        //else
                        {
                            //var xColumnName = xElement_References.Attributes().Single(a => a.Name.LocalName == "Name");
                            //string[] name_tokens = xColumnName.Value.Split('.');
                            SchemaObjectName sonTableOrColumn = ReadSchemaObjectName(2, xElement_References); //name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                            if (sonTableOrColumn.PartsCount == 2)
                            {
                                // table
                            }
                            else
                            {
                                columns_collected++;
                                collector(sonTableOrColumn.ItemName);
                            }
                        }
                    }
                }
            }

            if (columns_collected < minColumnsCount)
            {
                throw new NotImplementedException("No columns collected.");
            }
        }

        private static void CollectTableColumns(XElement xTable, SchemaObjectName sonTable, List<StoreLakeColumnRegistration> columns, bool processComputed)
        {
            var xRelationship_Columns = xTable.Elements().Where(e => e.Name.LocalName == "Relationship" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "Columns")).First();

            foreach (var xRelationship_Columns_Entry in xRelationship_Columns.Elements())
            {
                var xElement_Column = xRelationship_Columns_Entry.Elements().Single(e => e.Name.LocalName == "Element" && e.Attributes().Any(a => a.Name.LocalName == "Type"));
                var xElement_Column_Type = xElement_Column.Attributes().Single(a => a.Name.LocalName == "Type");
                bool isComputedColumn = string.Equals(xElement_Column_Type.Value, "SqlComputedColumn", StringComparison.Ordinal); // && a.Value == "SqlSimpleColumn" SqlComputedColumn
                if ((isComputedColumn && processComputed) || (!isComputedColumn && !processComputed))
                {
                    //var xColumnName = xElement_Column.Attributes().Single(a => a.Name.LocalName == "Name");
                    //string[] name_tokens = xColumnName.Value.Split('.');
                    string column_name = ReadSchemaObjectName(3, xElement_Column).ItemName;// name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");

                    StoreLakeColumnRegistration creg = new StoreLakeColumnRegistration()
                    {
                        ColumnName = column_name
                    };

                    var xIsNullable = xElement_Column.Elements().FirstOrDefault(e => e.Name.LocalName == "Property" && e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "IsNullable"));
                    if (xIsNullable != null)
                    {
                        var xValue = xIsNullable.Attributes().Single(a => a.Name.LocalName == "Value");
                        creg.IsNullable = string.Equals(xValue.Value, "False", StringComparison.OrdinalIgnoreCase) ? false
                                        : string.Equals(xValue.Value, "True", StringComparison.OrdinalIgnoreCase);
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
                                throw new StoreLakeSdkException("Oops [" + sonTable.ObjectName + "] COMPUTED [" + creg.ColumnName + "] AS " + expressionScript_Value + "");
                            }
                        }
                        else
                        {
                            foreach (var xExpressionDependencies_Entry in xExpressionDependencies.Elements().Where(x => x.Name.LocalName == "Entry"))
                            {
                                var xExpressionDependencies_Entry_References = xExpressionDependencies_Entry.Elements().Single(x => x.Name.LocalName == "References");
                                //var xDependencyColumnName = xExpressionDependencies_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                                //name_tokens = xDependencyColumnName.Value.Split('.');
                                string dependecy_column_name = ReadSchemaObjectName(3, xExpressionDependencies_Entry_References).ItemName;// name_tokens[name_tokens.Length - 1].Replace("[", "").Replace("]", "");
                                ExpressionDependencies_ColumnNames.Add(dependecy_column_name);

                                creg.ColumnDbType = columns.Single(x => x.ColumnName == dependecy_column_name).ColumnDbType;
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
                        //var xColumnTypeName = xTypeSpecifier_Entry_Element_Relationship_Entry_References.Attributes().Single(a => a.Name.LocalName == "Name");
                        var sonType = ReadSchemaObjectName(1, xTypeSpecifier_Entry_Element_Relationship_Entry_References);
                        creg.ColumnDbType = ParseKnownDbType(sonTable, creg.ColumnName, sonType, out string structureTypeSchemaName, out string structureTypeName);
                    }

                    columns.Add(creg);
                }// not a computedcolumn
            }
        }

        private static System.Data.SqlDbType ParseKnownDbType(SchemaObjectName sonObject, string itemName, SchemaObjectName sonType, out string structureTypeSchemaName, out string structureTypeName)
        {
            string typeName = sonType.FullName;
            structureTypeSchemaName = null;
            structureTypeName = null;
            if (string.Equals(typeName, "[int]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Int;
            }
            else if (string.Equals(typeName, "[bigint]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.BigInt;
            }
            else if (string.Equals(typeName, "[nvarchar]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.NVarChar;
            }
            else if (string.Equals(typeName, "[nchar]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.NChar;
            }
            else if (string.Equals(typeName, "[sys].[sysname]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.NVarChar;
            }
            else if (string.Equals(typeName, "[smallint]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.SmallInt;
            }
            else if (string.Equals(typeName, "[tinyint]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.TinyInt;
            }
            else if (string.Equals(typeName, "[bit]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Bit;
            }
            else if (string.Equals(typeName, "[datetime]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.DateTime;
            }
            else if (string.Equals(typeName, "[date]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Date;
            }
            else if (string.Equals(typeName, "[time]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Time;
            }
            else if (string.Equals(typeName, "[xml]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Xml;
            }
            else if (string.Equals(typeName, "[uniqueidentifier]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.UniqueIdentifier;
            }
            else if (string.Equals(typeName, "[decimal]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Decimal;
            }
            else if (string.Equals(typeName, "[float]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Float;
            }
            else if (string.Equals(typeName, "[varbinary]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.VarBinary;
            }
            else if (string.Equals(typeName, "[binary]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Binary;
            }
            else if (string.Equals(typeName, "[image]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Image;
            }
            else if (string.Equals(typeName, "[rowversion]", StringComparison.Ordinal))
            {
                return System.Data.SqlDbType.Timestamp;
            }
            else
            {
                structureTypeSchemaName = sonType.SchemaName;
                structureTypeName = sonType.ObjectName;
                return SqlDbType.Structured;
            }
        }
    }

    [Serializable]
    public sealed class StoreLakeSdkException : Exception
    {
        public StoreLakeSdkException(string message) : base(message)
        {

        }
    }
}

