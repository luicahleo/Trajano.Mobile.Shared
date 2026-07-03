namespace Trajano.Mobile.Shared.Logging
{
    /// <summary>
    /// Abstracción mínima de logging que necesitan los componentes compartidos
    /// (Trajano.Mobile.Shared). IMGA e IMCA ya tienen sus propios servicios de logging con
    /// nombres de métodos distintos (IEmulatorLoggingService, ILoggingService) — en vez de
    /// forzar un rename en cada app, cada una registra un adaptador delgado que implementa
    /// esta interfaz delegando a su logger real.
    /// </summary>
    public interface ISharedLogger
    {
        Task LogInfoAsync(string message, string category);
        Task LogWarnAsync(string message, string category);
        Task LogErrorAsync(string message, string category);
    }
}
