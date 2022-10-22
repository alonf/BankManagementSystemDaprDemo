using Newtonsoft.Json;

namespace BMSD.Accessors.CheckingAccount.DB
{
    public class AccountTransactionRecord
    {
        [JsonProperty("id")]
        public string? Id { get; set; }
        [JsonProperty("accountId")]
        public string? AccountId { get; set; }
        [JsonProperty("transactionAmount")]
        public decimal TransactionAmount { get; set; }
        [JsonProperty("transactionTime")]
        public DateTimeOffset TransactionTime { get; set; }
    }
}
