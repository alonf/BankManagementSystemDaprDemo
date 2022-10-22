using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace BMSD.Accessors.CheckingAccount.DB
{
    public class AccountInfo
    {
        [JsonProperty("id")]
        public string? Id { get; set; } //account id
        [JsonProperty("bankId")]
        public string? BankId { get; set; } = "The Bank";
        [JsonProperty("accountBalance")]
        public decimal AccountBalance { get; set; }
        [JsonProperty("overdraftLimit")]
        public decimal OverdraftLimit { get; set; }
        [JsonProperty("accountTransactions")]
        public List<string> AccountTransactions { get; set; } = new List<string>();
        [JsonProperty("_etag")]
        public string? ETag { get; set; }
        [JsonProperty("_self")]
        public string? Self { get; set; }
    }
}
