using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GingerMintSoft.WorkFlows.WebHook
{
    // internal using: GingerMintSoft.WorkFlows.
    using Communication;

    public static class PaymentStatus
    {
        [FunctionName("PaymentStatus")]
        public static async Task<IActionResult> WebHookPaymentStatus(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "WebHook/PaymentStatus")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var paymentStatus = "not paid";
            var id = req.Query["id"];
            var status = await client.GetStatusAsync(id);

            if (status.RuntimeStatus != OrchestrationRuntimeStatus.Running)
            {
                log.LogInformation($"WebHookPaymentStatus: {status.RuntimeStatus}.");
                return new NotFoundResult();
            }

            if (!req.HasFormContentType) 
                return new OkObjectResult($"Status: {paymentStatus}");

            var form = await req.ReadFormAsync();
            string transactionId = form["id"];

            if (string.IsNullOrEmpty(transactionId))
            {
                await client.RaiseEventAsync(id, "Paid", false);
            }
            else
            {
                var response = await Http.Create().Result.GetAsync($"HelpingHands/Payment/Customer/Transaction/State?paymentId={transactionId}");

                if (response == null) 
                    return new NoContentResult();

                if (!response.IsSuccessStatusCode)
                {
                    log.LogInformation($"[2020.08.20:150240]: {response}.");
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    log.LogInformation($"DepositPayment: {content}.");
                    dynamic objOutput = JsonConvert.DeserializeObject(content);
                    paymentStatus = objOutput.Status.Value == "paid" ? "paid" : "not paid";
                }
                
                if (paymentStatus == "paid")
                    await client.RaiseEventAsync(id, "Paid", true);
                else
                    await client.RaiseEventAsync(id, "Paid", false);
            }

            return new OkObjectResult($"Status: {paymentStatus}");
        }
    }
}
