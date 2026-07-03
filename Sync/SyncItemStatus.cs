namespace Trajano.Mobile.Shared.Sync
{
    /// <summary>
    /// Estado de sincronización de una entidad local. Generaliza el enum EstadoSync
    /// de IMGA (Pendiente/Sincronizando/Sincronizado/Error/PendienteEdicion) — es el más
    /// completo de las dos apps; IMCA solo tenía un bool Sincronizado sin estado terminal.
    /// </summary>
    public enum SyncItemStatus
    {
        Pending = 0,
        Syncing = 1,
        Synced = 2,
        Error = 3,
        PendingEdit = 4
    }
}
