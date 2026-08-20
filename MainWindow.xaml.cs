using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using NAudio.CoreAudioApi;

namespace AudioTwin.App
{
    public class OutputDeviceViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _delayMs;

        public MMDevice Device { get; set; } = null!;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window
    {
        private AudioEngine _engine;
        private MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();
        public ObservableCollection<OutputDeviceViewModel> OutputDevices { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            _engine = new AudioEngine(Log);
            CargarDispositivosHardware();
        }

        private void CargarDispositivosHardware()
        {
            try
            {
                var dispositivos = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

                InDev.ItemsSource = dispositivos;
                InDev.DisplayMemberPath = "FriendlyName";

                OutputDevices.Clear();
                foreach (var d in dispositivos)
                {
                    OutputDevices.Add(new OutputDeviceViewModel
                    {
                        Device = d,
                        IsSelected = false,
                        DelayMs = 0
                    });
                }

                // Coincide exactamente con el x:Name="OutDevs" del XAML
                OutDevs.ItemsSource = OutputDevices;
                Log($"Se encontraron {dispositivos.Count} dispositivos de reproducción.");
            }
            catch (Exception ex)
            {
                Log($"Error al detectar hardware: {ex.Message}");
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _engine.Stop();

            var seleccionados = OutputDevices.Where(x => x.IsSelected).ToList();

            if (InDev.SelectedItem == null || seleccionados.Count == 0)
            {
                MessageBox.Show("Selecciona el Cable Virtual en la entrada y marca la casilla 'Usar' en al menos un altavoz.",
                                "Configuración incompleta", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cableVirtual = (MMDevice)InDev.SelectedItem;
            var targets = seleccionados.Select(x => new OutputTarget(x.Device, x.DelayMs)).ToList();

            Log("Aplicando configuración con retardo de sincronización...");
            _engine.Start(cableVirtual, targets);
            StatusText.Text = "Activo";
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