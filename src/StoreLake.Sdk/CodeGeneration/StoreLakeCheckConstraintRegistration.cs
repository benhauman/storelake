using System.Collections.Generic;

namespace StoreLake.Sdk.CodeGeneration
{
    internal class StoreLakeCheckConstraintRegistration
    {
        public string CheckConstraintName { get; internal set; }
        public string CheckConstraintSchema { get; internal set; }
        public string DefiningTableName { get; internal set; }
        public string DefiningTableSchema { get; internal set; }
        public List<StoreLakeKeyColumnRegistration> DefiningColumns { get; set; } = new List<StoreLakeKeyColumnRegistration>();

        public string CheckExpressionScript { get; internal set; }
    }
}