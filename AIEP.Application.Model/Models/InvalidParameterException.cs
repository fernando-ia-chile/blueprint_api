namespace AIEP.Application.Model.Models
{
    [Serializable]
    public class InvalidParameterException : Exception
    {
        public InvalidParameterException(string propertyName, string message, Exception ex)
            : base($"El valor del parámetro [{propertyName}] no es válido. " + message, ex)
        {
        }

        public InvalidParameterException(string propertyName, string message)
            : base($"El valor del parámetro [{propertyName}] no es válido. " + message)
        {
        }

        public InvalidParameterException(string propertyName)
            : base($"El valor del parámetro [{propertyName}] no es válido.")
        {
        }
    }
}
