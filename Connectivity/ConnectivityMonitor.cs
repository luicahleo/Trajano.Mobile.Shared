using Trajano.Mobile.Shared.Logging;
using MauiConnectivity = Microsoft.Maui.Networking.Connectivity;
using MauiNetworkAccess = Microsoft.Maui.Networking.NetworkAccess;
using MauiConnectivityChangedEventArgs = Microsoft.Maui.Networking.ConnectivityChangedEventArgs;

namespace Trajano.Mobile.Shared.Connectivity
{
    /// <inheritdoc cref="IConnectivityMonitor"/>
    public partial class ConnectivityMonitor : IConnectivityMonitor
    {
        private readonly ISharedLogger _logger;
        private bool _monitoreando;

        private const string LogTag = "ConnectivityMonitor";

        public bool TieneInternet =>
            MauiConnectivity.Current.NetworkAccess == MauiNetworkAccess.Internet;

        public event EventHandler<bool>? ConectividadCambio;

        public ConnectivityMonitor(ISharedLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void IniciarMonitoreo()
        {
            if (_monitoreando)
            {
                return;
            }

            MauiConnectivity.Current.ConnectivityChanged += OnConectividadCambiada;
            _monitoreando = true;

            _ = _logger.LogInfoAsync(
                $"Monitoreo de conectividad iniciado - Estado actual: {(TieneInternet ? "Con internet" : "Sin internet")}",
                LogTag);
        }

        public void DetenerMonitoreo()
        {
            if (!_monitoreando)
            {
                return;
            }

            MauiConnectivity.Current.ConnectivityChanged -= OnConectividadCambiada;
            _monitoreando = false;

            _ = _logger.LogInfoAsync("Monitoreo de conectividad detenido", LogTag);
        }

        private void OnConectividadCambiada(object? sender, MauiConnectivityChangedEventArgs e)
        {
            bool tieneInternet = e.NetworkAccess == MauiNetworkAccess.Internet;

            _ = _logger.LogInfoAsync(
                $"Cambio de conectividad detectado - Internet: {(tieneInternet ? "SÍ" : "NO")}, " +
                $"Perfiles: {string.Join(", ", e.ConnectionProfiles)}",
                LogTag);

            ConectividadCambio?.Invoke(this, tieneInternet);
        }

        public void Dispose()
        {
            DetenerMonitoreo();
            GC.SuppressFinalize(this);
        }
    }
}
