# 🎧 AudioTwin Pro (AudioMax)

> **AudioTwin Pro** es una aplicación de escritorio avanzada desarrollada en **C# (.NET / WPF)** y potenciada por **NAudio**, diseñada para duplicar, filtrar y sincronizar flujos de audio en tiempo real desde un dispositivo virtual de entrada hacia múltiples salidas físicas de hardware (altavoces, auriculares o dispositivos Bluetooth) con precisión milimétrica.

---

## 🚀 Características Principales (v2.1)

* **🔄 Sincronización Avanzada Anti-Desfase (Bluetooth):** Resuelve el problema crónico de desincronización en auriculares y parlantes Bluetooth tras períodos de silencio o modo reposo (*Standby*), mediante un reinicio limpio y sincronizado de los búferes de hardware.
* **🎛️ Control de Volumen en Tiempo Real:** Ajusta el volumen independiente de cada canal de salida al instante mediante barras deslizantes (*Sliders*) fluidas y etiquetas dinámicas, sin reiniciar el motor.
* **🧠 Filtrado Inteligente de Hardware:** Distingue automáticamente entre dispositivos de entrada virtuales (como *VB-Audio Cable* o *VoiceMeeter*) y altavoces físicos de salida.
* **💾 Persistencia Local Automática (`config.json`):** Guarda tus preferencias de dispositivos seleccionados, retardos en milisegundos y volúmenes, aplicándolos automáticamente al iniciar la aplicación.
* **⚡ Arquitectura de Audio Profesional v2.1 (Cero Microcortes):**
  * **Arranque Simultáneo Absoluto:** Inicializa y dispara todas las salidas de audio en bloque cerrado para minimizar el desfase inicial.
  * **Optimización de Memoria (Zero-Allocation):** Uso de `ArrayPool` para evitar la saturación del Recolector de Basura (*Garbage Collector*).
  * **Concurrencia Limpia:** Hilo de monitoreo pasivo en segundo plano (*Watchdog*) para asegurar la salud de los búferes sin generar bloqueos ni chasquidos.
* **🎨 Interfaz Moderna en Modo Oscuro:** Diseñada con una paleta elegante, tipografía legible y estilos optimizados para hover y selección en listas.
* **🛡️ Protección Anti-Clipping (Soft-Stop):** Aplicación de una detención suave al apagar el motor para evitar ruidos molestos en los parlantes.

---

## 📋 Requisitos del Sistema

1. **Windows 10 / 11** (x64).
2. Un controlador de audio virtual de entrada instalado (Recomendado: **[VB-CABLE Virtual Audio Device](https://vb-audio.com/Cable/)** o VoiceMeeter).
3. Dispositivos de salida físicos (auriculares cableados, altavoces, dispositivos Bluetooth, etc.).

---

## ⚙️ Guía de Uso Paso a Paso

### 1. Preparación del Sistema (Dispositivo Virtual)
1. Instala y configura **VB-CABLE** (o tu cable virtual preferido).
2. En la configuración de sonido de Windows, establece **CABLE Input** como tu **Dispositivo de Reproducción Predeterminado**. (Todo el sonido de tus juegos, navegador o reproductor multimedia pasará por este cable virtual).

### 2. Ejecución y Configuración en AudioTwin Pro
1. Abre **AudioTwin Pro**.
2. **Dispositivo de entrada:** En el menú desplegable superior, selecciona el cable virtual (ej. *CABLE Output*). La app lo detectará automáticamente gracias al filtrado inteligente.
3. **Dispositivos de salida:** En la lista central, marca las casillas (**Usar**) de los altavoces o auriculares físicos por los que quieres que suene el audio simultáneamente.
4. **Ajuste de Retardo (ms):** Si tus auriculares Bluetooth tienen un retraso natural de fábrica, ingresa los milisegundos necesarios (ej. `150` o `200`) para compensarlo y alinearlos perfectamente con tus parlantes por cable.
5. **Control de Volumen:** Desliza la barra de volumen de cada salida en tiempo real según lo necesites.
6. **Auto-Sincronización:** Mantén marcado el interruptor de Auto-Sync para mitigar la deriva de los relojes de hardware (*clock drift*).

### 3. Aplicar y Guardar
* Haz clic en el botón **▶ Aplicar**. El motor iniciará instantáneamente.
* La aplicación guardará tus preferencias en un archivo `config.json` local, por lo que la próxima vez que la abras, **se aplicará y reproducirá automáticamente**.

---

## 🛠️ Tecnologías Utilizadas

* **Lenguaje:** C# (.NET)
* **Interfaz Gráfica:** WPF (Windows Presentation Foundation)
* **Motor de Audio:** NAudio (CoreAudioApi / WASAPI Loopback)
* **Concurrencia:** Tasks, ArrayPool y gestión segura de hilos.

---

## 📂 Estructura del Código

* `MainWindow.xaml` / `MainWindow.xaml.cs`: Interfaz de usuario, enlace de datos con ViewModels y lógica de persistencia JSON.
* `AudioEngine.cs`: Núcleo de procesamiento de audio en tiempo real, control de búferes WASAPI y sincronización multi-canal con arquitectura profesional.

---

## 👨‍💻 Autor

Desarrollado con enfoque de ingeniería profesional por **Matías H. Martínez**.

---

## 📄 Licencia

Este proyecto se distribuye bajo los términos de la licencia MIT. Consulta el archivo `LICENSE` para más detalles.