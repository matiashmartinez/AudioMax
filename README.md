# 🎧 AudioMax Twin Pro

**AudioMax Twin Pro** es una utilidad de ruteo de audio para Windows, desarrollada en .NET. Permite capturar el sonido del sistema a través de un cable virtual y replicarlo **simultáneamente** en múltiples dispositivos físicos (como auriculares, altavoces y monitores) en tiempo real y con calidad de estudio.

Ideal para gamers, analistas y entusiastas del audio que necesitan escuchar el mismo flujo de sonido en distintos periféricos sin lidiar con los molestos retrasos o las limitaciones nativas de Windows.

## ✨ Características Principales
* **Múltiples Salidas Simultáneas:** Envía el audio a tus auriculares (ej. Redragon) y a tus parlantes (ej. Realtek) al mismo tiempo.
* **Alta Fidelidad:** Soporte nativo para tasas de muestreo de nivel profesional (ej. 96000Hz, 32-bit IEEE Float).
* **Consola de Registro Activa:** Monitor de eventos en tiempo real para visualizar estados de conexión y formatos de captura.
* **Baja Latencia:** Motor impulsado por las APIs nativas de Windows (WASAPI) para garantizar una latencia casi nula.

---

## 🛠️ Requisitos Previos

1. **Sistema Operativo:** Windows 10 / Windows 11.
2. **Entorno:** .NET Runtime (correspondiente a la versión de compilación).
3. **Driver Virtual:** Necesitas instalar **[VB-Audio Virtual Cable](https://vb-audio.com/Cable/)** (o cualquier cable virtual equivalente) para que actúe como puente.

---

## ⚙️ Configuración del Sistema (¡Muy Importante!)

Para que la aplicación funcione correctamente y no sufra bloqueos por parte del sistema operativo, debes seguir estos pasos en Windows antes de ejecutar la app:

### 1. Configurar el Cable Virtual como Salida de Windows
Todo el audio de tu PC debe dirigirse primero al cable virtual.
* Ve a los ajustes de sonido de Windows.
* Selecciona **CABLE Input (VB-Audio Virtual Cable)** como tu dispositivo de salida predeterminado.

### 2. Sincronizar Frecuencias (Sample Rates)
Para evitar el error de "silencio" o chasquidos, todos los dispositivos involucrados deben hablar el mismo idioma.
* Ve a **Panel de Control > Sonido**.
* Haz clic derecho y entra a **Propiedades > Opciones Avanzadas** en los siguientes dispositivos:
  * El Cable Virtual (Reproducción y Grabación).
  * Tus auriculares físicos.
  * Tus altavoces/monitores.
* Asegúrate de que todos estén configurados exactamente en el mismo formato. *(Recomendado: 24 bits o 32 bits, 48000Hz o 96000Hz)*.

### 3. Permisos de Micrófono en Windows (Bloqueo Común)
Windows trata al Cable Virtual como un dispositivo de grabación (micrófono). Si la app no tiene permisos, capturará silencio.
* Ve a **Configuración de Windows > Privacidad y seguridad > Micrófono**.
* Asegúrate de que **"Permitir que las aplicaciones accedan al micrófono"** esté activado.
* Activa también **"Permitir que las aplicaciones de escritorio accedan al micrófono"**.

---

## 🚀 Cómo usar AudioMax Twin Pro

1. Abre la aplicación.
2. En el panel **Dispositivo de entrada (Cable Virtual)**, selecciona el dispositivo de captura (generalmente `CABLE Input (VB-Audio Virtual Cable)`).
3. En la lista de **Altavoces de salida (múltiples)**, haz clic para seleccionar todos los dispositivos físicos por donde quieres que salga el sonido (puedes seleccionar varios manteniendo presionada la tecla `Ctrl`).
4. Haz clic en el botón **▶ Aplicar**.
5. Verifica en el **Registro de actividad** inferior que la captura se haya iniciado correctamente y que el formato coincida con el de tu sistema.
6. ¡Disfruta del audio sincronizado! Para detener el ruteo, simplemente haz clic en **⏹ Detener** o cierra la aplicación.

---

## 🐛 Solución de Problemas Frecuentes

* **No sale ningún sonido tras darle a Aplicar:** 
  Verifica que el cable virtual no esté en *Modo Exclusivo* en Windows. Ve a las propiedades del dispositivo > Opciones Avanzadas y desmarca "Permitir que las aplicaciones tomen el control exclusivo de este dispositivo".
* **Error de Formato o Crash al iniciar:** 
  Las frecuencias (Hz) entre el dispositivo de entrada y los de salida no coinciden. Revisa el Paso 2 de la configuración.
* **El log muestra que captura, pero hay silencio:**
  Verifica que el volumen principal de Windows no esté en 0% o muteado, ya que algunos drivers suspenden el flujo de datos. Asegúrate de haber realizado el Paso 3 (Privacidad del micrófono).

---

## 👨‍💻 Tecnologías Utilizadas
* C# / .NET
* [NAudio](https://github.com/naudio/NAudio) - Librería principal para el manejo de las APIs de audio de Windows.