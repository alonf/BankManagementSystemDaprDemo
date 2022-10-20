namespace BMSD.Tests.IntegrationTests.Contracts;

public class AccountTransactionInfo
{
    public string RequestId { get; set; }

    public string CallerId { get; set; }

    public string SchemaVersion { get; set; } = "1.0";

    public string AccountId { get; set; }

    public decimal Amount { get; set; }
}