using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace MemoryCleaner
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings = null!;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _settings = SettingsManager.Load();

            AutoCleanCheckBox.IsChecked = _settings.AutoCleanEnabled;
            ThresholdTextBox.Text = _settings.CleanThresholdPercent.ToString();
            IntervalTextBox.Text = _settings.CleanIntervalMinutes.ToString();

            ShowNotificationsCheckBox.IsChecked = _settings.ShowNotifications;
            PlaySoundCheckBox.IsChecked = _settings.PlaySound;

            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            StartMinimizedCheckBox.IsChecked = _settings.StartMinimized;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(ThresholdTextBox.Text, out int threshold) || threshold < 1 || threshold > 99)
            {
                MessageBox.Show("El umbral debe estar entre 1 y 99", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(IntervalTextBox.Text, out int interval) || interval < 1 || interval > 1440)
            {
                MessageBox.Show("El intervalo debe estar entre 1 y 1440 minutos", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _settings.AutoCleanEnabled = AutoCleanCheckBox.IsChecked ?? false;
            _settings.CleanThresholdPercent = threshold;
            _settings.CleanIntervalMinutes = interval;

            _settings.ShowNotifications = ShowNotificationsCheckBox.IsChecked ?? true;
            _settings.PlaySound = PlaySoundCheckBox.IsChecked ?? true;

            _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            _settings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;

            SettingsManager.Save(_settings);

            // Configurar inicio con Windows
            SetStartupWithWindows(_settings.StartWithWindows);

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static void SetStartupWithWindows(bool enable)
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key != null)
                {
                    if (enable)
                    {
                        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue("MemoryCleaner", $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue("MemoryCleaner", false);
                    }
                    key.Close();
                }
            }
            catch
            {
                // Ignorar errores de registro
            }
        }
    }

    public class AppSettings
    {
        public bool AutoCleanEnabled { get; set; } = false;
        public int CleanThresholdPercent { get; set; } = 75;
        public int CleanIntervalMinutes { get; set; } = 30;
        public bool ShowNotifications { get; set; } = true;
        public bool PlaySound { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MemoryCleaner",
            "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // Ignorar errores de carga
            }

            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Ignorar errores de guardado
            }
        }
    }
}
