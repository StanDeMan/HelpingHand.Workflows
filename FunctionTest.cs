using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GingerMintSoft.WorkFlows
{
    // internal: GingerMintSoft.WorkFlows.
    using Payment;

    public static class FunctionTest
    {
        [FunctionName("FunctionTest")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>
            {
                await context.CallActivityAsync<string>("FunctionTestHello", "Tokyo"),
                //await context.CallActivityAsync<string>("FunctionTestHello", "Seattle"),
            };

            return outputs;
        }

        [FunctionName("FunctionTestHello")]
        public static async Task<string> SayHello([ActivityTrigger] string name, ILogger log)
        {
            const string baseUri = "http://localhost:52719";
            //const string baseUri = "https://helpinghandsservices.azurewebsites.net";

            var client = await Http.Create(baseUri);

            var response = await client.GetAsync("HelpingHands/Users");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(response.StatusCode);
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine(JArray.Parse(content));
            }

            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName("FunctionTestHttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync("FunctionTest", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}