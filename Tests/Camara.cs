using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using OpenCvSharp;

namespace IntegracionCajeroPruebas.Tests;

// Item 12 del BEEX, página 2: "Camera (face recognition)" GDZS-DC004-MG3021. El propio dictamen técnico
// dice que es una "cámara USB nativa... detectada por el Kernel de Windows 11 IoT como webcam genérica" y
// que se integra "utilizando librerías estándar de captura multimedia de .NET o wrappers de OpenCV" — por
// eso se usa OpenCvSharp (VideoCapture) en vez de un protocolo propio como en el resto de Protocolos/.
// No incluye reconocimiento facial: solo valida que se puede abrir la cámara y capturar una imagen fija.
public static partial class HardwareTests
{
    public static void Test_PingCamara()
    {
        var cfg = HardwareConfig.Actual.Camara;
        var r = new Reporte($"Cámara USB - Ping (dispositivo {cfg.IndiceDispositivo})");
        try
        {
            using var captura = new VideoCapture(cfg.IndiceDispositivo);
            r.Criterio("La cámara se abre (VideoCapture.IsOpened)", captura.IsOpened());
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    public static void Test_CapturarRostro()
    {
        var cfg = HardwareConfig.Actual.Camara;
        var r = new Reporte($"Cámara USB (item 12, reconocimiento facial) - dispositivo {cfg.IndiceDispositivo}");
        try
        {
            using var captura = new VideoCapture(cfg.IndiceDispositivo);
            if (!r.Criterio("La cámara se abre (VideoCapture.IsOpened)", captura.IsOpened())) { r.Finalizar(); return; }

            Consola.Aviso("  >> Párese frente a la cámara, mirando al lente, con buena luz, antes de ejecutar esta opción.");

            using var frame = new Mat();
            var leido = false;
            // Los primeros frames de muchas webcams llegan vacíos o negros mientras el sensor ajusta
            // exposición/balance de blancos; se reintenta en vez de fallar con el primer Read().
            for (var intento = 0; intento < 10 && !leido; intento++)
            {
                leido = captura.Read(frame) && !frame.Empty();
                if (!leido) Thread.Sleep(100);
            }
            if (!r.Criterio("Se capturó un frame no vacío", leido)) { r.Finalizar(); return; }

            Directory.CreateDirectory(cfg.CarpetaCapturas);
            var ruta = Path.Combine(cfg.CarpetaCapturas, $"captura_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
            Cv2.ImWrite(ruta, frame);
            // Sin confirmación oral del asistente: la consola corre en la misma IoT, así que el desarrollador
            // revisa el archivo directamente en Capturas/ en vez de preguntarle a alguien que no puede verlo.
            r.Criterio($"Imagen guardada en {ruta} ({frame.Width}x{frame.Height}) — revisar visualmente", File.Exists(ruta));
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }
}
