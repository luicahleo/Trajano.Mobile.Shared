using CommunityToolkit.Mvvm.ComponentModel;

namespace Trajano.Mobile.Shared.Mvvm
{
    /// <summary>
    /// Base para ViewModels que reemplaza el patrón IsLoading/ErrorMessage/HasError
    /// reimplementado independientemente en 19 ViewModels de IMGA y 6 de IMCA (con nombres
    /// de campo inconsistentes entre ambos: isLoading vs _isLoading). RunSafeAsync encapsula
    /// el guard "if IsLoading return; ... finally IsLoading = false" que también se repetía
    /// casi idéntico en cada uno.
    /// </summary>
    public abstract partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool hasError;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        /// <summary>
        /// Limpia el estado de error sin tocar IsLoading.
        /// </summary>
        protected void ClearError()
        {
            this.HasError = false;
            this.ErrorMessage = string.Empty;
        }

        /// <summary>
        /// Establece el estado de error visible en la UI.
        /// </summary>
        protected void SetError(string message)
        {
            this.HasError = true;
            this.ErrorMessage = message;
        }

        /// <summary>
        /// Ejecuta una operación asíncrona con el guard estándar: ignora si ya hay una
        /// operación en curso (IsLoading), limpia el error previo, marca IsLoading mientras
        /// corre, y lo restaura al terminar (incluso si lanza una excepción no capturada por
        /// el propio <paramref name="operacion"/>).
        /// </summary>
        /// <param name="operacion">Operación a ejecutar</param>
        /// <param name="onError">
        /// Callback opcional para manejar una excepción no capturada por <paramref name="operacion"/>.
        /// Si no se provee, la excepción se vuelca a ErrorMessage con SetError y se traga
        /// (no se relanza), igual que hacían la mayoría de los call sites que este helper reemplaza.
        /// </param>
        protected async Task RunSafeAsync(Func<Task> operacion, Action<Exception>? onError = null)
        {
            if (this.IsLoading)
            {
                return;
            }

            this.IsLoading = true;
            this.ClearError();

            try
            {
                await operacion();
            }
            catch (Exception ex)
            {
                if (onError != null)
                {
                    onError(ex);
                }
                else
                {
                    this.SetError(ex.Message);
                }
            }
            finally
            {
                this.IsLoading = false;
            }
        }
    }
}
