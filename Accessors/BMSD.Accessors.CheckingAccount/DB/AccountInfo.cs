using System.Text.Json.Serialization;

namespace BMSD.Accessors.CheckingAccount.DB
{
    public class AccountInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; } //account id
        [JsonPropertyName("accountBalance")]
        public decimal AccountBalance { get; set; }
        [JsonPropertyName("overdraftLimit")]
        public decimal OverdraftLimit { get; set; }
        [JsonPropertyName("accountTransactions")]
        public List<string> AccountTransactions { get; set; } = new List<string>();
        [JsonPropertyName("_etag")]
        public string? ETag { get; set; }
        [JsonPropertyName("_self")]
        public string? Self { get; set; }

    }
}
