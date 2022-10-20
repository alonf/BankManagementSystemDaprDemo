using System.ComponentModel.DataAnnotations;
using Xunit.Sdk;

namespace BMSD.Tests.IntegrationTests.Contracts;

public class CustomerInfo
{
    public string AccountId { get; set; }

    public string SchemaVersion { get; set; }

    public string FullName { get; set; }

    public string Email { get; set; }

    public string PhoneNumber { get; set; }

    public string FullAddress { get; set; }
}