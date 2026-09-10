namespace AIEP.Application.Repository.Options
{
    /// <summary>
    /// Motores de base de datos soportados por la capa de repositorio.
    /// El valor efectivo se toma de <c>Database:Provider</c> en la configuracion.
    /// </summary>
    public enum DbProviderType
    {
        Oracle = 0,
        SqlServer = 1
    }
}
