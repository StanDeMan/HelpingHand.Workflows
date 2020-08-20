using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace GingerMintSoft.WorkFlows
{
    using Newtonsoft.Json;
    // internal: GingerMintSoft.WorkFlows.
    using Payment;

    public static class FunctionTest
    {
        [FunctionName("PaymentTransaction")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var requestBody = Convert.ToString(context.GetInput<dynamic>());
            var output = await context.CallActivityAsync<string>("DepositPayment", requestBody);
                
            return output;
        }

        [FunctionName("DepositPayment")]
        public static async Task<string> Payment([ActivityTrigger] string request, ILogger log)
        {
#if DEBUG
            const string baseUri = "http://localhost:52719";
#else
            const string baseUri = "https://helpinghandsservices.azurewebsites.net";
#endif
            var client = await Http.Create(baseUri);

            var content = "";
            var response = await client.PostAsync("HelpingHands/Payment/Customer/Execute/Banktransfer", request);

            if (!response.IsSuccessStatusCode)
            {
                log.LogInformation($"[2020.08.20:150240]: {response}.");
            }
            else
            {
                content = await response.Content.ReadAsStringAsync();
                log.LogInformation($"DepositPayment: {content}.");
            }

            return $"{content}";
        }

        [FunctionName("StartBanktransfer")]
        public static async Task<IActionResult> StartBanktransfer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic request = JsonConvert.DeserializeObject(requestBody);

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync("PaymentTransaction", null, request);

            log.LogInformation($"Started payment transaction orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}