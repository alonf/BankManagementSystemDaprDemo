using System.Text.Json;
using System.Text;
using Xunit.Abstractions;
using BMSD.Tests.IntegrationTests.Contracts;

namespace BMSD.Tests.IntegrationTests
{
    public class IntegrationTest
    {
        private readonly HttpClient _httpClient;
        private readonly ISignalRWrapper _signalR;

        public IntegrationTest(IHttpClientFactory httpClientFactory, ISignalRWrapperFactory signalRWrapperFactory, ITestOutputHelper testOutputHelper)
        {
            _signalR = signalRWrapperFactory.Create(testOutputHelper);
            _httpClient = httpClientFactory.CreateClient("IntegrationTest");
        }
        private JsonSerializerOptions SerializeOptions => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private async Task<string> RegisterTestAccountAsync(string email)
        {
            var customerRegistrationInfo = new CustomerRegistrationInfo()
            {
                CallerId = "Teller1",
                RequestId = Guid.NewGuid().ToString(),
                SchemaVersion = "1.0",
                FirstName = "John",
                LastName = "Doe",
                Address = "123 Main St",
                City = "Any Town",
                State = "Any State",
                ZipCode = "90210",
                Email = email,
                PhoneNumber = "+1-555-555-5555",
            };

            var json = JsonSerializer.Serialize(customerRegistrationInfo, SerializeOptions);
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            await _signalR.StartSignalR();
            var response = await _httpClient.PostAsync("RegisterCustomer", data);
            Assert.NotNull(response);
            Assert.Equal(200, (int)response.StatusCode);

            var result = await _signalR.WaitForSignalREventWithConditionAsync(20, messages=>
                messages.Where(m => m.RequestId == customerRegistrationInfo.RequestId).Any());
            
            Assert.True(result);
            var accountId = _signalR.Messages.Where(e => e.RequestId == customerRegistrationInfo.RequestId).Select(e => e.AccountId).FirstOrDefault();
            Assert.NotNull(accountId);

            var testResponse = await _httpClient.GetAsync($"GetAccountId?email={customerRegistrationInfo.Email}");
            Assert.NotNull(testResponse);
            Assert.Equal(200, (int)response.StatusCode);
            var responseJson = await testResponse.Content.ReadAsStringAsync();

            var accountArray = JsonSerializer.Deserialize<AccountIdInfo>(responseJson, SerializeOptions);
            Assert.NotNull(accountArray);
            Assert.NotEmpty(accountArray.AccountIds);
            Assert.Contains(accountId, accountArray.AccountIds);

            return accountId;
        }

        [Fact]
        public async Task TestRegisterAccount()
        {
            await RegisterTestAccountAsync("jhon@email.com");
        }


        private async Task<BalanceInfo> GetAccountBalanceAsync(string accountId)
        {
            var response = await _httpClient.GetAsync($"GetAccountBalance?accountId={accountId}");
            Assert.NotNull(response);
            Assert.Equal(200, (int)response.StatusCode);
            var responseJson = await response.Content.ReadAsStringAsync();

            var balanceInfo = JsonSerializer.Deserialize<BalanceInfo>(responseJson, SerializeOptions);
            Assert.NotNull(balanceInfo);

            return balanceInfo;
        }

        [Fact]
        public async Task TestDeposit()
        {
            var accountId = await RegisterTestAccountAsync("deposit@email.com");

            var startBalanceInfo = await GetAccountBalanceAsync(accountId);

            var accountTransactionInfo = new AccountTransactionInfo()
            {
                CallerId = "Teller1",
                RequestId = Guid.NewGuid().ToString(),
                SchemaVersion = "1.0",
                AccountId = accountId,
                Amount = 100
            };

            var json = JsonSerializer.Serialize(accountTransactionInfo, SerializeOptions);
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            await _signalR.StartSignalR();
            var response = await _httpClient.PostAsync("Deposit", data);
            Assert.NotNull(response);
            Assert.Equal(200, (int)response.StatusCode);

            var result = await _signalR.WaitForSignalREventWithConditionAsync(20, messages =>
                messages.Where(m => m.RequestId == accountTransactionInfo.RequestId).Any());
            Assert.True(result);
            
            var endBalanceInfo = await GetAccountBalanceAsync(accountId);
            Assert.Equal(startBalanceInfo.Balance + 100, endBalanceInfo.Balance);
        }

        [Fact]
        public async Task TestSuccessWithdraw()
        {
            var accountId = await RegisterTestAccountAsync("withdraw@email.com");

            var startBalanceInfo = await GetAccountBalanceAsync(accountId);

            var accountTransactionInfo = new AccountTransactionInfo()
            {
                CallerId = "Teller1",
                RequestId = Guid.NewGuid().ToString(),
                SchemaVersion = "1.0",
                AccountId = accountId,
                Amount = 100
            };

            var json = JsonSerializer.Serialize(accountTransactionInfo, SerializeOptions);
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            await _signalR.StartSignalR();
            var response = await _httpClient.PostAsync("Withdraw", data);
            Assert.NotNull(response);
            Assert.Equal(200, (int)response.StatusCode);

            var result = await _signalR.WaitForSignalREventWithConditionAsync(20, messages =>
                messages.Where(m => m.RequestId == accountTransactionInfo.RequestId).Any());
            Assert.True(result);

            Assert.True(_signalR.Messages.Where(e => e.RequestId == accountTransactionInfo.RequestId).First().IsSuccessful);

            var endBalanceInfo = await GetAccountBalanceAsync(accountId);

            Assert.Equal(startBalanceInfo.Balance - 100, endBalanceInfo.Balance);

        }

        [Fact]
        public async Task TestFailedWithdraw()
        {
            var accountId = await RegisterTestAccountAsync("withdraw@email.com");

            var startBalanceInfo = await GetAccountBalanceAsync(accountId);

            var accountTransactionInfo = new AccountTransactionInfo()
            {
                CallerId = "Teller1",
                RequestId = Guid.NewGuid().ToString(),
                SchemaVersion = "1.0",
                AccountId = accountId,
                Amount = 1000000
            };

            var json = JsonSerializer.Serialize(accountTransactionInfo, SerializeOptions);
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("Withdraw", data);
            Assert.NotNull(response);
            Assert.Equal(400, (int)response.StatusCode);

            var endBalanceInfo = await GetAccountBalanceAsync(accountId);
            Assert.Equal(startBalanceInfo.Balance, endBalanceInfo.Balance);
        }

        [Fact]
        public async Task TestTransactionHistory()
        {
            var accountId = await RegisterTestAccountAsync("transaction@email.com");

            for (int i = 1; i <= 6; ++i)
            {
                var accountTransactionInfo = new AccountTransactionInfo()
                {
                    CallerId = "Teller1",
                    RequestId = Guid.NewGuid().ToString(),
                    SchemaVersion = "1.0",
                    AccountId = accountId,
                    Amount = i * 100
                };

                var json = JsonSerializer.Serialize(accountTransactionInfo, SerializeOptions);
                var data = new StringContent(json, Encoding.UTF8, "application/json");

                await _signalR.StartSignalR();
                var response = await _httpClient.PostAsync(i % 2 == 0 ? "Deposit" : "Withdraw", data);
                Assert.NotNull(response);
                Assert.Equal(200, (int)response.StatusCode);

                var result = await _signalR.WaitForSignalREventWithConditionAsync(20, messages =>
                    messages.Where(m => m.RequestId == accountTransactionInfo.RequestId).Any());
                Assert.True(result);
                Assert.True(_signalR.Messages.Where(e => e.RequestId == accountTransactionInfo.RequestId).First().IsSuccessful);
            }

            var responseHistory = await _httpClient.GetAsync($"GetAccountTransactionHistory?accountId={accountId}");
            var transactionHistory = JsonSerializer.Deserialize<AccountTransactionResponse[]>(await responseHistory.Content.ReadAsStringAsync(), SerializeOptions);
            Assert.NotNull(transactionHistory);
            Assert.Equal(6, transactionHistory.Length);
            Assert.Equal(300, transactionHistory.Sum(e => e.TransactionAmount));
        }
    }
}