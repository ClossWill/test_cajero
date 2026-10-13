using System.Runtime.InteropServices;
using System.Text;

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
