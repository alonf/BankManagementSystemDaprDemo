using Json.Schema;
using System.Runtime.Serialization;

namespace BMS.Accessors.UserInfo
{
    [Serializable]
    internal class JSchemaValidationException : Exception
    {
        private ValidationResults? validationResult;

        public JSchemaValidationException()
        {
        }

        public JSchemaValidationException(ValidationResults validationResult)
        {
            this.validationResult = validationResult;
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