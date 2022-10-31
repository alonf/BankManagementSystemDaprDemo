using System.Runtime.Serialization;

namespace BMSD.Accessors.UserInfo
{
    [Serializable]
    internal class JSchemaValidationException : Exception
    {
        public ICollection<NJsonSchema.Validation.ValidationError>? ValidationResult { get; private set; }

        public JSchemaValidationException()
        {
        }

        public JSchemaValidationException(ICollection<NJsonSchema.Validation.ValidationError> validationResult) :
            base(string.Join(Environment.NewLine, validationResult.Select(x => x.ToString())))
        {
            ValidationResult = validationResult;
        }

        public JSchemaValidationException(string? message) : base(message)
        {
        }

        public JSchemaValidationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected JSchemaValidationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}