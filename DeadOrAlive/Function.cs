using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using CoreTweet;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace DeadOrAlive
{
    public class Function
    {
        private static readonly AmazonDynamoDBClient dbClient = new AmazonDynamoDBClient(RegionEndpoint.APNortheast1);
        private static readonly HttpClient httpClient = new HttpClient();
        private Tokens tokens = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandlerAsync(ILambdaContext context)
        {

            using (var dbContext = new DynamoDBContext(dbClient))
            {
                var monitorTargets = await dbContext.ScanAsync<DaMonitor>(new List<ScanCondition>()).GetRemainingAsync();
                var batchWrite = dbContext.CreateBatchWrite<DaMonitor>();
                foreach (var monitor in monitorTargets)
                {
                    context.Logger.LogLine($"{monitor.Url}:{monitor.Status}:{monitor.ChamgeTime}");
                    var response = await httpClient.GetAsync(monitor.Url, HttpCompletionOption.ResponseHeadersRead);
                    context.Logger.LogLine($"Status:{response.StatusCode}");

                    if(response.StatusCode != System.Net.HttpStatusCode.OK && monitor.Status == 0)
                    {
                        //今回からNGになった
                        monitor.Status = 1;
                        monitor.ChamgeTime = DateTime.Now.ToString();
                        batchWrite.AddPutItem(monitor);
                        await PushLineNotifyAsync($"【ダウン検知】\n{monitor.ServiceName}\n{monitor.Url}\n{DateTime.Now.ToString()}");
                        DoTweet($"【自動ツイート】{Environment.NewLine}現在{monitor.ServiceName}が停止しています。復旧までしばらくお待ち下さい。{Environment.NewLine}{DateTime.Now.ToString()}");
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.OK && monitor.Status != 0)
                    {
                        //今回からOKになった
                        monitor.Status = 0;
                        monitor.ChamgeTime = DateTime.Now.ToString();
                        batchWrite.AddPutItem(monitor);
                        await PushLineNotifyAsync($"【アップ検知】\n{monitor.Url}\n{monitor.ServiceName}\n{DateTime.Now.ToString()}");
                        DoTweet($"【自動ツイート】{Environment.NewLine}現在{monitor.ServiceName}が復旧しました。{Environment.NewLine}{DateTime.Now.ToString()}");
                    }
                }
                await batchWrite.ExecuteAsync();
            }
        }

        /// <summary>
        /// LINE Notifyに通知する
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task PushLineNotifyAsync(string message)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "message", message }
            });

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("LINE_TOKEN"));
            var result = await httpClient.PostAsync("https://notify-api.line.me/api/notify", content);
        }

        /// <summary>
        /// ツイートする
        /// </summary>
        /// <param name="message"></param>
        private void DoTweet(string message)
        {
            if(tokens == null)
            {
                tokens = Tokens.Create(Environment.GetEnvironmentVariable("TWITTER_API_KEY")
                                           , Environment.GetEnvironmentVariable("TWITTER_API_SECRET")
                                           , Environment.GetEnvironmentVariable("TWITTER_ACCESS_KEY")
                                           , Environment.GetEnvironmentVariable("TWITTER_ACCESS_SECRET"));
            }

            tokens.Statuses.Update(status => message);
        }
    }
}
