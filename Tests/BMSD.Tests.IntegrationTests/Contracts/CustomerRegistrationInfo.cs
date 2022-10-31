namespace BMSD.Tests.IntegrationTests.Contracts;

public class CustomerRegistrationInfo
{
    public string RequestId { get; set; }

    public string CallerId { get; set; }

    public string SchemaVersion { get; set; } = "1.0";

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Email { get; set; }

    public string PhoneNumber { get; set; }

    public string Address { get; set; }

    public string City { get; set; }

    public string State { get; set; }

    public string ZipCode { get; set; }
}
