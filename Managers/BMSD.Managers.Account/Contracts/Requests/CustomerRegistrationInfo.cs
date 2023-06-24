using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace BMSD.Managers.Account.Contracts.Requests;

internal class CustomerRegistrationInfo
{
    [Required(ErrorMessage = "The requestId is missing")]
    public string? RequestId { get; set; }

    [Required(ErrorMessage = "The callerId is required to enable upstream callback filtering")]
    public string? CallerId { get; set; }

    [ValidateNever]
    public string SchemaVersion { get; set; } = "1.0";
        
    [Required(ErrorMessage = "The first Name is missing")]
    public string? FirstName { get; set; }
        
    [Required(ErrorMessage = "The last Name is missing")]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "The email is missing")]
    [EmailAddress(ErrorMessage = "the Email is not valid")]
    public string? Email { get; set; }


    [Required(ErrorMessage = "The phone Number is missing")]
    [Phone(ErrorMessage = "The phone Number is not valid")]
    public string? PhoneNumber { get; set; }

    [ValidateNever]
    public string? Address { get; set; }

    [ValidateNever]
    public string? City { get; set; }

    [ValidateNever]
    public string? State { get; set; }

    [ValidateNever]
    public string? ZipCode { get; set; }
}