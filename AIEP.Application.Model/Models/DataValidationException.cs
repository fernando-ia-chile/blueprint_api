namespace AIEP.Application.Model.Models
{
    [Serializable]
    public class DataValidationException : Exception
    {
        public DataValidationException()
        {
        }

        public DataValidationException(string message)
            : base(message)
        {
        }

        public DataValidationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
