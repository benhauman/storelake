namespace StoreLake.Test
{
    using System.Data;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using StoreLake.Sdk.SqlDom;

    [TestClass]
    public class CheckConstraintGenerator_Tests
    {
        public TestContext TestContext { get; set; }
        [TestMethod]
        public void CK_hlspattributevaluedecimal_valuescale()
        {
            DataTable table = new DataTable() { TableName = "tst" };
            Do(table, "(LEN(RTRIM(REPLACE(SUBSTRING(PARSENAME(ISNULL([attributevalue], 0), 1), 1, 10), N'0', N''))) <= [attributescale])");
        }

        private void Do(DataTable table, string definition)
        {

            BooleanExpressionGenerator.BuildFromCheckConstraintDefinition("dbo", table, TestContext.TestName, definition, out bool hasError, out string errorText);
            //LEN Assert.IsFalse(hasError, errorText);
        }

        [TestMethod]
        public void CK_hlspprocesscmdbtaskflow_validate_cmdb()
        {
            DataTable table = new DataTable() { TableName = "tst" };
            Do(table, "CONVERT(BIT,[dbo].[hlspdefinition_check_cmdb_assignment]([sptaskid],[cmdbflowid]))=(0)");
        }

        [TestMethod]
        public void CK_zztest_validate_thumbnailhash()
        {
            DataTable table = new DataTable() { TableName = "zztest" };
            Do(table, "CONVERT(BIT,[dbo].[hlspdefinition_check_cmdb_assignment]([sptaskid],[cmdbflowid]))=(0)");
        }
    }
}
