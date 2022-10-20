using System.Text.Json.Serialization;

namespace BMSD.Accessors.CheckingAccount.DB
{
    public class AccountTransactionRecord
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("accountId")]
        public string? AccountId { get; set; }
        [JsonPropertyName("transactionAmount")]
        public decimal TransactionAmount { get; set; }
        [JsonPropertyName("transactionTime")]
        public DateTimeOffset TransactionTime { get; set; }
    }
}
