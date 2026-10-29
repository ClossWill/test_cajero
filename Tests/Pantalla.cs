using System.Runtime.InteropServices;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using OpenCvSharp;

namespace IntegracionCajeroPruebas.Tests;

// Partidas 3 y 4 del BEEX: "Display & Touch screens" (19" táctil 1280x1024 + pantalla superior 21.5", HDMI/VGA, touch
// por USB HID). El dictamen técnico dice que no requieren programación: Windows las ve como monitores normales. Esta
// prueba solo valida que se puede dibujar en la pantalla periférica (la superior, no la principal): enumera los
// monitores con la API de Windows y abre una ventana sin bordes que cubre el monitor elegido con un patrón de prueba.
// Se usa OpenCV (ya referenciado) para no agregar WinForms/WPF y su runtime de escritorio.
public static partial class HardwareTests
{
    public static void Test_PingPantallas()
    {
        var r = new Reporte("Pantallas - Ping (monitores que ve Windows)");
        try
        {
            var monitores = Monitores.Listar();
            ImprimirMonitores(monitores);
            r.Criterio("Windows reporta al menos un monitor", monitores.Count > 0);
            r.Criterio("Hay una pantalla periférica (segundo monitor activo)", monitores.Any(m => !m.Principal));
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    /// <summary>
    /// Cubre la pantalla periférica con un patrón de prueba. Teclas: ESPACIO/flecha derecha = siguiente patrón,
    /// flecha izquierda = anterior, ESC o Q = cierra. Si la pantalla es táctil, cada toque deja una marca con sus
    /// coordenadas (Windows lo entrega como clic de ratón).
    /// </summary>
    public static void Test_PantallaPeriferica()
    {
        const string ventana = "Prueba de pantalla - FINSO";
        var r = new Reporte("Pantalla periférica - Patrón de prueba");
        try
        {
            var monitores = Monitores.Listar();
            ImprimirMonitores(monitores);
            if (!r.Criterio("Windows reporta al menos un monitor", monitores.Count > 0)) { r.Finalizar(); return; }

            var destino = ElegirMonitor(monitores, out var enPrincipal);
            if (!r.Criterio("Se identificó el monitor a probar", destino is not null)) { r.Finalizar(); return; }
            var m = destino!.Value;
            if (enPrincipal) Consola.Aviso("  No hay segundo monitor: el patrón se muestra en una ventana sobre el principal.");

            var ancho = enPrincipal ? m.Ancho / 2 : m.Ancho;
            var alto = enPrincipal ? m.Alto / 2 : m.Alto;
            Consola.Aviso($"  >> Patrón en {m.Nombre} ({m.Ancho}x{m.Alto}). ESPACIO cambia de patrón, ESC o Q cierra; toque/clic deja una marca.");

            var toques = new List<Point>();
            var tocado = false;
            Cv2.NamedWindow(ventana, WindowFlags.Normal | WindowFlags.GuiNormal);
            Cv2.SetMouseCallback(ventana, (evento, x, y, _, _) =>
            {
                if (evento != MouseEventTypes.LButtonDown) return;
                lock (toques) { toques.Add(new Point(x, y)); }
                tocado = true;
                Console.WriteLine($"  Toque/clic en ({x}, {y})");
            });
            Cv2.ResizeWindow(ventana, ancho, alto);
            Cv2.ImShow(ventana, Patron(0, ancho, alto, m, toques));
            Cv2.WaitKey(1);
            var colocada = enPrincipal ? Monitores.ColocarVentana(ventana, m.X + m.Ancho / 4, m.Y + m.Alto / 4, ancho, alto, sinBordes: false)
                                       : Monitores.ColocarVentana(ventana, m.X, m.Y, ancho, alto, sinBordes: true);
            r.Criterio("La ventana de prueba se colocó sobre el monitor", colocada);

            var patron = 0;
            var cambios = 0;
            try
            {
                while (true)
                {
                    lock (toques) { Cv2.ImShow(ventana, Patron(patron, ancho, alto, m, toques)); }
                    var tecla = Cv2.WaitKeyEx(100); // WaitKeyEx: devuelve también las flechas (códigos extendidos)
                    if (tecla is 27 or 'q' or 'Q') break;
                    if (tecla is ' ' or 2555904 or 'n' or 'N') { patron = (patron + 1) % NumeroPatrones; cambios++; }
                    else if (tecla is 2424832 or 'p' or 'P') { patron = (patron + NumeroPatrones - 1) % NumeroPatrones; cambios++; }
                    else if (tecla is 'c' or 'C') lock (toques) { toques.Clear(); }
                    if (Cv2.GetWindowProperty(ventana, WindowPropertyFlags.Visible) < 1) break;
                }
            }
            finally { Cv2.DestroyAllWindows(); }

            // Sin confirmación del asistente: el desarrollador mira la pantalla directamente (ver videollamada en la IoT).
            r.Criterio($"Patrón mostrado en {m.Nombre} ({cambios} cambio(s) de patrón) — revisar visualmente", true);
            if (tocado) Consola.Ok("  La pantalla respondió a toques/clics.");
            else Consola.Info("  No se registraron toques/clics (normal si la pantalla periférica no es táctil).");
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    private const int NumeroPatrones = 8;

    private static void ImprimirMonitores(List<Monitores.Monitor> monitores)
    {
        Console.WriteLine($"  Monitores detectados: {monitores.Count}");
        for (var i = 0; i < monitores.Count; i++)
        {
            var m = monitores[i];
            Console.WriteLine($"    {i}: {m.Nombre} | {m.Ancho}x{m.Alto} en ({m.X}, {m.Y}) | {(m.Principal ? "PRINCIPAL" : "secundario")}");
        }
    }

    /// <summary>
    /// Monitor periférico = el primero que no es el principal (en el ATM, la pantalla superior). Con más de dos
    /// pregunta cuál; sin secundario cae al principal (laptop de desarrollo) y lo indica en <paramref name="enPrincipal"/>.
    /// </summary>
    private static Monitores.Monitor? ElegirMonitor(List<Monitores.Monitor> monitores, out bool enPrincipal)
    {
        enPrincipal = false;
        var secundarios = monitores.Where(m => !m.Principal).ToList();
        if (secundarios.Count == 0) { enPrincipal = true; return monitores.First(m => m.Principal); }
        if (secundarios.Count == 1) return secundarios[0];

        Console.Write($"  Índice de monitor a probar [{monitores.IndexOf(secundarios[0])}]: ");
        var texto = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(texto)) return secundarios[0];
        if (int.TryParse(texto, out var n) && n >= 0 && n < monitores.Count) return monitores[n];
        Consola.Error($"  Índice {texto} fuera de rango.");
        return null;
    }

    // Colores en BGR. Los rótulos usan solo ASCII (Hershey no dibuja acentos).
    private static Mat Patron(int tipo, int ancho, int alto, Monitores.Monitor m, List<Point> toques)
    {
        var img = new Mat(alto, ancho, MatType.CV_8UC3, Scalar.Black);
        string nombre;
        switch (tipo)
        {
            case 0: // barras de color SMPTE simplificadas
                nombre = "Barras de color";
                Scalar[] barras = { new(255, 255, 255), new(0, 255, 255), new(255, 255, 0), new(0, 255, 0), new(255, 0, 255), new(0, 0, 255), new(255, 0, 0), new(0, 0, 0) };
                for (var i = 0; i < barras.Length; i++)
                    Cv2.Rectangle(img, new Rect(i * ancho / barras.Length, 0, ancho / barras.Length + 1, alto), barras[i], -1);
                break;
            case 1: nombre = "Rojo"; img.SetTo(new Scalar(0, 0, 255)); break;
            case 2: nombre = "Verde"; img.SetTo(new Scalar(0, 255, 0)); break;
            case 3: nombre = "Azul"; img.SetTo(new Scalar(255, 0, 0)); break;
            case 4: nombre = "Blanco"; img.SetTo(Scalar.White); break;
            case 5: nombre = "Negro"; break;
            case 6: // degradado horizontal de grises
                nombre = "Degradado de grises";
                for (var x = 0; x < ancho; x++)
                {
                    var g = x * 255 / Math.Max(1, ancho - 1);
                    Cv2.Line(img, new Point(x, 0), new Point(x, alto), new Scalar(g, g, g));
                }
                break;
            default: // cuadrícula de 100 px con diagonales: detecta recortes, escalado y distorsión
                nombre = "Cuadricula 100 px";
                for (var x = 0; x < ancho; x += 100) Cv2.Line(img, new Point(x, 0), new Point(x, alto), Scalar.White);
                for (var y = 0; y < alto; y += 100) Cv2.Line(img, new Point(0, y), new Point(ancho, y), Scalar.White);
                Cv2.Line(img, new Point(0, 0), new Point(ancho, alto), new Scalar(0, 255, 255), 2);
                Cv2.Line(img, new Point(ancho, 0), new Point(0, alto), new Scalar(0, 255, 255), 2);
                Cv2.Circle(img, new Point(ancho / 2, alto / 2), Math.Min(ancho, alto) / 3, new Scalar(0, 255, 0), 2);
                break;
        }

        // Marco de 1 px al borde: si falta algún lado, la imagen está recortada (overscan) en esa pantalla.
        Cv2.Rectangle(img, new Rect(0, 0, ancho, alto), new Scalar(255, 0, 255), 3);

        var texto = $"{nombre} ({tipo + 1}/{NumeroPatrones}) | {m.Nombre} {m.Ancho}x{m.Alto} | {DateTime.Now:HH:mm:ss}";
        var pie = "ESPACIO: siguiente | P: anterior | C: borra toques | ESC/Q: cerrar";
        var escala = Math.Max(0.6, ancho / 1600.0);
        // Contorno negro + relleno blanco para que se lea sobre cualquier fondo.
        foreach (var (grosor, color) in new[] { (5, Scalar.Black), (2, Scalar.White) })
        {
            Cv2.PutText(img, texto, new Point(20, (int)(50 * escala) + 10), HersheyFonts.HersheySimplex, escala, color, grosor);
            Cv2.PutText(img, pie, new Point(20, alto - 25), HersheyFonts.HersheySimplex, escala * 0.8, color, grosor);
        }
        foreach (var t in toques)
        {
            Cv2.Circle(img, t, 22, new Scalar(0, 0, 255), 3);
            Cv2.Circle(img, t, 3, Scalar.White, -1);
            Cv2.PutText(img, $"({t.X},{t.Y})", new Point(t.X + 28, t.Y - 8), HersheyFonts.HersheySimplex, 0.6, Scalar.Black, 4);
            Cv2.PutText(img, $"({t.X},{t.Y})", new Point(t.X + 28, t.Y - 8), HersheyFonts.HersheySimplex, 0.6, Scalar.White, 1);
        }
        return img;
    }
}

/// <summary>Monitores de Windows (user32) y colocación de una ventana de OpenCV sobre uno de ellos.</summary>
public static class Monitores
{
    public readonly record struct Monitor(string Nombre, int X, int Y, int Ancho, int Alto, bool Principal);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangulo { public int Izquierda, Arriba, Derecha, Abajo; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InfoMonitor
    {
        public int Tamano;
        public Rectangulo Monitor, Trabajo;
        public int Banderas;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Dispositivo;
    }

    private delegate bool EnumMonitoresProc(IntPtr monitor, IntPtr hdc, ref Rectangulo rect, IntPtr dato);

    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr recorte, EnumMonitoresProc proc, IntPtr dato);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref InfoMonitor info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? clase, string titulo);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int indice);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int indice, int valor);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr despuesDe, int x, int y, int ancho, int alto, uint banderas);
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr contexto);

    private const int GwlStyle = -16;
    private const int WsCaption = 0x00C00000, WsThickFrame = 0x00040000, WsPopup = unchecked((int)0x80000000);
    private const uint SwpNoZOrder = 0x0004, SwpFrameChanged = 0x0020, SwpShowWindow = 0x0040;
    private static readonly IntPtr PerMonitorV2 = new(-4);
    private static bool _dpiFijado;

    public static List<Monitor> Listar()
    {
        // Sin esto, con escala de pantalla distinta de 100 % Windows entrega coordenadas "virtualizadas" y la ventana
        // no coincide con los píxeles reales del monitor. Solo surte efecto si aún no se creó ninguna ventana.
        if (!_dpiFijado) { _dpiFijado = true; try { SetProcessDpiAwarenessContext(PerMonitorV2); } catch { /* mejor esfuerzo */ } }

        var lista = new List<Monitor>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref Rectangulo _, IntPtr _) =>
        {
            var info = new InfoMonitor { Tamano = Marshal.SizeOf<InfoMonitor>() };
            if (GetMonitorInfo(h, ref info))
                lista.Add(new Monitor(info.Dispositivo.TrimStart('\\', '.'), info.Monitor.Izquierda, info.Monitor.Arriba,
                    info.Monitor.Derecha - info.Monitor.Izquierda, info.Monitor.Abajo - info.Monitor.Arriba, (info.Banderas & 1) != 0));
            return true;
        }, IntPtr.Zero);
        return lista.OrderByDescending(m => m.Principal).ThenBy(m => m.X).ToList();
    }

    /// <summary>Mueve la ventana de OpenCV (por título) al rectángulo dado; con <paramref name="sinBordes"/> le quita marco y barra de título.</summary>
    public static bool ColocarVentana(string titulo, int x, int y, int ancho, int alto, bool sinBordes)
    {
        var hwnd = FindWindow(null, titulo);
        if (hwnd == IntPtr.Zero) return false;
        // OpenCV envuelve el área de dibujo en una ventana hija: la que lleva el marco es la superior.
        var superior = GetParent(hwnd);
        if (superior != IntPtr.Zero) hwnd = superior;
        if (sinBordes)
        {
            var estilo = GetWindowLong(hwnd, GwlStyle);
            SetWindowLong(hwnd, GwlStyle, (estilo & ~(WsCaption | WsThickFrame)) | WsPopup);
        }
        return SetWindowPos(hwnd, IntPtr.Zero, x, y, ancho, alto, SwpNoZOrder | SwpFrameChanged | SwpShowWindow);
    }
}
