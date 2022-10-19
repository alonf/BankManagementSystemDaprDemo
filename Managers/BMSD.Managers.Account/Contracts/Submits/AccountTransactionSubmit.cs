namespace BMSD.Managers.Account.Contracts.Submits
{
    internal class AccountTransactionSubmit
    {
        public string? RequestId { get; set; }

        public string? CallerId { get; set; }

        public string? SchemaVersion { get; set; } 

        public string? AccountId { get; set; }

        public decimal Amount { get; set; }
    }
}