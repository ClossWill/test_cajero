using System.Runtime.InteropServices;
using System.Text;
using OpenCvSharp;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>Comandos ESC/POS estándar usados en la prueba de la KP-80A.</summary>
public static class EscPos
{
    public static readonly byte[] Inicializar = { 0x1B, 0x40 };          // ESC @
    public static readonly byte[] NegritaOn = { 0x1B, 0x45, 0x01 };      // ESC E 1
    public static readonly byte[] NegritaOff = { 0x1B, 0x45, 0x00 };     // ESC E 0
    public static readonly byte[] Centrar = { 0x1B, 0x61, 0x01 };        // ESC a 1
    public static readonly byte[] Izquierda = { 0x1B, 0x61, 0x00 };      // ESC a 0
    public static readonly byte[] SaltoLinea = { 0x0A };
    public static readonly byte[] AvanzarYCortar = { 0x1D, 0x56, 0x42, 0x00 }; // GS V 66 0: avanza y corte parcial
    public static readonly byte[] EstadoEnLinea = { 0x10, 0x04, 0x01 };  // DLE EOT 1 (solo canal serial)

    public static byte[] Texto(string s) => Encoding.ASCII.GetBytes(s);

    public static byte[] Concatenar(params byte[][] partes) => partes.SelectMany(p => p).ToArray();

    /// <summary>Respuesta a DLE EOT 1: bit 3 en 1 = fuera de línea.</summary>
    public static bool EstaEnLinea(byte estado) => (estado & 0x08) == 0;

    /// <summary>GS ! n (select character size): escala 0-7 (x1 a x8) de ancho y alto para el texto siguiente.
    /// EscalaTexto(0, 0) vuelve al tamaño normal. Estándar ESC/POS, no exclusivo del SDK del fabricante.</summary>
    public static byte[] EscalaTexto(int escalaAncho, int escalaAlto) =>
        new byte[] { 0x1D, 0x21, (byte)(((escalaAncho & 0x07) << 4) | (escalaAlto & 0x07)) };

    /// <summary>GS v 0 (print raster bit image): arma el comando a partir de bits ya empaquetados a 1bpp
    /// (ver <see cref="ImagenDesdeArchivo"/>). Estándar ESC/POS, no exclusivo del SDK del fabricante.</summary>
    public static byte[] ImagenRaster(byte[] datosEmpaquetados, int anchoDots, int altoDots)
    {
        var xBytes = (anchoDots + 7) / 8;
        var encabezado = new byte[]
        {
            0x1D, 0x76, 0x30, 0x00, // GS v 0, m=0 (normal)
            (byte)(xBytes & 0xFF), (byte)((xBytes >> 8) & 0xFF),
            (byte)(altoDots & 0xFF), (byte)((altoDots >> 8) & 0xFF),
        };
        return Concatenar(encabezado, datosEmpaquetados);
    }

    /// <summary>Carga una imagen (cualquier formato que lea OpenCV), la escala a anchoDots x altoDots y la
    /// convierte a 1bpp empaquetado (bit=1 → imprime negro) con umbral simple, listo para <see cref="ImagenRaster"/>.</summary>
    public static byte[] ImagenDesdeArchivo(string ruta, int anchoDots, int altoDots)
    {
        using var gris = Cv2.ImRead(ruta, ImreadModes.Grayscale);
        using var escalada = new Mat();
        Cv2.Resize(gris, escalada, new Size(anchoDots, altoDots));
        using var binaria = new Mat();
        Cv2.Threshold(escalada, binaria, 128, 255, ThresholdTypes.BinaryInv); // oscuro del logo -> bit en 1

        var xBytes = (anchoDots + 7) / 8;
        var datos = new byte[xBytes * altoDots];
        for (var y = 0; y < altoDots; y++)
            for (var x = 0; x < anchoDots; x++)
                if (binaria.At<byte>(y, x) != 0)
                    datos[y * xBytes + x / 8] |= (byte)(0x80 >> (x % 8));
        return datos;
    }
}

/// <summary>Envía bytes crudos a una impresora instalada en Windows (USB) a través del spooler, sin driver gráfico.</summary>
public static class ImpresoraRaw
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? DocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? DataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string nombre, out IntPtr h, IntPtr defaults);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr h);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(IntPtr h, int nivel, [In] DocInfo doc);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr h);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr h);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr h);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr h, IntPtr datos, int cantidad, out int escritos);

    /// <summary>Abre y cierra la cola sin imprimir nada: confirma que existe y responde, para el ping.</summary>
    public static bool Existe(string nombreImpresora)
    {
        if (!OpenPrinter(nombreImpresora, out var h, IntPtr.Zero)) return false;
        ClosePrinter(h);
        return true;
    }

    public static void Enviar(string nombreImpresora, byte[] bytes)
    {
        if (!OpenPrinter(nombreImpresora, out var h, IntPtr.Zero))
            throw new InvalidOperationException(
                $"No se pudo abrir la impresora '{nombreImpresora}' (Win32 {Marshal.GetLastWin32Error()}). Revisar Get-Printer.");

        var p = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, p, bytes.Length);
            var doc = new DocInfo { DocName = "Prueba FINSO", DataType = "RAW" };
            if (!StartDocPrinter(h, 1, doc)) throw new InvalidOperationException($"StartDocPrinter Win32 {Marshal.GetLastWin32Error()}");
            try
            {
                StartPagePrinter(h);
                if (!WritePrinter(h, p, bytes.Length, out var escritos) || escritos != bytes.Length)
                    throw new InvalidOperationException($"WritePrinter escribió {escritos}/{bytes.Length} bytes (Win32 {Marshal.GetLastWin32Error()})");
                EndPagePrinter(h);
            }
            finally { EndDocPrinter(h); }
        }
        finally
        {
            Marshal.FreeHGlobal(p);
            ClosePrinter(h);
        }
    }
}
