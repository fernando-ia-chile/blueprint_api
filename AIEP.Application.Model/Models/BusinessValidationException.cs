namespace AIEP.Application.Model.Models
{
    [Serializable]
    public class BusinessValidationException : Exception
    {
        public BusinessValidationException(string message) : base(message)
        {
        }

        public BusinessValidationException(string message, Exception ex) : base(message, ex)
        {
        }
    }
}
