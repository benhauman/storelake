using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace StoreLake.Sdk.SqlDom
{
    public static class ProcedureGenerator
    {
        public static ProcedureMetadata ParseProcedureBody(string procedure_schema, string procedure_name, string procedure_body, Dictionary<string, ProcedureCodeParameter> procedureParameters)
        {
            string fullName = "[" + procedure_schema + "].[" + procedure_name + "]";
            TSqlFragment sqlF = ScriptDomFacade.Parse(procedure_body);
            return new ProcedureMetadata(fullName, procedure_name, sqlF, procedureParameters);
        }
        public static ProcedureOutputSet[] IsQueryProcedure(bool resolveColumnType, ISchemaMetadataProvider schemaMetadata, ProcedureMetadata procedure_metadata)
        {
            BatchOutputColumnTypeResolver columnTypeResolver = new BatchOutputColumnTypeResolver(schemaMetadata, procedure_metadata.BodyFragment, procedure_metadata);
            StatementVisitor vstor = new StatementVisitor(resolveColumnType, columnTypeResolver, procedure_metadata.BodyFragment);
            procedure_metadata.BodyFragment.Accept(vstor);
            return vstor.resultHasOutputResultSet.ToArray();
        }


        public static DbType ResolveToDbDataType(DataTypeReference dataType)
        {
            if (dataType.Name.Count == 1)
            {
                string typeName = dataType.Name[0].Value;
                if (string.Equals("INT", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    //return typeof(int);
                    return DbType.Int32;
                }

                if (string.Equals("BIT", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    //return typeof(bool);
                    return DbType.Boolean;
                }

                if (string.Equals("NVARCHAR", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    SqlDataTypeReference sqlDataType = (SqlDataTypeReference)dataType;
                    string maxLen = sqlDataType.Parameters[0].Value;
                    //dataType.p
                    //return typeof(string);
                    return DbType.String;
                }

                if (string.Equals("SMALLINT", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.Int16;
                }

                if (string.Equals("DATETIME", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.DateTime;
                }

                if (string.Equals("BIGINT", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.Int64;
                }

                if (string.Equals("TINYINT", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.Byte;
                }

                if (string.Equals("UNIQUEIDENTIFIER", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.Guid;
                }
                if (string.Equals("VARBINARY", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.Binary;
                }
                if (string.Equals("DECIMAL", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.Decimal;
                }
                if (string.Equals("DATETIMEOFFSET", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.DateTimeOffset;
                }
                if (string.Equals("NCHAR", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.String;
                }
                if (string.Equals("XML", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.Xml;
                }
                if (string.Equals("DATE", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.Date;
                }
                if (string.Equals("TIME", typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return DbType.Time;
                }
                throw new NotImplementedException("typeName:" + typeName);
            }
            else
            {
                throw new NotImplementedException("Name.Count:" + dataType.Name.Count);
            }
        }
        class StatementVisitor : DumpFragmentVisitor
        {
            private readonly TSqlFragment _toAnalyze;
            private readonly BatchOutputColumnTypeResolver columnTypeResolver;
            private readonly bool resolveColumnType;

            internal readonly List<ProcedureOutputSet> resultHasOutputResultSet = new List<ProcedureOutputSet>();

            internal StatementVisitor(bool resolveColumnType, BatchOutputColumnTypeResolver columnTypeResolver, TSqlFragment toAnalyze) : base(false)
            {
                this.resolveColumnType = resolveColumnType;
                this.columnTypeResolver = columnTypeResolver;
                _toAnalyze = toAnalyze;
            }

            private void DoResolveOutputResultSets(TSqlStatement toAnalyze, List<ProcedureOutputSet> collector)
            {
                StatementVisitor vstor = new StatementVisitor(resolveColumnType, columnTypeResolver, toAnalyze);
                toAnalyze.Accept(vstor);
                int cnt = vstor.resultHasOutputResultSet.Count;
                if (cnt > 0)
                {
                    foreach (ProcedureOutputSet outputSet in vstor.resultHasOutputResultSet)
                    {
                        collector.Add(outputSet);
                    }
                }
            }

            private void DoHasOutputResultSet(StatementList toAnalyze) // try, catch
            {
                StatementVisitor vstor = new StatementVisitor(resolveColumnType, columnTypeResolver, toAnalyze);
                toAnalyze.Accept(vstor);
                int cnt = vstor.resultHasOutputResultSet.Count;
                if (cnt > 0)
                {
                    this.resultHasOutputResultSet.AddRange(vstor.resultHasOutputResultSet);
                }
            }

            public override void ExplicitVisit(UnqualifiedJoin node)
            {
                base.ExplicitVisit(node); // ? CROSS APPLY
            }

            public override void ExplicitVisit(IfStatement node)
            {
                DoResolveOutputResultSets(node.ThenStatement, resultHasOutputResultSet);

                if (node.ElseStatement != null)
                {
                    // skip 'else'  but it can be use for column type discovery
                    if (resultHasOutputResultSet.Count == 0)
                    {
                        // no SELECT in ThenStatement list : maybe THROW? or RAISEERROR
                        DoResolveOutputResultSets(node.ElseStatement, resultHasOutputResultSet);
                    }
                    else
                    {
                        bool hasMissingColumnInfo = this.HasMissingColumnInfo();
                        // apply column types from ELSE branch + (NOT NULL => NULL)
                        //if (hasMissingColumnInfo)
                        {
                            List<ProcedureOutputSet> else_outputResultSets = new List<ProcedureOutputSet>();
                            DoResolveOutputResultSets(node.ElseStatement, else_outputResultSets);
                            if (else_outputResultSets.Count > 0)
                            {
                                MergeMissingColumnInformation(resultHasOutputResultSet, else_outputResultSets);
                            }

                            //if (HasMissingColumnInfo())
                            //{
                            //    // still not possible! if this is a nested IF the it is ok!
                            //}
                        }

                    }
                }
            }

            private bool HasMissingColumnInfo()
            {
                foreach (ProcedureOutputSet xxx in resultHasOutputResultSet)
                {
                    if (xxx.HasMissingColumnInfo())
                    {
                        return true;
                    }
                }

                return false;
            }

            private static void MergeMissingColumnInformation(List<ProcedureOutputSet> resultHasOutputResultSet, List<ProcedureOutputSet> else_outputResultSets)
            {
                if (resultHasOutputResultSet.Count != else_outputResultSets.Count)
                    throw new NotImplementedException("Mismatched output column set count. Expected:" + resultHasOutputResultSet.Count + " Actual:" + else_outputResultSets.Count);

                for (int ixSet = 0; ixSet < resultHasOutputResultSet.Count; ixSet++)
                {
                    ProcedureOutputSet setT = resultHasOutputResultSet[ixSet];
                    ProcedureOutputSet setE = else_outputResultSets[ixSet];
                    string setinfo = "set_" + (ixSet + 1) + "_" + resultHasOutputResultSet.Count;
                    MergeMissingColumnInformation(setinfo, setT, setE);
                }
            }

            private static void MergeMissingColumnInformation(string setinfo, ProcedureOutputSet setT, ProcedureOutputSet setE)
            {
                if (setT.ColumnCount != setT.ColumnCount)
                    throw new NotImplementedException("Mismatched output column count. (" + setinfo + ") Expected:" + setT.ColumnCount + " Actual:" + setE.ColumnCount);

                for (int ixCol = 0; ixCol < setT.ColumnCount; ixCol++)
                {
                    ProcedureOutputColumn colT = setT.ColumnAt(ixCol);
                    ProcedureOutputColumn colE = setE.ColumnAt(ixCol);
                    if (colT.HasMissingInformation)
                    {
                        if (!colE.HasMissingInformation)
                        {
                            colT.ApplyMissingInformation(colE);
                        }
                        else
                        {
                            // still not possible! if this is a nested IF the it is ok!
                        }
                    }
                    else
                    {
                        if (!colT.AllowNull.Value && colE.AllowNull.GetValueOrDefault(true))
                        {
                            // NOT NULL => NULL
                            colT.SetAllowNull();
                        }
                        else
                        {
                            // everything's fine here
                        }
                    }
                }
            }

            public override void ExplicitVisit(TryCatchStatement node)
            {
                DoHasOutputResultSet(node.TryStatements);
                if (node.CatchStatements != null)
                {
                    // skip 'catch' but it can be use for column type discovery
                    if (resultHasOutputResultSet.Count == 0)
                    {
                        // no SELECT in TryStatements list : maybe THROW? or RAISEERROR
                        DoHasOutputResultSet(node.CatchStatements);
                    }
                    else
                    {
                        // apply column types from CATCH
                    }
                }
            }

            public override void ExplicitVisit(SelectStatement node)
            {
                if (node.Into != null)
                    return;

                if (node.QueryExpression is BinaryQueryExpression bqe) // UNION?
                {
                    // premature optimization : without visitor
                    SelectElementVisitor vstorF = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    QuerySpecification qspecF = (QuerySpecification)bqe.FirstQueryExpression;
                    foreach (SelectElement selectEl in qspecF.SelectElements)
                    {
                        selectEl.Accept(vstorF);
                    }
                    if (vstorF.HasOutput)
                    {
                        if (vstorF.ResultOutput.HasMissingColumnInfo())
                        {
                            SelectElementVisitor vstorS = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                            QuerySpecification qspecS = (QuerySpecification)bqe.SecondQueryExpression;
                            foreach (SelectElement selectEl in qspecS.SelectElements)
                            {
                                selectEl.Accept(vstorS);
                            }

                            vstorF.ResultOutput.ApplyMissingInformation(vstorS.ResultOutput);
                        }

                        resultHasOutputResultSet.Add(vstorF.ResultOutput);
                    }

                }
                else
                {
                    SelectElementVisitor vstor = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    QuerySpecification qspec = (QuerySpecification)node.QueryExpression;
                    foreach (SelectElement selectEl in qspec.SelectElements)
                    {
                        selectEl.Accept(vstor);
                    }
                    if (vstor.HasOutput)
                    {
                        resultHasOutputResultSet.Add(vstor.ResultOutput);
                    }
                }


            }


            public override void ExplicitVisit(UpdateStatement node)
            {
                if (node.UpdateSpecification != null && node.UpdateSpecification.OutputClause != null)
                {
                    SelectElementVisitor vstor = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    node.UpdateSpecification.OutputClause.Accept(vstor);
                    if (vstor.HasOutput)
                    {
                        resultHasOutputResultSet.Add(vstor.ResultOutput);
                    }
                }
            }

            public override void ExplicitVisit(InsertStatement node)
            {
                if (node.InsertSpecification != null && node.InsertSpecification.OutputClause != null)
                {
                    SelectElementVisitor vstor = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    node.InsertSpecification.OutputClause.Accept(vstor);
                    if (vstor.HasOutput)
                    {
                        resultHasOutputResultSet.Add(vstor.ResultOutput);
                    }
                }
            }

            public override void ExplicitVisit(DeleteStatement node)
            {
                if (node.DeleteSpecification != null && node.DeleteSpecification.OutputClause != null)
                {
                    SelectElementVisitor vstor = new SelectElementVisitor(resolveColumnType, columnTypeResolver, node);
                    node.DeleteSpecification.OutputClause.Accept(vstor);
                    if (vstor.HasOutput)
                    {
                        resultHasOutputResultSet.Add(vstor.ResultOutput);
                    }
                }
            }
        }

        public static string RemoveTrailingBlockComment(string ddl)
        {
            if (ddl != null && ddl.TrimEnd().EndsWith("*/"))
            {
                int lastBlockStart = ddl.LastIndexOf("/*");
                int lastBlockEnd = ddl.LastIndexOf("*/");
                if (lastBlockStart > 0 && lastBlockEnd > lastBlockStart)
                {
                    string new_ddl = ddl.Substring(0, lastBlockStart);
                    return new_ddl;
                }
                else
                {
                    // block comment start could not be found?
                    return ddl;
                }
            }
            else
            {
                // no trailing block comment
                return ddl;
            }
        }
        private static string[] GetLines(string str, bool removeEmptyLines = false)
        {
            return str.Split(new[] { "\r\n", "\r", "\n" },
                removeEmptyLines ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);
        }
        public static void LoadFunctionOutputColumns(ISchemaMetadataProvider schemaMetadata, IBatchParameterMetadata parameterMetadata, string functionBodyScriptIn, Action<OutputColumnDescriptor> collector)
        {
            string functionBodyScript = RemoveTrailingBlockComment(functionBodyScriptIn);
            if (functionBodyScript.Trim()[0] == '(')
            {
                string[] lines = GetLines(functionBodyScript);
                StringBuilder buffer = new StringBuilder();
                StringWriter swriter = new StringWriter(buffer);
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("--"))
                    {
                        // skip it
                    }
                    else
                    {
                        swriter.WriteLine(line);
                    }
                }
                swriter.Flush();
                functionBodyScript = buffer.ToString();
                //using (var sreader = new StringReader(functionBodyScript))
                //{
                //    string line;
                //    do
                //    {
                //        line = sreader.ReadLine();
                //        if (line != null)
                //        {
                //
                //        }
                //    }
                //    while (line != null);

                int idx = functionBodyScript.IndexOf('(');
                int lastix = functionBodyScript.LastIndexOf(')');

                functionBodyScript = functionBodyScript.Substring(idx + 1, lastix - idx - 2);
                functionBodyScript = functionBodyScript.Trim();
            }
            TSqlFragment sqlF = ScriptDomFacade.Load(new StringReader(functionBodyScript));
            SelectStatement stmt_sel = (SelectStatement)((TSqlScript)sqlF).Batches[0].Statements[0];

            BatchOutputColumnTypeResolver batchResolver = new BatchOutputColumnTypeResolver(schemaMetadata, sqlF, parameterMetadata);
            StatementOutputColumnTypeResolverV2 resolver = new StatementOutputColumnTypeResolverV2(batchResolver, stmt_sel);

            QuerySpecification first = TopQuerySpecification(stmt_sel.QueryExpression);
            foreach (SelectElement se in first.SelectElements)
            {
                if (se is SelectScalarExpression scalarExpr)
                {
                    OutputColumnDescriptor col = resolver.ResolveSelectScalarExpression(scalarExpr);
                    collector(col);
                }
                else
                {
                    throw new NotImplementedException(se.WhatIsThis());
                }
            }
        }

        internal static void ParseViewDefinition()
        {
            throw new NotImplementedException();
        }

        private static QuerySpecification TopQuerySpecification(QueryExpression expr)
        {
            if (expr is QuerySpecification qspec)
                return qspec;
            BinaryQueryExpression bqExpr = (BinaryQueryExpression)expr;
            return TopQuerySpecification(bqExpr.FirstQueryExpression);
        }

        sealed class BatchWithoutParameters : IBatchParameterMetadata
        {
            ColumnTypeMetadata IBatchParameterMetadata.TryGetParameterType(string parameterName)
            {
                return null;
            }
        }


        public static void LoadViewOutputColumns(ISchemaMetadataProvider schemaMetadata, string ddl, Action<OutputColumnDescriptor> collector)
        {
            TSqlFragment sqlF = ScriptDomFacade.Parse(ddl);
            //CreateViewStatement stmt_CreateView = (CreateViewStatement)((TSqlScript)sqlF).Batches[0].Statements[0];
            SelectStatement stmt_sel = (SelectStatement)((TSqlScript)sqlF).Batches[0].Statements[0];

            BatchOutputColumnTypeResolver batchResolver = new BatchOutputColumnTypeResolver(schemaMetadata, stmt_sel, new BatchWithoutParameters());

            StatementOutputColumnTypeResolverV2 resolver = new StatementOutputColumnTypeResolverV2(batchResolver, stmt_sel);

            QuerySpecification first = TopQuerySpecification(stmt_sel.QueryExpression);
            foreach (SelectElement se in first.SelectElements)
            {
                if (se is SelectScalarExpression scalarExpr)
                {
                    OutputColumnDescriptor col = resolver.ResolveSelectScalarExpression(scalarExpr);
                    collector(col);
                }
                else
                {
                    throw new NotImplementedException(se.WhatIsThis());
                }
            }
        }
        class SelectElementVisitor : DumpFragmentVisitor
        {
            private bool hasSetVariable;
            private readonly ProcedureOutputSet outputSet;
            private readonly StatementOutputColumnTypeResolverV2 columnTypeResolver;
            private readonly StatementWithCtesAndXmlNamespaces statement;
            private readonly bool resolveColumnType;

            public SelectElementVisitor(bool resolveColumnType, BatchOutputColumnTypeResolver columnTypeResolver, StatementWithCtesAndXmlNamespaces statement) : base(false)
            {
                this.resolveColumnType = resolveColumnType;
                this.columnTypeResolver = new StatementOutputColumnTypeResolverV2(columnTypeResolver, statement);
                this.outputSet = new ProcedureOutputSet(statement);
                this.statement = statement;
            }

            internal bool HasOutput
            {
                get
                {
                    if (hasSetVariable)
                    {
                        outputSet.Clear();
                    }

                    return outputSet.ColumnCount > 0;
                }
            }
            internal ProcedureOutputSet ResultOutput
            {
                get
                {
                    if (!HasOutput)
                    {
                        throw new NotSupportedException("No output");
                    }

                    return outputSet;
                }
            }

            public override void ExplicitVisit(SelectSetVariable node)
            {
                // no! do not call the base implementation : stop the visiting of the child fragments!
                hasSetVariable = true; // => no outputs
                outputSet.Clear();
            }

            public override void ExplicitVisit(QualifiedJoin node)
            {

            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                if (hasSetVariable)
                {
                    outputSet.Clear();
                }
                else
                {
                    //Console.WriteLine("Resolve type for reference:" + node.AsText());
                    OutputColumnDescriptor columnDbType = resolveColumnType
                        ? columnTypeResolver.ResolveColumnReference(node)
                        : null;
                    if (columnDbType == null)
                    {
                        // put breakpoint here and try again
                        //Console.WriteLine("Type resolve failed for reference:" + node.AsText());
                    }
                    outputSet.AddColumn(new ProcedureOutputColumn(node, columnDbType)); // ColumnReferenceExpression : [a].[attributeid]
                }
            }
            public override void ExplicitVisit(SelectScalarExpression node)
            {
                // IIF(@limitreached = 1, 1, 0)
                if (hasSetVariable)
                {
                    outputSet.Clear();
                }
                else
                {
                    //Console.WriteLine("Resolve type for scalar:" + node.AsText());
                    OutputColumnDescriptor columnDbType = resolveColumnType
                        ? columnTypeResolver.ResolveSelectScalarExpression(node)
                        : null;
                    if (columnDbType == null || columnDbType.ColumnType == null)
                    {
                        // put a breakpoint here
                        //this column cannot be resolved from this query. if this is a part of a IF statemt try ELSE branch.
                    }
                    outputSet.AddColumn(new ProcedureOutputColumn(node, columnDbType)); // SelectScalarExpression
                }
            }
            public override void ExplicitVisit(IdentifierOrValueExpression node)
            {
                // column from constant
                if (hasSetVariable)
                {
                    outputSet.Clear();
                }
                else
                {
                    throw new NotSupportedException(node.AsText());
                    //outputSet.resultFragments.Add(node); // IdentifierOrValueExpression
                }
            }
        }

        internal static bool? HasReturnStatement(ProcedureMetadata procedure_metadata)
        {
            ReturnStatementVisitor vstor = new ReturnStatementVisitor();
            procedure_metadata.BodyFragment.Accept(vstor);
            return vstor.result;
        }

        class ReturnStatementVisitor : TSqlFragmentVisitor
        {
            internal bool result;
            public override void ExplicitVisit(ReturnStatement node)
            {
                result = true;
            }
        }
    }
}
