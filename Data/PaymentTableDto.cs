using Microsoft.WindowsAzure.Storage.Table;

namespace GingerMintSoft.WorkFlows.Data
{
    public class PaymentTableDto : TableEntity
    {
        public PaymentTableDto(string workFlowId, string jsonState)
        {
            PartitionKey = "State";
            RowKey = workFlowId;
            State = jsonState;
        }

        public PaymentTableDto() {}
        public string State { get; set; }
    }
}
