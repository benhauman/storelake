using Dibix.TestStore.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dibix.TestStore.Demo
{
    public static class DemoDatabaseExtensions_x1
    {
        public static StoreLakeDatabaseTable<DemoTestDatabase, ChangeQueueEntity> hlsyschangesqueue(this DemoTestDatabase db)
        {
            return new StoreLakeDatabaseTable<DemoTestDatabase, ChangeQueueEntity>(db);
        }
        public static StoreLakeDatabaseTable<DemoTestDatabase, TestAgentEntity> hlsysagent(this DemoTestDatabase db)
        {
            return new StoreLakeDatabaseTable<DemoTestDatabase, TestAgentEntity>(db);
        }
    }

    public sealed class ChangeQueueEntity : StoreLakeDatabaseEntity<Guid, DemoTestDatabase> // see 'hlsyschangesqueue' / 'hlsyschangesrequestsqueue'
    {
        public Guid RequestId { get; set; }
        public int State { get; set; }
        public DateTime RegistrationTime { get; set; }
        public int Owner { get; set; }
        public string Subject { get; set; }
        public string Request { get; set; }

        public ChangeQueueEntity() : base()
        {

        }

        public override Guid PK()
        {
            return RequestId;
        }

        public override void ValidateDatabaseEntity(DemoTestDatabase db)
        {
            base.ValidateDatabaseEntity(db);
            if (RequestId == Guid.Empty)
                throw new InvalidOperationException("RequestId is empty");
        }
    }

    public sealed class TestAgentEntity : StoreLakeDatabaseEntity<int, DemoTestDatabase> // 'hlsysagent'
    {
        public TestAgentEntity()
        {

        }
        public override int PK()
        {
            return agentid;
        }
        public int agentid { get { return base.GetValueInt32(); } set { base.SetValueInt32(value); } }
        public string name { get { return base.GetValueString(); } set { base.SetValueString(value); } }
        public string description { get { return base.GetValueString(); } set { base.SetValueString(value); } }
        public short active { get { return base.GetValueInt16(); } set { base.SetValueInt16(value); } }
    }
}
