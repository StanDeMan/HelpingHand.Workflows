namespace GingerMintSoft.WorkFlows.BusinessLogic
{
    public class EditRequest
    {
        public string InstanceId { get; set; }
        public string TaskId { get; set; }

        public EditRequest(string instanceId, string taskId)
        {
            InstanceId = instanceId;
            TaskId = taskId;
        }

        public dynamic Enhance(dynamic paymentRequest)
        {
#if DEBUG
            paymentRequest.webhookurl = $@"http://6b788eed359a.ngrok.io/api/WebHook/PaymentStatus?id={InstanceId}";
            paymentRequest.redirecturl = $@"http://localhost:52719/taskstatus/{TaskId}";
#else
            paymentRequest.webhookurl = $@"https://gingermintsoftworkflows.azurewebsites.net/api/WebHook/PaymentStatus?id={InstanceId}";
            paymentRequest.redirecturl = $@"https://www.werebuzy.com/taskstatus/{TaskId}";
#endif
            return paymentRequest;
        }
    }
}
