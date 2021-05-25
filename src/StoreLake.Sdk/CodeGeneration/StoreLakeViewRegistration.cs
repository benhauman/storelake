using System.Collections.Generic;

namespace StoreLake.Sdk.CodeGeneration
{
    internal class StoreLakeViewRegistration
    {
        public string ViewSchema { get; set; }
        public string ViewName { get; set; }
        public string ViewQueryScript { get; set; }

        public List<StoreLakeViewColumnRegistration> Columns { get; set; } = new List<StoreLakeViewColumnRegistration>();
    }
}