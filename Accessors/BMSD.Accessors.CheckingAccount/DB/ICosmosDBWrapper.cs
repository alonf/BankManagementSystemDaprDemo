namespace BMSD.Accessors.CheckingAccount.DB
{
    public interface ICosmosDBWrapper
    {
        Task<IList<AccountTransactionRecord>?> GetAccountTransactionHistoryAsync(string accountId, int numberOfTransactions);
        Task<decimal> GetBalanceAsync(string accountId);
        Task UpdateBalanceAsync(string requestId, string accountId, decimal amount);
        Task<decimal> GetAccountBalanceLowLimitAsync(string accountId);
        Task SetAccountBalanceLowLimitAsync(string accountId, decimal limit);
        Task<AccountInfo> GetAccountInfoAsync(string accountId);
    }
}