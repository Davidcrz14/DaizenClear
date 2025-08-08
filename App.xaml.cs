using System.Windows;
using System.Windows.Threading;

namespace MemoryCleaner
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Configurar manejo de excepciones global
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            // Verificar privilegios de administrador
            if (!MemoryManager.IsAdministrator())
            {
                MessageBox.Show(
                    "Esta aplicación requiere privilegios de administrador para limpiar la memoria.\n\n" +
                    "Por favor, ejecute la aplicación como administrador.",
                    "Privilegios Insuficientes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                
                Shutdown();
                return;
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"Error inesperado: {e.Exception.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            e.Handled = true;
        }
    }
}