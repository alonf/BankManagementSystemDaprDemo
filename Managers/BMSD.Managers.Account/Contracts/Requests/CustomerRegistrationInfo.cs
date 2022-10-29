using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace BMSD.Managers.Account.Contracts.Requests
{
    public class CustomerRegistrationInfo
    {
        [Required(ErrorMessage = "The RequestId is missing")]
        public string? RequestId { get; set; }

        [Required(ErrorMessage = "The callerId is required to enable upstream callback filtering")]
        public string? CallerId { get; set; }
        
        [ValidateNever]
        public string SchemaVersion { get; set; } = "1.0";
        
        [Required(ErrorMessage = "The First Name is missing")]
        public string? FirstName { get; set; }
        
        [Required(ErrorMessage = "The Last Name is missing")]
        public string? LastName { get; set; }

        [Required(ErrorMessage = "The Email is missing")]
        [EmailAddress(ErrorMessage = "the Email is not valid")]
        public string? Email { get; set; }


        [Required(ErrorMessage = "The Phone Number is missing")]
        [Phone(ErrorMessage = "The Phone Number is not valid")]
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
}
