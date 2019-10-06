using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace DeadOrAlive
{
    public class Function
    {
        private static readonly AmazonDynamoDBClient Client = new AmazonDynamoDBClient(RegionEndpoint.APNortheast1);
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task FunctionHandlerAsync(ILambdaContext context)
        {
            using (var dbContext = new DynamoDBContext(Client))
            {
                var batchGet = await dbContext.ScanAsync<DaMonitor>(new List<ScanCondition>()).GetRemainingAsync();
            }
        }
    }
}
