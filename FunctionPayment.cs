using System;
using System.Dynamic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GingerMintSoft.WorkFlows.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GingerMintSoft.WorkFlows
{
    // internal using: GingerMintSoft.WorkFlows.
    using Communication;

    public static class FunctionPayment
    {
        [FunctionName("DepositPayment")]
        public static async Task<string> Payment([ActivityTrigger] string request, ILogger log)
        {
            var content = "";
            var response = await Http.Create()
                .Result.PostAsync("HelpingHands/Payment/Customer/Execute/Banktransfer", request);

            if (response == null) return content;

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

        [FunctionName("PaymentStatus")]
        public static async Task<IActionResult> WebHookPaymentStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "WebHook/PaymentStatus")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var paymentStatus = "not paid";

            // workflow id
            var id = req.Query["id"];
            var status = await client.GetStatusAsync(id);

            if (status.RuntimeStatus != OrchestrationRuntimeStatus.Running)
            {
                log.LogInformation($"WebHook/PaymentStatus: {status.RuntimeStatus}.");
                return new NotFoundResult();
            }

            if (!req.HasFormContentType) 
            {
                await client.RaiseEventAsync(id, "Paid", false);
            }

            var form = await req.ReadFormAsync();
            string paymentId = form["id"];

            if (string.IsNullOrEmpty(paymentId))
            {
                await client.RaiseEventAsync(id, "Paid", false);
            }
            else
            {
                string content;
                HttpResponseMessage response;
                var httpClient = Http.Create().Result;

                try
                {
                    response = await httpClient
                        .GetAsync($"HelpingHands/Payment/Customer/Transaction/State?paymentId={paymentId}");

                    response.EnsureSuccessStatusCode();
                    content = await response.Content.ReadAsStringAsync();
                    log.LogInformation($"DepositPayment: {content}.");
                }
                catch (Exception e)
                {
                    log.LogError($"[2020.08.20:150240]: {e}.");
                    return new NoContentResult();
                }

                dynamic objContent = JsonConvert.DeserializeObject(content);
                paymentStatus = objContent.Status.Value == "paid" ? "paid" : "not paid";
                string taskId = objContent.Metadata.TaskId.Value;

                if (!string.IsNullOrEmpty(taskId) &&
                    !string.IsNullOrEmpty(paymentStatus) &&
                    paymentStatus == "paid")
                {
                    UserTaskDto task;

                    try
                    {
                        response = await httpClient
                            .GetAsync($"HelpingHands/Users/Tasks/{taskId}");

                        response.EnsureSuccessStatusCode();
                        var taskResult = response.Content.ReadAsStringAsync().Result;
                        task = JsonConvert.DeserializeObject<UserTaskDto>(taskResult);
                    }
                    catch (Exception e)
                    {
                        log.LogError($"[2020.08.23:145319]: {e}.");
                        return new NotFoundResult();
                    }

                    try
                    {
                        task.Payed = true;
                        task.TaskStatusTypeId = 6; // Geschlossen
                        var jsonTask = JsonConvert.SerializeObject(task);

                        response = await httpClient
                            .PutAsync($"HelpingHands/Users/Tasks/{taskId}", jsonTask);

                        response.EnsureSuccessStatusCode();
                        //var taskUpdateResult = response.Content.ReadAsStringAsync().Result;
                    }
                    catch (Exception e)
                    {
                        log.LogError($"[2020.08.23:161144]: {e}.");
                        return new NotFoundResult();
                    }

                    await client.RaiseEventAsync(id, "Paid", true);
                }
                else
                {
                    await client.RaiseEventAsync(id, "Paid", false);
                }
            }

            return new OkObjectResult($"Status: {paymentStatus}");
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
            paymentRequest.webhookurl = $@"http://95aa4edfeaf5.ngrok.io/api/WebHook/PaymentStatus?id={instanceId}";
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