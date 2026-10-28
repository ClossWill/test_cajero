using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;
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
            Consola.Aviso("  >> Párese frente a la cámara, mirando al lente, con buena luz, antes de ejecutar esta opción.");

            // La cámara a usar es la GDZS-010101-03M-V (visible), que en DirectShow es el índice 1 (confirmado
            // 2026-10-27: 640x480 a color). El índice 0 es la GDZS-020202-02M-IR (grises) y el 2 es un dispositivo
            // que devuelve negro. Como OpenCV no expone nombres, se prueba primero el configurado y solo si no
            // entrega color se recorren los demás.
            using var frame = new Mat();
            var indice = CapturarFrameColor(cfg.IndiceDispositivo, frame);
            if (!r.Criterio("Se capturó un frame a color (no vacío, canales B/G/R distintos)", indice >= 0)) { r.Finalizar(); return; }
            Console.WriteLine($"  Cámara a color: índice {indice}" +
                (indice != cfg.IndiceDispositivo ? $" (distinto del configurado, {cfg.IndiceDispositivo}; actualizar IndiceDispositivo en hardware.json)" : ""));

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

    private const int MaxIndicesCamara = 5;

    private static VideoCapture AbrirCamara(int indice)
    {
        // DirectShow en vez del backend por defecto (Media Foundation): MSMF puede negociar el formato
        // monocromo/IR de la cámara; DSHOW con MJPG pide el flujo comprimido RGB estándar.
        // Se reintenta: al abrir un dispositivo justo después de cerrar el otro (la IR y la visible comparten
        // el mismo hub USB) DirectShow a veces devuelve "no abre" en el primer intento.
        var captura = new VideoCapture(indice, VideoCaptureAPIs.DSHOW);
        for (var intento = 1; intento < 4 && !captura.IsOpened(); intento++)
        {
            captura.Dispose();
            Thread.Sleep(500);
            captura = new VideoCapture(indice, VideoCaptureAPIs.DSHOW);
        }
        if (!captura.IsOpened()) return captura;
        captura.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
        captura.ConvertRgb = true;
        return captura;
    }

    /// <summary>
    /// Toma un frame del flujo de video (no una foto suelta): ubica la cámara a color, graba unos segundos de
    /// video en vivo y guarda el clip (.avi) junto con un frame del centro del clip (.jpg).
    /// </summary>
    public static void Test_CapturarFrameVideo()
    {
        const int segundos = 3;
        var cfg = HardwareConfig.Actual.Camara;
        var r = new Reporte($"Cámara USB - Frame de video a color ({segundos} s de flujo)");
        try
        {
            Consola.Aviso("  >> Párese frente a la cámara, mirando al lente, con buena luz, antes de ejecutar esta opción.");

            using var primero = new Mat();
            var indice = CapturarFrameColor(cfg.IndiceDispositivo, primero);
            if (!r.Criterio("Se encontró una cámara que entrega video a color", indice >= 0)) { r.Finalizar(); return; }

            using var captura = AbrirCamara(indice);
            if (!r.Criterio($"La cámara a color (índice {indice}) se reabre", captura.IsOpened())) { r.Finalizar(); return; }

            Directory.CreateDirectory(cfg.CarpetaCapturas);
            var marca = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var rutaVideo = Path.GetFullPath(Path.Combine(cfg.CarpetaCapturas, $"video_{marca}.avi"));
            var rutaFrame = Path.GetFullPath(Path.Combine(cfg.CarpetaCapturas, $"frame_{marca}.jpg"));

            var fps = captura.Fps is > 1 and < 120 ? captura.Fps : 15;
            using var escritor = new VideoWriter(rutaVideo, VideoWriter.FourCC('M', 'J', 'P', 'G'), fps,
                new Size(primero.Width, primero.Height));
            if (!r.Criterio("Se abre el archivo de video para grabar", escritor.IsOpened())) { r.Finalizar(); return; }

            using var frame = new Mat();
            using var central = new Mat();
            var total = 0;
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            while (reloj.Elapsed.TotalSeconds < segundos)
            {
                if (!captura.Read(frame) || frame.Empty()) { Thread.Sleep(10); continue; }
                escritor.Write(frame);
                total++;
                // Se va quedando el frame más reciente hasta la mitad del tiempo; así el frame guardado
                // sale del flujo ya estabilizado y no del arranque del sensor.
                if (reloj.Elapsed.TotalSeconds <= segundos / 2.0) frame.CopyTo(central);
            }
            escritor.Release();

            if (!r.Criterio($"Se grabaron frames del flujo ({total} en {segundos} s)", total > 0)) { r.Finalizar(); return; }
            if (central.Empty()) frame.CopyTo(central);
            Cv2.ImWrite(rutaFrame, central);
            Console.WriteLine($"  Video: {rutaVideo}");
            r.Criterio($"Frame guardado en {rutaFrame} ({central.Width}x{central.Height}) — revisar visualmente",
                File.Exists(rutaFrame) && EsFrameColor(central));
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>
    /// Abre una ventana con el video en vivo de la cámara. La cámara se identifica por nombre DirectShow
    /// (CamaraConfig.NombreDispositivo): en el ATM resuelve sola a la GDZS-010101-03M-V; en la laptop, donde ese
    /// nombre no existe, lista las cámaras disponibles y pregunta cuál usar.
    /// </summary>
    public static void Test_VistaPreviaCamara()
    {
        const string ventana = "Vista previa - Camara FINSO";
        var cfg = HardwareConfig.Actual.Camara;
        var r = new Reporte("Cámara USB - Vista previa en vivo");
        try
        {
            var indice = ResolverIndiceCamara(cfg, out var nombre);
            if (!r.Criterio("Se identificó el dispositivo de cámara", indice >= 0)) { r.Finalizar(); return; }

            using var captura = AbrirCamara(indice);
            if (!r.Criterio($"La cámara \"{nombre}\" (índice {indice}) se abre", captura.IsOpened())) { r.Finalizar(); return; }

            Consola.Aviso("  >> Ventana abierta. ESC o Q (o cerrar la ventana) termina; S guarda una foto.");
            using var frame = new Mat();
            using var vista = new Mat();
            var mostrada = false;
            var cuadros = 0;
            var fotos = 0;
            var fps = 0.0;
            var cuadrosEnSegundo = 0;
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var ultimoCalculo = TimeSpan.Zero;
            var ultimoFrame = TimeSpan.Zero;
            try
            {
                while (true)
                {
                    if (captura.Read(frame) && !frame.Empty())
                    {
                        cuadros++;
                        cuadrosEnSegundo++;
                        ultimoFrame = reloj.Elapsed;
                        if (reloj.Elapsed - ultimoCalculo >= TimeSpan.FromSeconds(1))
                        {
                            fps = cuadrosEnSegundo / (reloj.Elapsed - ultimoCalculo).TotalSeconds;
                            cuadrosEnSegundo = 0;
                            ultimoCalculo = reloj.Elapsed;
                        }
                        frame.CopyTo(vista);
                        // Hershey solo dibuja ASCII: sin acentos en el rótulo.
                        Cv2.PutText(vista, $"{nombre} | idx {indice} | {frame.Width}x{frame.Height} | {fps:F0} fps",
                            new Point(10, 24), HersheyFonts.HersheySimplex, 0.6, Scalar.Black, 4);
                        Cv2.PutText(vista, $"{nombre} | idx {indice} | {frame.Width}x{frame.Height} | {fps:F0} fps",
                            new Point(10, 24), HersheyFonts.HersheySimplex, 0.6, Scalar.White, 1);
                        Cv2.ImShow(ventana, vista);
                        mostrada = true;
                    }
                    else if (reloj.Elapsed - ultimoFrame > TimeSpan.FromSeconds(10))
                    {
                        r.Criterio("La cámara sigue entregando frames", false, "sin frames durante 10 s");
                        break;
                    }

                    var tecla = Cv2.WaitKey(1);
                    if (tecla is 27 or 'q' or 'Q') break;
                    if (tecla is 's' or 'S' && !frame.Empty())
                    {
                        Directory.CreateDirectory(cfg.CarpetaCapturas);
                        var ruta = Path.GetFullPath(Path.Combine(cfg.CarpetaCapturas, $"vista_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"));
                        Cv2.ImWrite(ruta, frame);
                        fotos++;
                        Console.WriteLine($"  Foto guardada: {ruta}");
                    }
                    // Cerrar con la X de la ventana: la propiedad Visible baja a 0 (solo válida tras el primer ImShow).
                    if (mostrada && Cv2.GetWindowProperty(ventana, WindowPropertyFlags.Visible) < 1) break;
                }
            }
            finally { Cv2.DestroyAllWindows(); }

            r.Criterio($"Se mostró video en vivo ({cuadros} cuadros en {reloj.Elapsed.TotalSeconds:F0} s, {fotos} foto(s) guardada(s))", cuadros > 0);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>
    /// Índice DSHOW de la cámara cuyo nombre contiene NombreDispositivo. Si no hay coincidencia (p. ej. en la
    /// laptop de desarrollo) lista las cámaras y deja elegir. Devuelve -1 si no hay ninguna cámara.
    /// </summary>
    private static int ResolverIndiceCamara(CamaraConfig cfg, out string nombre)
    {
        nombre = "";
        var camaras = DirectShowCamaras.Listar();
        if (camaras.Count == 0) { Consola.Error("  DirectShow no reporta ninguna cámara."); return -1; }

        Console.WriteLine("  Cámaras detectadas (índice DirectShow):");
        for (var i = 0; i < camaras.Count; i++) Console.WriteLine($"    {i}: {camaras[i]}");

        if (!string.IsNullOrWhiteSpace(cfg.NombreDispositivo))
        {
            var coincidencia = camaras.FindIndex(c => c.Contains(cfg.NombreDispositivo, StringComparison.OrdinalIgnoreCase));
            if (coincidencia >= 0)
            {
                nombre = camaras[coincidencia];
                Consola.Ok($"  Cámara \"{nombre}\" encontrada por nombre (índice {coincidencia}).");
                return coincidencia;
            }
            Consola.Aviso($"  No hay una cámara con \"{cfg.NombreDispositivo}\" (¿entorno local?).");
        }

        Console.Write($"  Índice de cámara a usar [0]: ");
        var elegido = int.TryParse(Console.ReadLine(), out var n) ? n : 0;
        if (elegido < 0 || elegido >= camaras.Count) { Consola.Error($"  Índice {elegido} fuera de rango."); return -1; }
        nombre = camaras[elegido];
        return elegido;
    }

    /// <summary>Devuelve el índice de la primera cámara que entrega un frame a color (dejado en <paramref name="frame"/>), o -1.</summary>
    private static int CapturarFrameColor(int preferido, Mat frame)
    {
        var orden = new[] { preferido }.Concat(Enumerable.Range(0, MaxIndicesCamara).Where(i => i != preferido));
        foreach (var i in orden)
        {
            // DirectShow en vez del backend por defecto (Media Foundation): MSMF puede negociar el formato
            // monocromo/IR de la cámara; DSHOW con MJPG pide el flujo comprimido RGB estándar.
            using var captura = AbrirCamara(i);
            if (!captura.IsOpened()) { Console.WriteLine($"  Índice {i}: no abre"); continue; }

            // Los primeros frames de muchas webcams llegan vacíos o negros mientras el sensor ajusta
            // exposición/balance de blancos; se descartan varios y se conserva el último leído.
            using var ultimo = new Mat();
            var leidos = 0;
            for (var intento = 0; intento < 30 && leidos < 15; intento++)
            {
                if (captura.Read(ultimo) && !ultimo.Empty()) leidos++; else Thread.Sleep(100);
            }
            if (leidos == 0) { Console.WriteLine($"  Índice {i}: sin frames"); continue; }

            var color = EsFrameColor(ultimo);
            Console.WriteLine($"  Índice {i}: {ultimo.Width}x{ultimo.Height}, {(color ? "color" : "escala de grises (¿infrarroja?)")}");
            if (!color) continue;
            ultimo.CopyTo(frame);
            return i;
        }
        return -1;
    }

    // Una imagen en gris (IR) tiene B=G=R en cada píxel aunque venga empaquetada en 3 canales.
    private static bool EsFrameColor(Mat f)
    {
        if (f.Channels() < 3) return false;
        var canales = Cv2.Split(f);
        try
        {
            using var d1 = new Mat();
            using var d2 = new Mat();
            Cv2.Absdiff(canales[0], canales[1], d1);
            Cv2.Absdiff(canales[1], canales[2], d2);
            return Cv2.Mean(d1).Val0 + Cv2.Mean(d2).Val0 > 4;
        }
        finally { foreach (var c in canales) c.Dispose(); }
    }
}
