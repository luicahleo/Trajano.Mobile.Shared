namespace Trajano.Mobile.Shared.Sync
{
    /// <summary>
    /// Resultado de despachar un único registro al backend (éxito + id del servidor, o error).
    /// </summary>
    public class SyncDispatchOutcome
    {
        public bool Success { get; init; }
        public string? ServerId { get; init; }
        public string? Error { get; init; }

        /// <summary>
        /// Datos adicionales devueltos por el servidor que la app necesita para actualizar su
        /// entidad local (ej. IMGA también recibe un NumeroRegistro además del Id). El motor no
        /// interpreta este valor, solo lo reenvía a ISyncLocalStore.MarkSyncedAsync.
        /// </summary>
        public object? ExtraData { get; init; }

        public static SyncDispatchOutcome Ok(string? serverId = null, object? extraData = null) => new() { Success = true, ServerId = serverId, ExtraData = extraData };
        public static SyncDispatchOutcome Fail(string error) => new() { Success = false, Error = error };
    }

    /// <summary>
    /// Un ítem local ya mapeado a su DTO remoto, con la clave de idempotencia lista para enviar.
    /// </summary>
    public class SyncDispatchItem<TLocal, TDto>
    {
        public required TLocal Local { get; init; }
        public required TDto Dto { get; init; }
        public required Guid IdempotencyKey { get; init; }
    }

    /// <summary>
    /// Resultado agregado de un ciclo de sincronización.
    /// </summary>
    public class SyncResult
    {
        public int TotalPendientes { get; set; }
        public int Omitidos { get; set; }
        public int Exitosos { get; set; }
        public int Fallidos { get; set; }
        public bool TodoExitoso => Fallidos == 0 && Exitosos == TotalPendientes - Omitidos;
    }

    public class SyncProgressEventArgs : EventArgs
    {
        public int Total { get; init; }
        public int Completados { get; init; }
        public int Exitosos { get; init; }
        public int Fallidos { get; init; }
        public string? MensajeActual { get; init; }
    }
}
