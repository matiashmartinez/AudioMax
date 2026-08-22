using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using NAudio.CoreAudioApi;

namespace AudioTwin.App
{
    public class OutputDeviceViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _delayMs;
        private float _volume = 100f;

        public MMDevice Device { get; set; } = null!;
        public string DeviceId => Device?.ID ?? "";
        public string FriendlyName => Device?.FriendlyName ?? "";

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public int DelayMs
        {
            get => _delayMs;
            set { _delayMs = value; OnPropertyChanged(); }
        }

        public float Volume
        {
            get => _volume;
            set { _volume = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AppSettings
    {
        public string? SelectedInputId { get; set; }
        public bool EnableAutoSync { get; set; } = true;
        public List<DeviceConfig> DeviceConfigs { get; set; } = new();
    }

    public class DeviceConfig
    {
        public string DeviceId { get; set; } = "";
        public bool IsSelected { get; set; }
        public int DelayMs { get; set; }
        public float Volume { get; set; } = 100f;
    }

    public partial class MainWindow : Window
    {
        private AudioEngine _engine;
        private MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();
        public ObservableCollection<OutputDeviceViewModel> OutputDevices { get; set; } = new();
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public MainWindow()
        {
            InitializeComponent();
            _engine = new AudioEngine(Log);
            CargarDispositivosHardware();
            CargarConfiguracion();
        }

        private void CargarDispositivosHardware()
        {
            try
            {
                var renderDevices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

                var entradasValidas = renderDevices.Where(d => 
                    d.FriendlyName.Contains("Cable", StringComparison.OrdinalIgnoreCase) ||
                    d.FriendlyName.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                    d.FriendlyName.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                if (!entradasValidas.Any()) entradasValidas = renderDevices;

                InDev.ItemsSource = entradasValidas;
                InDev.DisplayMemberPath = "FriendlyName";
                if (entradasValidas.Any()) InDev.SelectedIndex = 0;

                var salidasFisicas = renderDevices.Where(d => 
                    !d.FriendlyName.Contains("Cable Input", StringComparison.OrdinalIgnoreCase) &&
                    !d.FriendlyName.Contains("VoiceMeeter Output", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                OutputDevices.Clear();
                foreach (var d in salidasFisicas)
                {
                    OutputDevices.Add(new OutputDeviceViewModel
                    {
                        Device = d,
                        IsSelected = false,
                        DelayMs = 0,
                        Volume = 100f
                    });
                }

                OutDevs.ItemsSource = OutputDevices;
                Log($"Dispositivos filtrados: {entradasValidas.Count} entradas, {salidasFisicas.Count} salidas físicas.");
            }
            catch (Exception ex)
            {
                Log($"Error al detectar hardware: {ex.Message}");
            }
        }

        private void GuardarConfiguracion()
        {
            try
            {
                var settings = new AppSettings
                {
                    SelectedInputId = (InDev.SelectedItem as MMDevice)?.ID,
                    EnableAutoSync = ChkAutoSync.IsChecked ?? true,
                    DeviceConfigs = OutputDevices.Select(d => new DeviceConfig
                    {
                        DeviceId = d.DeviceId,
                        IsSelected = d.IsSelected,
                        DelayMs = d.DelayMs,
                        Volume = d.Volume
                    }).ToList()
                };

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Log($"No se pudo guardar la config: {ex.Message}");
            }
        }

        private void CargarConfiguracion()
        {
            try
            {
                if (!File.Exists(_configPath)) return;

                string json = File.ReadAllText(_configPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings == null) return;

                ChkAutoSync.IsChecked = settings.EnableAutoSync;

                if (!string.IsNullOrEmpty(settings.SelectedInputId))
                {
                    foreach (var item in InDev.Items)
                    {
                        if (item is MMDevice dev && dev.ID == settings.SelectedInputId)
                        {
                            InDev.SelectedItem = dev;
                            break;
                        }
                    }
                }

                foreach (var dc in settings.DeviceConfigs)
                {
                    var match = OutputDevices.FirstOrDefault(o => o.DeviceId == dc.DeviceId);
                    if (match != null)
                    {
                        match.IsSelected = dc.IsSelected;
                        match.DelayMs = dc.DelayMs;
                        match.Volume = dc.Volume;
                    }
                }

                Log("Configuración previa cargada desde config.json.");
            }
            catch (Exception ex)
            {
                Log($"Error cargando configuración: {ex.Message}");
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _engine.Stop();

            var seleccionados = OutputDevices.Where(x => x.IsSelected).ToList();

            if (InDev.SelectedItem == null || seleccionados.Count == 0)
            {
                MessageBox.Show("Selecciona el Cable Virtual en la entrada y marca al menos una salida física.",
                                "Configuración incompleta", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cableVirtual = (MMDevice)InDev.SelectedItem;
            var targets = seleccionados.Select(x => new OutputTarget(x.Device, x.DelayMs, x.Volume)).ToList();
            bool autoSync = ChkAutoSync.IsChecked ?? true;

            Log("Aplicando configuración v1.2.0...");
            _engine.Start(cableVirtual, targets, autoSync);
            StatusText.Text = "Activo (v1.2.0)";

            GuardarConfiguracion();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _engine.Stop();
            StatusText.Text = "Detenido";
        }

        private void Log(string mensaje)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogList.Items.Add($"{DateTime.Now:HH:mm:ss.fff}  {mensaje}");
                if (LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[^1]);
            }));
        }
    }
}