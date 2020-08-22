using System;
using System.Dynamic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GingerMintSoft.WorkFlows.Workflow
{
    public static class FunctionPayment
    {
        [FunctionName("PaymentTransaction")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, 
            ILogger log)
        {
            var statusPayment = "Not paid";

            var requestBody = Convert.ToString(context.GetInput<dynamic>());
            var output = await context.CallActivityAsync<string>("DepositPayment", requestBody);

            if (output == null) return statusPayment;
            var outputObj = JsonConvert.DeserializeObject(output);
            if (outputObj.Status.Value != "open") return statusPayment;

            using (var timeout = new CancellationTokenSource())
            {
                var moderationDeadline = context.CurrentUtcDateTime.AddHours(24);
                var durableTimeout = context.CreateTimer(moderationDeadline, timeout.Token);
                var moderatedEvent = context.WaitForExternalEvent<bool>("Paid");
 
                if (moderatedEvent == await Task.WhenAny(moderatedEvent, durableTimeout))
                {
                    timeout.Cancel();
 
                    var isPayed = moderatedEvent.Result;

                    statusPayment = isPayed 
                        ? "Paid" 
                        : "Not paid";

                    log.LogInformation(isPayed
                        ? "Paid"
                        : "Not paid");
                }
                else
                {
                    log.LogInformation("Timed out");
                }
            }
 
            log.LogInformation("************** Orchestration complete ********************");
            
            return statusPayment;
        }

        [FunctionName("StartBanktransfer")]
        public static async Task<IActionResult> StartBanktransfer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var instanceId = Guid.NewGuid().ToString();

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic paymentRequest = JsonConvert.DeserializeObject<ExpandoObject>(requestBody);
#if DEBUG
            paymentRequest.webhookurl = $@"http://9ee4173063fe.ngrok.io/api/WebHook/PaymentStatus?id={instanceId}";
#else
            paymentRequest.webhookurl = $@"https://gingermintsoftworkflows.azurewebsites.net/api/WebHook/PaymentStatus?id={instanceId}";
#endif
            var request = JsonConvert.SerializeObject(paymentRequest);

            // Function input comes from the request content.
            await starter.StartNewAsync("PaymentTransaction", instanceId, request);
            log.LogInformation($"Started payment transaction orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}