using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeadOrAlive
{
    [DynamoDBTable("DA_MONITOR")]
    class DaMonitor
    {
        [DynamoDBHashKey]
        [DynamoDBProperty("URL")]
        public string Url { get; set; }

        [DynamoDBProperty("SERVICE_STATUS")]
        public int Status { get; set; }

        [DynamoDBProperty("CHANGE_TIME")]
        public string ChamgeTime { get; set; }

        [DynamoDBProperty("SERVICE_NAME")]
        public string ServiceName { get; set; }
    }
}
