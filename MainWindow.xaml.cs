using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MemoryCleaner
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly DispatcherTimer _updateTimer;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // Configurar timer para actualización automática (menos frecuente para ahorrar recursos)
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Cambiar de 2 a 5 segundos
            };
            _updateTimer.Tick += (s, e) => _viewModel.UpdateMemoryInfo();
            _updateTimer.Start();

            // Cargar información inicial
            _viewModel.UpdateMemoryInfo();

            // Configurar cierre minimizado a bandeja
            StateChanged += MainWindow_StateChanged;
        }

        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Deshabilitar el botón durante la limpieza
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Limpiando...";
                }

                _viewModel.StatusText = "Iniciando limpieza de memoria...";

                // Obtener memoria inicial para mostrar el progreso
                var initialMemory = MemoryManager.GetMemoryInfo();

                // Ejecutar limpieza en un hilo separado para no bloquear la UI
                await Task.Run(() => _viewModel.CleanMemory());

                // Obtener memoria final para calcular la diferencia
                var finalMemory = MemoryManager.GetMemoryInfo();
                var memoryFreed = initialMemory.RamUsed - finalMemory.RamUsed;

                var message = memoryFreed > 0
                    ? $"Memoria limpiada exitosamente\n\nMemoria liberada: {FormatBytes(memoryFreed)}\nUso de RAM: {initialMemory.RamUsagePercent:F1}% → {finalMemory.RamUsagePercent:F1}%"
                    : "Limpieza completada\n\nLa memoria ya estaba optimizada.";

                MessageBox.Show(
                    message,
                    "Limpieza Completada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al limpiar memoria:\n\n{ex.Message}\n\nAsegúrate de ejecutar la aplicación como administrador.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Rehabilitar el botón
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Limpiar Memoria";
                }

                _viewModel.StatusText = "Listo";
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _updateTimer.Stop();
            base.OnClosing(e);
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        // Brushes estáticos para ahorrar memoria
        private static readonly SolidColorBrush RedBrush = new(Colors.Red);
        private static readonly SolidColorBrush OrangeBrush = new(Colors.Orange);
        private static readonly SolidColorBrush YellowBrush = new(Colors.Yellow);
        private static readonly SolidColorBrush GreenBrush = new(Colors.Green);

        private double _ramUsagePercent;
        private double _virtualMemoryUsagePercent;
        private double _cacheUsagePercent;
        private string _ramUsageText = string.Empty;
        private string _virtualMemoryUsageText = string.Empty;
        private string _cacheUsageText = string.Empty;
        private string _lastCleanText = "Nunca";
        private string _statusText = "Listo";
        private bool _canCleanMemory = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double RamUsagePercent
        {
            get => _ramUsagePercent;
            set
            {
                _ramUsagePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RamUsageColor));
            }
        }

        public double VirtualMemoryUsagePercent
        {
            get => _virtualMemoryUsagePercent;
            set
            {
                _virtualMemoryUsagePercent = value;
                OnPropertyChanged();
            }
        }

        public double CacheUsagePercent
        {
            get => _cacheUsagePercent;
            set
            {
                _cacheUsagePercent = value;
                OnPropertyChanged();
            }
        }

        public SolidColorBrush RamUsageColor
        {
            get
            {
                return _ramUsagePercent switch
                {
                    >= 90 => RedBrush,
                    >= 75 => OrangeBrush,
                    >= 50 => YellowBrush,
                    _ => GreenBrush
                };
            }
        }

        public string RamUsageText
        {
            get => _ramUsageText;
            set
            {
                _ramUsageText = value;
                OnPropertyChanged();
            }
        }

        public string VirtualMemoryUsageText
        {
            get => _virtualMemoryUsageText;
            set
            {
                _virtualMemoryUsageText = value;
                OnPropertyChanged();
            }
        }

        public string CacheUsageText
        {
            get => _cacheUsageText;
            set
            {
                _cacheUsageText = value;
                OnPropertyChanged();
            }
        }

        public string LastCleanText
        {
            get => _lastCleanText;
            set
            {
                _lastCleanText = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public bool CanCleanMemory
        {
            get => _canCleanMemory;
            set
            {
                _canCleanMemory = value;
                OnPropertyChanged();
            }
        }

        public void UpdateMemoryInfo()
        {
            try
            {
                var memoryInfo = MemoryManager.GetMemoryInfo();

                // Actualizar solo si hay cambios significativos (más de 1% de diferencia)
                if (Math.Abs(memoryInfo.RamUsagePercent - _ramUsagePercent) > 1.0)
                {
                    RamUsagePercent = memoryInfo.RamUsagePercent;
                }

                if (Math.Abs(memoryInfo.VirtualMemoryUsagePercent - _virtualMemoryUsagePercent) > 1.0)
                {
                    VirtualMemoryUsagePercent = memoryInfo.VirtualMemoryUsagePercent;
                }

                // Calcular porcentaje del caché basado en la memoria RAM total
                var newCachePercent = memoryInfo.RamTotal > 0 ? (double)memoryInfo.CacheUsed * 100.0 / memoryInfo.RamTotal : 0;
                if (Math.Abs(newCachePercent - _cacheUsagePercent) > 1.0)
                {
                    CacheUsagePercent = newCachePercent;
                }

                // Actualizar textos solo si es necesario
                var newRamText = $"Usado: {FormatBytes(memoryInfo.RamUsed)} / Total: {FormatBytes(memoryInfo.RamTotal)}";
                if (newRamText != _ramUsageText)
                {
                    RamUsageText = newRamText;
                }

                var newVirtualText = $"Usado: {FormatBytes(memoryInfo.VirtualMemoryUsed)} / Total: {FormatBytes(memoryInfo.VirtualMemoryTotal)}";
                if (newVirtualText != _virtualMemoryUsageText)
                {
                    VirtualMemoryUsageText = newVirtualText;
                }

                var newCacheText = $"Caché: {FormatBytes(memoryInfo.CacheUsed)}";
                if (newCacheText != _cacheUsageText)
                {
                    CacheUsageText = newCacheText;
                }

                if (_statusText != "Listo")
                {
                    StatusText = "Listo";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }

        public void CleanMemory()
        {
            try
            {
                StatusText = "Limpiando memoria...";
                CanCleanMemory = false;

                var result = MemoryManager.CleanMemory();

                // Mostrar resultados detallados
                if (result.Success)
                {
                    LastCleanText = $"Última limpieza: {DateTime.Now:HH:mm:ss} - {result.GetFormattedMemoryFreed()} liberados";
                    StatusText = $"Limpieza completada - {result.GetFormattedMemoryFreed()} liberados";

                    // Log detallado para debug
                    System.Diagnostics.Debug.WriteLine($"Limpieza exitosa: {result.GetSummary()}");
                }
                else
                {
                    LastCleanText = $"Última limpieza: {DateTime.Now:HH:mm:ss} - Error";
                    StatusText = "Error durante la limpieza";

                    // Log de errores
                    System.Diagnostics.Debug.WriteLine($"Errores en limpieza: {string.Join("; ", result.Errors)}");
                }

                // Actualizar información después de la limpieza
                UpdateMemoryInfo();
            }
            catch (Exception ex)
            {
                LastCleanText = $"Última limpieza: {DateTime.Now:HH:mm:ss} - Error";
                StatusText = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Excepción en CleanMemory: {ex}");
            }
            finally
            {
                CanCleanMemory = true;
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
