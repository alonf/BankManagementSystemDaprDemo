namespace BMSD.Accessors.CheckingAccount.Contracts.Responses;

public class AccountCallbackResponse
{
    public string? ActionName { get; set; }
    public string? ResultMessage { get; set; }
    public bool IsSuccessful { get; set; }
    public string? RequestId { get; set; }
    public string? AccountId { get; set; }

}