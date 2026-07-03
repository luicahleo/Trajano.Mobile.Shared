namespace Trajano.Mobile.Shared.Connectivity
{
    /// <summary>
    /// Monitor de conectividad basado en eventos del sistema operativo (no en polling).
    /// Reemplaza el ConnectivityService propio de IMGA (que ya tenía este diseño correcto) y
    /// el chequeo inline por timer de IMCA (que solo verificaba conectividad activamente
    /// dentro del ciclo de un timer de sincronización, sin reaccionar a cambios reales de red).
    /// </summary>
    public interface IConnectivityMonitor : IDisposable
    {
        /// <summary>
        /// Indica si el dispositivo tiene acceso a la red en este momento.
        /// </summary>
        bool TieneInternet { get; }

        /// <summary>
        /// Se dispara cuando el sistema operativo notifica un cambio de conectividad.
        /// El parámetro indica si hay internet después del cambio.
        /// </summary>
        event EventHandler<bool>? ConectividadCambio;

        /// <summary>
        /// Comienza a escuchar los cambios de conectividad del sistema operativo.
        /// Debe llamarse una vez al inicio de la app (idempotente).
        /// </summary>
        void IniciarMonitoreo();

        /// <summary>
        /// Deja de escuchar los cambios de conectividad (idempotente).
        /// </summary>
        void DetenerMonitoreo();
    }
}
