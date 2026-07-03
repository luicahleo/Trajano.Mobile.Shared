using Trajano.Mobile.Shared.Connectivity;
using Trajano.Mobile.Shared.Logging;

namespace Trajano.Mobile.Shared.Sync
{
    /// <summary>
    /// Motor de sincronización offline único, reemplaza los dos SyncService independientes de
    /// IMGA e IMCA. Combina el guard de concurrencia real de IMGA (SemaphoreSlim no bloqueante)
    /// y su estado terminal explícito, con el envío en lotes y el backoff exponencial de IMCA,
    /// más una clave de idempotencia (nueva en ambas) que reemplaza tanto la dependencia de
    /// IdServidor (IMGA) como la heurística de ventana ±2h (IMCA) para detectar duplicados en
    /// el servidor. Cada app aporta solo: el acceso a su base local (ISyncLocalStore), el mapeo
    /// local-a-DTO y el despacho HTTP real (dispatcher).
    /// </summary>
    public class OfflineSyncEngine<TLocal, TDto> : IDisposable
    {
        private readonly ISyncLocalStore<TLocal> _store;
        private readonly Func<TLocal, TDto> _mapper;
        private readonly Func<IReadOnlyList<SyncDispatchItem<TLocal, TDto>>, CancellationToken, Task<IReadOnlyList<SyncDispatchOutcome>>> _dispatcher;
        private readonly IConnectivityMonitor _connectivity;
        private readonly ISharedLogger _logger;
        private readonly int _maxAttempts;
        private readonly int _batchSize;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private System.Timers.Timer? _safetyTimer;
        private bool _autoSyncStarted;

        public bool IsSyncing { get; private set; }

        public event EventHandler<SyncProgressEventArgs>? Progress;

        /// <param name="maxAttempts">Intentos antes de marcar el registro con estado terminal Error (IMGA usaba 3).</param>
        /// <param name="batchSize">Máximo de registros despachados por llamada al dispatcher (IMCA no tenía límite).</param>
        public OfflineSyncEngine(
            ISyncLocalStore<TLocal> store,
            Func<TLocal, TDto> mapper,
            Func<IReadOnlyList<SyncDispatchItem<TLocal, TDto>>, CancellationToken, Task<IReadOnlyList<SyncDispatchOutcome>>> dispatcher,
            IConnectivityMonitor connectivity,
            ISharedLogger logger,
            int maxAttempts = 3,
            int batchSize = 50)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxAttempts = maxAttempts;
            _batchSize = batchSize;
        }

        /// <summary>
        /// Activa el disparo automático: evento de conectividad (primario) más un timer de
        /// respaldo de bajo costo, reemplazando el timer de 5 segundos ad-hoc que tenía
        /// KioscoViewModel en IMCA (el disparo primario pasa a ser reconexión, no polling).
        /// Idempotente.
        /// </summary>
        public void StartAutoSync(TimeSpan? safetyInterval = null)
        {
            if (_autoSyncStarted)
            {
                return;
            }

            _autoSyncStarted = true;
            _connectivity.ConectividadCambio += OnConectividadCambio;

            _safetyTimer = new System.Timers.Timer((safetyInterval ?? TimeSpan.FromMinutes(5)).TotalMilliseconds)
            {
                AutoReset = true
            };
            _safetyTimer.Elapsed += async (_, _) => await SyncPendingAsync();
            _safetyTimer.Start();
        }

        public void StopAutoSync()
        {
            if (!_autoSyncStarted)
            {
                return;
            }

            _connectivity.ConectividadCambio -= OnConectividadCambio;
            _safetyTimer?.Stop();
            _safetyTimer?.Dispose();
            _safetyTimer = null;
            _autoSyncStarted = false;
        }

        private async void OnConectividadCambio(object? sender, bool tieneInternet)
        {
            if (tieneInternet)
            {
                await SyncPendingAsync();
            }
        }

        /// <summary>
        /// Ejecuta un ciclo de sincronización. No bloqueante: si ya hay una sincronización en
        /// curso, retorna inmediatamente un resultado vacío (comportamiento de IMGA).
        /// </summary>
        public async Task<SyncResult> SyncPendingAsync(CancellationToken cancellationToken = default)
        {
            SyncResult resultado = new SyncResult();

            if (!_connectivity.TieneInternet)
            {
                await _logger.LogInfoAsync("Sin internet. Sincronización pospuesta.", "OfflineSyncEngine");
                return resultado;
            }

            if (!await _lock.WaitAsync(0, cancellationToken))
            {
                await _logger.LogInfoAsync("Sincronización ya en progreso. Ignorando solicitud.", "OfflineSyncEngine");
                return resultado;
            }

            try
            {
                IsSyncing = true;

                List<TLocal> pendientes = await _store.GetPendingAsync();
                resultado.TotalPendientes = pendientes.Count;

                if (pendientes.Count == 0)
                {
                    return resultado;
                }

                List<TLocal> listos = pendientes.Where(EstaListoParaReintento).ToList();
                resultado.Omitidos = pendientes.Count - listos.Count;

                int completados = 0;

                foreach (List<TLocal> lote in Chunk(listos, _batchSize))
                {
                    if (!_connectivity.TieneInternet)
                    {
                        await _logger.LogWarnAsync(
                            $"Internet perdido durante sincronización. Procesados: {completados}/{listos.Count}",
                            "OfflineSyncEngine");
                        break;
                    }

                    List<SyncDispatchItem<TLocal, TDto>> items = lote
                        .Select(local => new SyncDispatchItem<TLocal, TDto>
                        {
                            Local = local,
                            Dto = _mapper(local),
                            IdempotencyKey = _store.GetIdempotencyKey(local)
                        })
                        .ToList();

                    foreach (SyncDispatchItem<TLocal, TDto> item in items)
                    {
                        await _store.IncrementAttemptAsync(item.Local);
                    }

                    IReadOnlyList<SyncDispatchOutcome> outcomes;
                    try
                    {
                        outcomes = await _dispatcher(items, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await _logger.LogErrorAsync($"Excepción despachando lote: {ex.Message}", "OfflineSyncEngine");
                        outcomes = items.Select(_ => SyncDispatchOutcome.Fail(ex.Message)).ToList();
                    }

                    for (int i = 0; i < items.Count; i++)
                    {
                        TLocal local = items[i].Local;
                        SyncDispatchOutcome outcome = i < outcomes.Count
                            ? outcomes[i]
                            : SyncDispatchOutcome.Fail("El dispatcher no devolvió resultado para este ítem");

                        if (outcome.Success)
                        {
                            await _store.MarkSyncedAsync(local, outcome.ServerId, outcome.ExtraData);
                            resultado.Exitosos++;
                        }
                        else
                        {
                            bool esTerminal = _store.GetAttempts(local) >= _maxAttempts;
                            await _store.MarkErrorAsync(local, outcome.Error ?? "Error desconocido del servidor", esTerminal);
                            resultado.Fallidos++;
                        }

                        completados++;
                        Progress?.Invoke(this, new SyncProgressEventArgs
                        {
                            Total = listos.Count,
                            Completados = completados,
                            Exitosos = resultado.Exitosos,
                            Fallidos = resultado.Fallidos,
                            MensajeActual = $"Sincronizando {completados}/{listos.Count}..."
                        });
                    }
                }

                return resultado;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error general en sincronización: {ex.Message}", "OfflineSyncEngine");
                return resultado;
            }
            finally
            {
                IsSyncing = false;
                _lock.Release();
            }
        }

        /// <summary>
        /// Backoff exponencial (de IMCA): intento 0 siempre listo; a partir de ahí espera
        /// 2^intentos minutos desde el último intento (1, 2, 4, 8, 16... minutos).
        /// </summary>
        private bool EstaListoParaReintento(TLocal item)
        {
            int intentos = _store.GetAttempts(item);
            if (intentos <= 0)
            {
                return true;
            }

            DateTime? ultimoIntento = _store.GetLastAttempt(item);
            if (ultimoIntento == null)
            {
                return true;
            }

            double delayMinutos = Math.Pow(2, intentos);
            return DateTime.Now >= ultimoIntento.Value.AddMinutes(delayMinutos);
        }

        private static IEnumerable<List<TLocal>> Chunk(List<TLocal> items, int size)
        {
            for (int i = 0; i < items.Count; i += size)
            {
                yield return items.Skip(i).Take(size).ToList();
            }
        }

        public void Dispose()
        {
            StopAutoSync();
            _lock.Dispose();
        }
    }
}
