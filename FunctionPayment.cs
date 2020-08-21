using System;
using System.IO;
using System.Threading;
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

    public static class FunctionPayment
    {
        [FunctionName("CheckPayment")]
        public static async Task<string> CheckPayment(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var output = await context.CallActivityAsync<string>("CheckPaymentStatus", context.GetInput<string>());
                
            log.LogInformation($"CheckPayment: {output}");
            return output;
        }

        [FunctionName("PaymentTransaction")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var requestBody = Convert.ToString(context.GetInput<dynamic>());
            var output = await context.CallActivityAsync<string>("DepositPayment", requestBody);
            
            var objOutput = JsonConvert.DeserializeObject(output);

            if (objOutput.Status.Value != "open") 
                return output;

            using (var timeout = new CancellationTokenSource())
            {
                var moderationDeadline = context.CurrentUtcDateTime.AddMinutes(20);
                var durableTimeout = context.CreateTimer(moderationDeadline, timeout.Token);
                var moderatedEvent = context.WaitForExternalEvent<bool>("Payed");
 
                if (moderatedEvent == await Task.WhenAny(moderatedEvent, durableTimeout))
                {
                    timeout.Cancel();
 
                    var isPayed = moderatedEvent.Result;

                    log.LogInformation(isPayed
                        ? "Payed"
                        : "Not payed");
                }
                else
                {
                    log.LogInformation("Timed out");
                }
            }
 
            log.LogInformation("************** Orchestration complete ********************");
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
            var httpClient = await Http.Create(baseUri);

            var content = "";
            var response = await httpClient.PostAsync("HelpingHands/Payment/Customer/Execute/Banktransfer", request);

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

        [FunctionName("CheckPaymentStatus")]
        public static async Task<string> CheckPaymentStatus([ActivityTrigger] string paymentId, ILogger log)
        {
            var paid = "not paid";
#if DEBUG
            const string baseUri = "http://localhost:52719";
#else
            const string baseUri = "https://helpinghandsservices.azurewebsites.net";
#endif
            var httpClient = await Http.Create(baseUri);

            var response = await httpClient.GetAsync($"HelpingHands/Payment/Customer/Transaction/State?paymentId={paymentId}");

            if (!response.IsSuccessStatusCode)
            {
                log.LogInformation($"[2020.08.20:150240]: {response}.");
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                log.LogInformation($"DepositPayment: {content}.");

                dynamic objOutput = JsonConvert.DeserializeObject(content);

                paid = objOutput.Status.Value == "paid" ? "paid" : "not paid";
            }

            return paid;
        }

        [FunctionName("StartBanktransfer")]
        public static async Task<IActionResult> StartBanktransfer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic request = JsonConvert.DeserializeObject(requestBody);

            var instanceId = Guid.NewGuid().ToString();

            // Function input comes from the request content.
            await starter.StartNewAsync("PaymentTransaction", instanceId, request);

            log.LogInformation($"Started payment transaction orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("WebHookPaymentStatus")]
        public static async Task<IActionResult> WebHookPaymentStatus(
            [HttpTrigger(AuthorizationLevel.Function, "get")]HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var paidStatus = "not paid";

            var id = req.Query["id"];
 
            var status = await client.GetStatusAsync(id);

            if (status.RuntimeStatus != OrchestrationRuntimeStatus.Running)
            {
                log.LogInformation($"WebHookPaymentStatus: {status.RuntimeStatus}.");
                return new NotFoundResult();
            }

            if (!req.HasFormContentType) 
                return new OkObjectResult($"Status: {paidStatus}");

            var form = await req.ReadFormAsync();
            string transactionId = form["id"];

            if (string.IsNullOrEmpty(transactionId))
            {
                await client.RaiseEventAsync(id, "Payed", false);
            }
            else
            {
                paidStatus = await client.StartNewAsync("CheckPayment", transactionId);

                if (paidStatus == "paid")
                    await client.RaiseEventAsync(id, "Payed", true);
                else
                    await client.RaiseEventAsync(id, "Payed", false);
            }

            return new OkObjectResult($"Status: {paidStatus}");
        }
    }
}