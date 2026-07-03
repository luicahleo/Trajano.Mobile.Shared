namespace Trajano.Mobile.Shared.Sync
{
    /// <summary>
    /// Abstracción del acceso a la base local (SQLite) que cada app implementa sobre su propio
    /// repositorio, para que OfflineSyncEngine no conozca los detalles de persistencia de
    /// ninguna de las dos apps. Cada app aporta solo el mapeo entre su entidad local (TLocal)
    /// y estas operaciones.
    /// </summary>
    public interface ISyncLocalStore<TLocal>
    {
        /// <summary>
        /// Registros pendientes de sincronización (nuevos o editados), sin filtrar por backoff —
        /// el filtrado por tiempo de reintento lo hace el motor.
        /// </summary>
        Task<List<TLocal>> GetPendingAsync();

        /// <summary>
        /// Clave de idempotencia estable del registro (generada una sola vez al crearlo
        /// localmente), enviada en cada intento de sincronización para que el backend pueda
        /// detectar duplicados de forma exacta en vez de heurísticas por ventana de tiempo.
        /// </summary>
        Guid GetIdempotencyKey(TLocal item);

        /// <summary>
        /// Número de intentos de sincronización ya realizados para este registro.
        /// </summary>
        int GetAttempts(TLocal item);

        /// <summary>
        /// Momento del último intento de sincronización, o null si nunca se intentó.
        /// Usado por el motor para calcular el backoff exponencial.
        /// </summary>
        DateTime? GetLastAttempt(TLocal item);

        /// <summary>
        /// Incrementa el contador de intentos y actualiza el timestamp del último intento,
        /// antes de despachar la sincronización (evita reintentar de inmediato si el proceso
        /// se interrumpe a mitad de la llamada HTTP).
        /// </summary>
        Task IncrementAttemptAsync(TLocal item);

        /// <summary>
        /// Marca el registro como sincronizado exitosamente con el identificador del servidor.
        /// extraData reenvía SyncDispatchOutcome.ExtraData tal cual, para datos adicionales
        /// específicos de la app (ej. un NumeroRegistro asignado por el servidor).
        /// </summary>
        Task MarkSyncedAsync(TLocal item, string? serverId, object? extraData);

        /// <summary>
        /// Marca el registro con un error. El motor decide, según MaxAttempts, si este error es
        /// terminal (Error) o si el registro sigue pendiente para el próximo ciclo.
        /// </summary>
        Task MarkErrorAsync(TLocal item, string error, bool esTerminal);
    }
}
