using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace GingerMintSoft.WorkFlows.Activity
{
    // internal using: GingerMintSoft.WorkFlows.
    using Communication;

    public static partial class Activity
    {
        [FunctionName("DepositPayment")]
        public static async Task<string> Payment([ActivityTrigger] string request, ILogger log)
        {
            var content = "";
            var httpClient = await Http.Create();
            var response = await httpClient.PostAsync("HelpingHands/Payment/Customer/Execute/Banktransfer", request);

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
    }
}
