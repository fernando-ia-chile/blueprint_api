namespace AIEP.Application.Model.Models;

public class ExceptionResponse
{
    /// <summary>
    /// Inicializa una nueva instancia de la clase <see cref="ExceptionResponse"/>.
    /// </summary>
    /// <param name="message">El mensaje de la excepción.</param>
    public ExceptionResponse(string message)
    {
        this.message = message;
    }

    public string message { get; set; }
    public string detail { get; set; }
}
