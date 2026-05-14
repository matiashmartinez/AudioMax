using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using NAudio.CoreAudioApi;

namespace AudioTwin.App
{
    public partial class MainWindow : Window
    {
        private AudioEngine _engine;
        private MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();

        public MainWindow()
        {
            InitializeComponent();
            _engine = new AudioEngine(Log);   // Inyectamos la función de log
            CargarDispositivosHardware();
        }

        private void CargarDispositivosHardware()
        {
            try
            {
                var dispositivos = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

                InDev.ItemsSource = dispositivos;
                InDev.DisplayMemberPath = "FriendlyName";

                OutDevs.ItemsSource = dispositivos;
                OutDevs.DisplayMemberPath = "FriendlyName";

                Log($"Se encontraron {dispositivos.Count} dispositivos de reproducción.");
            }
            catch (Exception ex)
            {
                Log($"Error al detectar hardware: {ex.Message}");
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Si ya está en marcha, detenemos antes de reconfigurar
            _engine.Stop();

            if (InDev.SelectedItem == null || OutDevs.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecciona el Cable Virtual en la entrada y al menos un altavoz en la salida.",
                                "Configuración incompleta", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cableVirtual = (MMDevice)InDev.SelectedItem;
            var salidasFisicas = OutDevs.SelectedItems.Cast<MMDevice>().ToList();

            Log("Aplicando configuración...");
            _engine.Start(cableVirtual, salidasFisicas);
            StatusText.Text = "Activo";
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _engine.Stop();
            StatusText.Text = "Detenido";
        }

        // Método de log que actualiza la lista desde cualquier hilo
        private void Log(string mensaje)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogList.Items.Add($"{DateTime.Now:HH:mm:ss.fff}  {mensaje}");
                // Auto-scroll al último elemento
                if (LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[^1]);
            }));
        }
    }
}