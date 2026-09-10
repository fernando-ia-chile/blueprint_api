using System.Linq;

namespace AIEP.Application.Model.Models
{
    [Serializable]
    public class RequiredParameterException : Exception
    {
        public RequiredParameterException(string propertyName, string message, Exception ex)
            : base($"El parámetro [{propertyName}] es requerido. " + message, ex)
        {
        }

        public RequiredParameterException(string propertyName, string message)
            : base($"El parámetro [{propertyName}] es requerido. " + message)
        {
        }

        public RequiredParameterException(string propertyName)
            : base($"El parámetro [{propertyName}] es requerido.")
        {
        }
    }

    [Serializable]
    public class RequiredParametersException : Exception
    {
        public RequiredParametersException(params string[] propertyName)
            : base($"Los parámetros [{propertyName.Aggregate((i, j) => i + ", " + j)}] son requeridos.")
        {
        }
    }
}
