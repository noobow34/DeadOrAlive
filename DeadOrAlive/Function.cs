using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using SocialOpinionAPI.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace DeadOrAlive
{
    public class Function
    {
        private static readonly AmazonDynamoDBClient dbClient = new(RegionEndpoint.APNortheast1);
        private static readonly HttpClient httpClient = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandlerAsync(ILambdaContext context)
        {
            using var dbContext = new DynamoDBContext(dbClient);
            var monitorTargets = await dbContext.ScanAsync<DaMonitor>(new List<ScanCondition>()).GetRemainingAsync();
            var batchWrite = dbContext.CreateBatchWrite<DaMonitor>();
            foreach (var monitor in monitorTargets)
            {
                context.Logger.LogLine($"{monitor.Url}:{monitor.Status}:{monitor.ChamgeTime}");
                HttpResponseMessage response = null;
                try
                {
                    response = await httpClient.GetAsync(monitor.Url, HttpCompletionOption.ResponseHeadersRead);
                    context.Logger.LogLine($"Status:{response.StatusCode}");
                }
                catch
                {
                    context.Logger.LogLine($"Status:exception");
                }

                if ((response == null || response.StatusCode != System.Net.HttpStatusCode.OK) && monitor.Status == 0)
                {
                    //今回からNGになった
                    monitor.Status = 1;
                    monitor.ChamgeTime = DateTime.Now.ToString();
                    batchWrite.AddPutItem(monitor);
                    await PostToSlackAsync($"【ダウン検知】\n{monitor.ServiceName}\n{monitor.Url}\n{DateTime.Now}");
                    DoTweet($"【自動ツイート】{Environment.NewLine}現在{monitor.ServiceName}が停止しています。復旧までしばらくお待ち下さい。{Environment.NewLine}{DateTime.Now}");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.OK && monitor.Status != 0)
                {
                    //今回からOKになった
                    monitor.Status = 0;
                    monitor.ChamgeTime = DateTime.Now.ToString();
                    batchWrite.AddPutItem(monitor);
                    await PostToSlackAsync($"【アップ検知】\n{monitor.Url}\n{monitor.ServiceName}\n{DateTime.Now}");
                    DoTweet($"【自動ツイート】{Environment.NewLine}現在{monitor.ServiceName}が復旧しました。{Environment.NewLine}{DateTime.Now}");
                }
            }
            await batchWrite.ExecuteAsync();
        }

        /// <summary>
        /// LINE botに通知する
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task PostToSlackAsync(string message)
        {
            using HttpClient client = new();
            var webhookUrl = Environment.GetEnvironmentVariable("WEBHOOK");
            var payload = new { text = message };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _ = await client.PostAsync(webhookUrl, content);
        }

        /// <summary>
        /// ツイートする
        /// </summary>
        /// <param name="message"></param>
        private void DoTweet(string message)
        {
            var client = new SocialOpinionAPI.Clients.TweetsClient(new OAuthInfo
            {
                AccessToken = Environment.GetEnvironmentVariable("TWITTER_ACCESS_TOKEN"),
                AccessSecret = Environment.GetEnvironmentVariable("TWITTER_ACCESS_SECRET"),
                ConsumerKey = Environment.GetEnvironmentVariable("TWITTER_CONSUMER_KEY"),
                ConsumerSecret = Environment.GetEnvironmentVariable("TWITTER_CONSUMER_SECRET")
            });

            client.PostTweet(message);
        }
    }
}
