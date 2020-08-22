using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GingerMintSoft.WorkFlows.Activity
{
    // internal using: GingerMintSoft.WorkFlows.
    using Payment;

    public static partial class Activity
    {
        [FunctionName("CheckPaymentStatus")]
        public static async Task<string> CheckPaymentStatus([ActivityTrigger] string paymentId, ILogger log)
        {
            var paid = "not paid";

            var httpClient = await Http.Create();

            var response = await httpClient.GetAsync($"HelpingHands/Payment/Customer/Transaction/State?paymentId={paymentId}");

            if (response == null) return paid;

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
    }
}