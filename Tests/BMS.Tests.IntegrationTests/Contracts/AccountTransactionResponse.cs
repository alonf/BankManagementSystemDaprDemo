namespace BMSD.Tests.IntegrationTests.Contracts;

public class AccountTransactionResponse
{
    public string AccountId { get; set; }

    public decimal TransactionAmount { get; set; }

    public DateTimeOffset TransactionTime { get; set; }
}