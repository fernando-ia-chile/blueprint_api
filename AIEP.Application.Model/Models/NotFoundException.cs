namespace AIEP.Application.Model.Models
{
    [Serializable]
    public class NotFoundException<IDT> : Exception
    {
        public IDT Id { get; set; }
        public string TipoEntidad { get; set; }

        public NotFoundException()
        {
        }

        public NotFoundException(IDT id, string tipoEntidad) : base()
        {
            Id = id;
            TipoEntidad = tipoEntidad;
        }

        public NotFoundException(string message)
            : base(message)
        {
        }

        public NotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public NotFoundException(string message, Exception inner, IDT id, string tipoEntidad)
            : base(message, inner)
        {
            Id = id;
            TipoEntidad = tipoEntidad;
        }
    }
}
