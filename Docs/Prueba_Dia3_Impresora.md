# Prueba Día 3 – Impresora térmica KP-80A

## Objetivo

Imprimir un voucher mínimo con el texto **"Hola Mundo FINSO"** en negritas y ejecutar el corte de papel, demostrando control directo por comandos ESC/POS **y** validando con certeza el canal que se usará en el despliegue productivo (ver "Spooler vs. SDK" más abajo).

Además, `Test_ImprimirVoucherOperacion` prueba el formato real que necesitará el voucher de producción: logo de la empresa arriba y, debajo, los datos de una operación de cambio con el formato de un comprobante tradicional (fecha, tipo de operación, montos entregado/recibido, denominaciones, tipo de cambio, comisión, nivel de identificación, terminal y folio). **Funciona en cualquier modo** (`Spooler`, `Serial` o `UsbSdk`): el logo se arma con `GS v 0` (raster bit image) y el tamaño de fuente con `GS !` — ambos comandos ESC/POS estándar, implementados en `EscPos.cs` (`ImagenDesdeArchivo`/`ImagenRaster`/`EscalaTexto`). Originalmente esto dependía del SDK del fabricante (`CP_Pos_PrintRasterImageFromFile`/`CP_Pos_SetTextScale`), pero el SDK no se pudo destrabar en esta máquina (ver "Supuestos y riesgos") y se reimplementó por ESC/POS crudo para no bloquear la prueba.

## Protocolo / librería

- **ESC/POS** estándar. Comandos: `ESC @` (inicializar), `ESC E 1/0` (negritas), `ESC a n` (alineación), `GS V 66 0` (avanzar y corte parcial), `DLE EOT 1` (estado en línea, solo por serial).
- Tres canales, elegidos con `Impresora.Modo`:
  - `Spooler` (USB): se envían bytes crudos a la cola de Windows con `winspool.Drv` (`datatype = RAW`), sin usar el driver gráfico (`Protocolos/EscPos.cs`, clase `ImpresoraRaw`).
  - `Serial`: escritura directa al COM con `System.IO.Ports`.
  - **`UsbSdk` (recomendado para producción):** usa el SDK nativo del fabricante (`autoreplyprint.dll`, modo "autoreply" por USB) en vez del spooler de Windows. Permite consultar el estado real (papel/tapa/cortador) antes de imprimir y confirmar con `QueryPrintResult` que el ticket salió, no solo que se envió el comando (`Protocolos/AutoReplyPrintSdk.cs`).

### Spooler vs. SDK: por qué importa para producción

El modo `Spooler` basta para esta prueba de hardware, pero **no** es el canal recomendado para la app oficial:

1. **No confirma impresión real.** `WritePrinter` confirma que el spooler de Windows recibió los bytes, no que la impresora los imprimió. El servicio de cola puede encolar, reintentar o colgarse sin que la app se entere — en un flujo de dispensación de efectivo, no saber si el comprobante salió es un problema de conciliación.
2. **Sin estado antes de imprimir.** Hay que saber si hay papel, si la tapa está cerrada y si el cortador está libre *antes* de imprimir, no después de que el cliente ya espera el ticket. El SDK, con `autoreplymode=1`, hace que la impresora reporte su estado solo y permite consultarlo con `CP_Pos_QueryRTStatus`.
3. **Dependencia del servicio Spooler de Windows**, que se puede detener, bloquear por otro trabajo o fallar en frío tras un reinicio — un punto de falla fuera del control de la app en un kiosco.

Por eso esta prueba implementa también el modo `UsbSdk`: valida en la máquina real el mismo canal (estado + confirmación de impresión) que se extraerá a la app oficial, en vez de validar solo el camino corto del spooler.

**Actualización (2026-10-17): el modo `UsbSdk` no se pudo destrabar en esta máquina** — ver el detalle completo en "Supuestos y riesgos". `Impresora.Modo` quedó de vuelta en `Spooler` (el único que de verdad imprime hoy) y el logo/tamaños de fuente se reimplementaron por ESC/POS crudo (ver arriba) para no depender del SDK. El razonamiento de esta sección sigue siendo válido como objetivo a futuro para la app oficial — solo que en esta máquina concreta hace falta resolver el acceso del SDK primero (posiblemente con ayuda del proveedor/BEEX).

## Precondiciones

- Impresora encendida, con rollo de 80 mm y tapa cerrada.
- Modo Spooler: driver instalado y nombre exacto de la cola (`Get-Printer | Select Name`).
- Modo Serial: puerto y baudios correctos (115200 por defecto; confirmar con el autotest de la impresora).
- Modo UsbSdk: `autoreplyprint.dll` (win32) junto al `.exe` — la compila el `.csproj` desde `referencias_integracion/Receipt Printer(KP-80A)/autoreplyprint_dll_win32/`. Solo una impresora USB conectada (si hay varias, hace falta implementar `CP_Port_EnumUsb` para elegir).

## Configuración

```json
"Impresora": {
  "Modo": "Spooler",
  "NombreImpresora": "POS80 Printer",
  "Puerto": "COM4",
  "BaudRate": 115200,
  "NombreUsb": "POS80",
  "RutaLogo": "logo.png"
}
```

`RutaLogo` es relativa a la carpeta del `.exe`; el `.csproj` copia `logo.png` (raíz del repo) junto al build. Solo la usa `Test_ImprimirVoucherOperacion`.

## Código

### `Protocolos/EscPos.cs`

```csharp
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
```

### `Protocolos/AutoReplyPrintSdk.cs` (modo `UsbSdk`)

Wrapper propio por P/Invoke directo a `autoreplyprint.dll` nativa (cdecl, cadenas ANSI) en vez del wrapper administrado `autoreplyprint.cs.dll` del SDK (es una DLL .NET Framework antigua; se evita esa dependencia, igual que `EscPos.cs`/`CcTalk.cs` implementan el protocolo propio).

```csharp
using System.Runtime.InteropServices;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>
/// Wrapper mínimo propio sobre el SDK nativo del fabricante de la KP-80A (<c>autoreplyprint.dll</c>,
/// carpeta <c>referencias_integracion/Receipt Printer(KP-80A)/.../windowsSDK.rar/C#/.../autoreplyprint.c/dll/win32</c>).
/// Se invoca el .dll nativo directamente (P/Invoke, calling convention cdecl por defecto en el .h del SDK,
/// cadenas ANSI) en vez de referenciar el wrapper administrado "autoreplyprint.cs.dll" que trae el SDK:
/// es una DLL .NET Framework antigua y así se evita esa dependencia, igual que <see cref="EscPos"/> y
/// <see cref="CcTalkBus"/> implementan su protocolo propio en vez de usar la librería del fabricante.
///
/// Este canal (USB + modo "autoreply") es el que GRG/BEEX documentan para producción: a diferencia del
/// spooler de Windows (<see cref="ImpresoraRaw"/>), permite consultar estado real (papel, tapa, cortador)
/// antes de imprimir y confirmar con <see cref="QueryPrintResult"/> que el ticket salió, no solo que se
/// envió. Ver "Supuestos y riesgos" en Docs/Prueba_Dia3_Impresora.md.
/// </summary>
public static class AutoReplyPrintSdk
{
    private const string Dll = "autoreplyprint.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr CP_Port_OpenUsb(string name, int autoreplymode);

    // CP_ComDataBits_8=8, CP_ComParity_NoParity=0, CP_ComStopBits_One=0, CP_ComFlowControl_None=0 (valores
    // del enum en autoreplyprint.h): mismo 8N1 sin control de flujo que ya usa el modo "Serial" (EnviarPorSerial).
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr CP_Port_OpenCom(string name, uint baudrate, int databits, int parity, int stopbits,
        int flowcontrol, int autoreplymode);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Port_Close(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_ResetPrinter(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_SetAlignment(IntPtr handle, int nAlignment);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_SetTextBold(IntPtr handle, int nBold);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int CP_Pos_PrintText(IntPtr handle, string str);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_FeedLine(IntPtr handle, int numLines);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_FullCutPaper(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_QueryRTStatus(IntPtr handle, uint timeout);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_QueryPrintResult(IntPtr handle, uint timeout);

    // Logo/tamaño de fuente (autoreplyprint.h §"print image"/"set text scale"): el .h no aclara si
    // dstw/dsth=0 conserva el tamaño original, así que ImprimirImagen siempre pide ambos en puntos (dots).
    // nWidthScale/nHeightScale tampoco traen rango documentado; se asume 0-7 (x1 a x8), el mismo patrón
    // que ESC/POS GS ! estándar — pendiente de confirmar con una impresión física real.
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int CP_Pos_PrintRasterImageFromFile(IntPtr handle, int dstw, int dsth, string pszFile,
        int binaryzationMethod, int compressionMethod);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Pos_SetTextScale(IntPtr handle, int nWidthScale, int nHeightScale);

    public const int AlineacionIzquierda = 0;
    public const int AlineacionCentro = 1;
    public const int AlineacionDerecha = 2;

    // CP_ImageBinarizationMethod.
    public const int BinarizacionDithering = 0;
    public const int BinarizacionUmbral = 1;
    public const int BinarizacionDifusionError = 2;

    // CP_ImageCompressionMethod.
    public const int SinCompresion = 0;

    /// <summary>Abre el puerto USB (interfaz "USB Printing Support") en modo autoreply.
    /// <paramref name="nombre"/> puede ser cualquier texto si solo hay una impresora USB conectada
    /// (así lo documenta el SDK); null/handle inválido si no la encuentra.
    /// En la máquina real (2026-10-16/17) este Open falla con el SDK aunque detecta un dispositivo USB (log
    /// propio de la DLL, VID 0x0FE6/PID 0x811E), con y sin spooler de Windows corriendo, con y sin privilegios
    /// de administrador — pero Windows SÍ ve esta impresora como USB Printing Support genuino (Propiedades >
    /// Puertos de "POS80 Printer" muestra `CP001`, tipo `USBPort`). Se sospecha que el controlador de Windows
    /// retiene el puerto mientras la cola esté instalada; pendiente de confirmar quitando la cola temporalmente.
    /// <see cref="AbrirCom"/> (por COM13/14, el puente CP210x del mismo cable USB) también falló en los
    /// intentos iniciales, antes de este cambio de rumbo — ver Docs/Prueba_Dia3_Impresora.md.</summary>
    public static IntPtr Abrir(string nombre) => CP_Port_OpenUsb(nombre, autoreplymode: 1);

    /// <summary>Abre el puerto serie (virtual, sobre el mismo USB) en modo autoreply: misma SDK, mismo
    /// protocolo "autoreply" que <see cref="Abrir"/>, pero por <c>CP_Port_OpenCom</c> en vez de
    /// <c>CP_Port_OpenUsb</c>. Alternativa sin confirmar aún como la ruta correcta para esta impresora
    /// (ver comentario de <see cref="Abrir"/>); se deja implementada por si <see cref="Abrir"/> no se
    /// puede destrabar.</summary>
    public static IntPtr AbrirCom(string puerto, uint baudios)
    {
        // Para COM10 en adelante, CreateFile (lo que CP_Port_OpenCom hace internamente) exige el prefijo
        // "\\.\": "COM14" a secas falla silenciosamente aunque el puerto exista y esté libre (clásico de
        // Win32, no documentado en autoreplyprint.h). COM1-COM9 funcionan con o sin el prefijo, así que es
        // seguro agregarlo siempre.
        var nombre = puerto.StartsWith(@"\\.\", StringComparison.Ordinal) ? puerto : @"\\.\" + puerto;
        return CP_Port_OpenCom(nombre, baudios, databits: 8, parity: 0, stopbits: 0, flowcontrol: 0, autoreplymode: 1);
    }

    public static bool EsValido(IntPtr handle) => handle != IntPtr.Zero;

    public static void Cerrar(IntPtr handle)
    {
        if (EsValido(handle)) CP_Port_Close(handle);
    }

    public static bool Reset(IntPtr handle) => CP_Pos_ResetPrinter(handle) != 0;

    /// <summary>Imprime una imagen (p.ej. el logo) desde un archivo. <paramref name="anchoDots"/>/<paramref name="altoDots"/>
    /// son el tamaño destino en puntos de impresión, no en píxeles del archivo.</summary>
    public static bool ImprimirImagen(IntPtr handle, string rutaArchivo, int anchoDots, int altoDots) =>
        CP_Pos_PrintRasterImageFromFile(handle, anchoDots, altoDots, rutaArchivo, BinarizacionDithering, SinCompresion) != 0;

    /// <summary>Escala del texto siguiente (ancho, alto); 0 = tamaño normal. Ver nota de rango arriba.</summary>
    public static bool EscalaTexto(IntPtr handle, int escalaAncho, int escalaAlto) =>
        CP_Pos_SetTextScale(handle, escalaAncho, escalaAlto) != 0;
    public static bool Alinear(IntPtr handle, int alineacion) => CP_Pos_SetAlignment(handle, alineacion) != 0;
    public static bool Negrita(IntPtr handle, bool activa) => CP_Pos_SetTextBold(handle, activa ? 1 : 0) != 0;
    public static bool Texto(IntPtr handle, string texto) => CP_Pos_PrintText(handle, texto) != 0;
    public static bool AvanzarLineas(IntPtr handle, int lineas) => CP_Pos_FeedLine(handle, lineas) != 0;
    public static bool CorteCompleto(IntPtr handle) => CP_Pos_FullCutPaper(handle) != 0;

    /// <summary>true si pudo pedir/leer el estado; <paramref name="rtstatus"/> queda en 0 si no responde a tiempo.</summary>
    public static bool ConsultarEstado(IntPtr handle, uint timeoutMs, out int rtstatus)
    {
        rtstatus = CP_Pos_QueryRTStatus(handle, timeoutMs);
        return rtstatus != 0;
    }

    /// <summary>Confirma si el último trabajo de impresión realmente salió (no solo que se envió).</summary>
    public static bool ConsultarResultadoImpresion(IntPtr handle, uint timeoutMs) =>
        CP_Pos_QueryPrintResult(handle, timeoutMs) != 0;

    // Máscaras de CP_RTSTATUS_XXX según autoreplyprint.h (bytes: [0]=general [1]=estado [2]=error [3]=papel).
    public static bool FueraDeLinea(int rtstatus) => ((rtstatus >> 0) & 0x08) == 0x08;
    public static bool TapaAbierta(int rtstatus) => ((rtstatus >> 8) & 0x04) == 0x04;
    public static bool SinPapel(int rtstatus) => ((rtstatus >> 8) & 0x20) == 0x20;
    public static bool ErrorOcurrido(int rtstatus) => ((rtstatus >> 8) & 0x40) == 0x40;
    public static bool ErrorCortador(int rtstatus) => ((rtstatus >> 16) & 0x08) == 0x08;
    public static bool ErrorIrrecuperable(int rtstatus) => ((rtstatus >> 16) & 0x20) == 0x20;
    public static bool PapelPorAgotarse(int rtstatus) => ((rtstatus >> 24) & 0x0C) == 0x0C;

    public static string DescribirEstado(int rtstatus)
    {
        var problemas = new List<string>();
        if (FueraDeLinea(rtstatus)) problemas.Add("fuera de línea");
        if (TapaAbierta(rtstatus)) problemas.Add("tapa abierta");
        if (SinPapel(rtstatus)) problemas.Add("sin papel");
        if (PapelPorAgotarse(rtstatus)) problemas.Add("papel por agotarse");
        if (ErrorCortador(rtstatus)) problemas.Add("error de cortador");
        if (ErrorIrrecuperable(rtstatus)) problemas.Add("error irrecuperable");
        if (ErrorOcurrido(rtstatus)) problemas.Add("error reportado");
        return problemas.Count == 0 ? "sin alertas" : string.Join(", ", problemas);
    }
}
```

### `Tests/Impresora.cs`

```csharp
using System.IO.Ports;
using IntegracionCajeroPruebas.Config;
using IntegracionCajeroPruebas.Helpers;
using IntegracionCajeroPruebas.Protocolos;

namespace IntegracionCajeroPruebas.Tests;

// Impresora térmica KP-80A con ESC/POS: por cola de Windows (USB, RAW), directo por puerto serial,
// o por el SDK nativo del fabricante en modo USB autoreply (canal recomendado para la app oficial:
// ver AutoReplyPrintSdk.cs y "Supuestos y riesgos" en Docs/Prueba_Dia3_Impresora.md).
public static partial class HardwareTests
{
    public static void Test_PingImpresora()
    {
        var cfg = HardwareConfig.Actual.Impresora;
        var r = new Reporte($"Impresora KP-80A - Ping (modo {cfg.Modo})");
        try
        {
            if (cfg.Modo.Equals("UsbSdk", StringComparison.OrdinalIgnoreCase))
            {
                // Windows ve esta impresora como CP001/USBPort ("USB Printing Support" genuino, confirmado
                // 2026-10-17 en Propiedades > Puertos), así que CP_Port_OpenUsb es la ruta nativa correcta,
                // no CP_Port_OpenCom. El fallo anterior se sospecha del controlador de Windows reteniendo el
                // puerto mientras la cola "POS80 Printer" está instalada — ver Supuestos y riesgos.
                var h = AutoReplyPrintSdk.Abrir(cfg.NombreUsb);
                if (r.Criterio($"Apertura de impresora USB '{cfg.NombreUsb}' vía SDK (autoreply)", AutoReplyPrintSdk.EsValido(h)))
                {
                    if (AutoReplyPrintSdk.ConsultarEstado(h, 1000, out var estado))
                        Consola.Info($"        Estado: {AutoReplyPrintSdk.DescribirEstado(estado)}");
                    else
                        Consola.Aviso("        Sin respuesta de estado (sin verificación garantizada por el SDK; se avisa, no bloquea).");
                    AutoReplyPrintSdk.Cerrar(h);
                }
            }
            else if (cfg.Modo.Equals("Serial", StringComparison.OrdinalIgnoreCase))
            {
                using var puerto = new SerialPort(cfg.Puerto, cfg.BaudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 2000,
                    DtrEnable = true, // ver CcTalk.cs: algunos adaptadores necesitan esto para habilitarse
                    RtsEnable = true,
                };
                puerto.Open();
                r.Criterio($"Apertura de {cfg.Puerto} @ {cfg.BaudRate}", puerto.IsOpen);

                puerto.Write(EscPos.EstadoEnLinea, 0, EscPos.EstadoEnLinea.Length);
                Thread.Sleep(200);
                if (puerto.BytesToRead > 0)
                {
                    var estado = (byte)puerto.ReadByte();
                    r.Criterio("Impresora en línea (DLE EOT 1)", EscPos.EstaEnLinea(estado), $"estado=0x{estado:X2}");
                }
                else
                {
                    Consola.Aviso("        Sin respuesta a DLE EOT 1 (no todas las KP-80A lo responden por serial).");
                }
            }
            else
            {
                r.Criterio($"Cola '{cfg.NombreImpresora}' existe y se puede abrir", ImpresoraRaw.Existe(cfg.NombreImpresora));
            }
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    public static void Test_ImprimirVoucherPrueba()
    {
        var cfg = HardwareConfig.Actual.Impresora;
        var r = new Reporte($"Impresora KP-80A - Voucher de prueba (modo {cfg.Modo})");
        try
        {
            if (cfg.Modo.Equals("UsbSdk", StringComparison.OrdinalIgnoreCase))
            {
                ImprimirPorSdk(r, cfg);
                r.Finalizar();
                return;
            }

            var voucher = EscPos.Concatenar(
                EscPos.Inicializar,
                EscPos.Centrar,
                EscPos.NegritaOn, EscPos.Texto("Hola Mundo FINSO"), EscPos.NegritaOff, EscPos.SaltoLinea,
                EscPos.Izquierda, EscPos.Texto("Prueba de hardware - texto normal"), EscPos.SaltoLinea,
                EscPos.Texto(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), EscPos.SaltoLinea,
                EscPos.SaltoLinea, EscPos.SaltoLinea,
                EscPos.AvanzarYCortar);

            if (cfg.Modo.Equals("Serial", StringComparison.OrdinalIgnoreCase))
                EnviarPorSerial(r, cfg, voucher);
            else
            {
                ImpresoraRaw.Enviar(cfg.NombreImpresora, voucher);
                r.Criterio($"{voucher.Length} bytes entregados al spooler de '{cfg.NombreImpresora}'", true);
            }

            r.Criterio("Imprimió 'Hola Mundo FINSO' legible y en negritas", Consola.PreguntarAsistente("¿Salió un ticket con \"Hola Mundo FINSO\" en negritas?"));
            r.Criterio("El autocortador cortó el papel", Consola.PreguntarAsistente("¿El ticket salió cortado (suelto)?"));
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    // Modo "UsbSdk": usa autoreplyprint.dll (SDK del fabricante) en vez del spooler de Windows.
    // Valida lo que la app oficial necesitará: estado real antes de imprimir (papel/tapa/cortador)
    // y confirmación de que el ticket salió (QueryPrintResult), no solo que se envió el comando.
    private static void ImprimirPorSdk(Reporte r, ImpresoraConfig cfg)
    {
        var h = AutoReplyPrintSdk.Abrir(cfg.NombreUsb);
        if (!r.Criterio($"Apertura de impresora USB '{cfg.NombreUsb}' vía SDK (autoreply)", AutoReplyPrintSdk.EsValido(h)))
            return;

        try
        {
            // "0" de QueryRTStatus es ambiguo (falla de lectura o, por diseño del SDK, estado sin bits
            // encendidos): el fabricante advierte que esta instrucción "no tiene verificación, no se
            // puede garantizar que el resultado sea correcto". Por eso el criterio solo se evalúa si
            // hubo respuesta; si no hay respuesta, se avisa y se continúa (igual que el DLE EOT serial).
            if (AutoReplyPrintSdk.ConsultarEstado(h, 1000, out var estadoPrevio))
            {
                Consola.Info($"        Estado antes de imprimir: {AutoReplyPrintSdk.DescribirEstado(estadoPrevio)}");
                r.Criterio("Sin papel/tapa/cortador reportado antes de imprimir",
                    !(AutoReplyPrintSdk.SinPapel(estadoPrevio) || AutoReplyPrintSdk.TapaAbierta(estadoPrevio) || AutoReplyPrintSdk.ErrorCortador(estadoPrevio)),
                    AutoReplyPrintSdk.DescribirEstado(estadoPrevio));
            }
            else
            {
                Consola.Aviso("        Sin respuesta de estado antes de imprimir (se continúa).");
            }

            AutoReplyPrintSdk.Reset(h);
            AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionCentro);
            AutoReplyPrintSdk.Negrita(h, true);
            AutoReplyPrintSdk.Texto(h, "Hola Mundo FINSO");
            AutoReplyPrintSdk.Negrita(h, false);
            AutoReplyPrintSdk.AvanzarLineas(h, 1);
            AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionIzquierda);
            AutoReplyPrintSdk.Texto(h, "Prueba de hardware - texto normal");
            AutoReplyPrintSdk.AvanzarLineas(h, 1);
            AutoReplyPrintSdk.Texto(h, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            AutoReplyPrintSdk.AvanzarLineas(h, 3);
            r.Criterio("Comandos de texto/negrita entregados por el SDK", true);

            r.Criterio("Corte completo aceptado por el SDK", AutoReplyPrintSdk.CorteCompleto(h));

            // QueryPrintResult confirma que el trabajo anterior salió realmente, no solo que se envió.
            var salioImpreso = AutoReplyPrintSdk.ConsultarResultadoImpresion(h, 3000);
            r.Criterio("El SDK confirma que el ticket se imprimió (QueryPrintResult)", salioImpreso);

            if (AutoReplyPrintSdk.ConsultarEstado(h, 1000, out var estadoFinal))
                Consola.Info($"        Estado después de imprimir: {AutoReplyPrintSdk.DescribirEstado(estadoFinal)}");

            r.Criterio("Imprimió 'Hola Mundo FINSO' legible y en negritas", Consola.PreguntarAsistente("¿Salió un ticket con \"Hola Mundo FINSO\" en negritas?"));
            r.Criterio("El autocortador cortó el papel", Consola.PreguntarAsistente("¿El ticket salió cortado (suelto)?"));
        }
        finally { AutoReplyPrintSdk.Cerrar(h); }
    }

    // Voucher con el formato de un comprobante real de cambio de divisas (logo + datos de la operación).
    // Requiere modo UsbSdk: el logo y los tamaños de fuente usan funciones del SDK del fabricante
    // (CP_Pos_PrintRasterImageFromFile / CP_Pos_SetTextScale) que no tienen equivalente ya implementado
    // en el camino ESC/POS crudo (Raw/Serial) de este proyecto.
    public static void Test_ImprimirVoucherOperacion()
    {
        var cfg = HardwareConfig.Actual.Impresora;
        var r = new Reporte($"Impresora KP-80A - Voucher de operación (logo + formato tradicional, modo {cfg.Modo})");
        try
        {
            // El SDK (autoreplyprint.dll) no se pudo destrabar en esta máquina (ver AutoReplyPrintSdk.Abrir):
            // logo y tamaños de fuente también son comandos ESC/POS estándar (GS v 0, GS !), así que
            // Spooler/Serial los arman a mano en vez de depender del SDK para esto.
            if (cfg.Modo.Equals("UsbSdk", StringComparison.OrdinalIgnoreCase))
                ImprimirVoucherPorSdk(r, cfg);
            else
                ImprimirVoucherPorEscPos(r, cfg);
        }
        catch (Exception ex) { r.Criterio("Ejecución sin excepciones", false, ex.Message); }
        r.Finalizar();
    }

    private static void ImprimirVoucherPorEscPos(Reporte r, ImpresoraConfig cfg)
    {
        var ahora = DateTime.Now;
        var folio = ahora.ToString("yyMMdd-HHmmss");
        var montoEntregadoUsd = 100.00m;
        var tipoCambio = 18.50m;
        var comision = 0.00m;
        var montoRecibidoMxn = montoEntregadoUsd * tipoCambio - comision;

        var rutaLogo = Path.Combine(AppContext.BaseDirectory, cfg.RutaLogo);
        var logoExiste = File.Exists(rutaLogo);
        r.Criterio($"Logo encontrado en '{rutaLogo}' (Impresora.RutaLogo)", logoExiste);

        var partes = new List<byte[]> { EscPos.Inicializar, EscPos.Centrar };
        if (logoExiste)
        {
            // Mismo tamaño que en el SDK: 576 dots = ancho imprimible de 80mm @ 203dpi; 147 proporcional
            // al logo.png real (2560x655) para no deformarlo.
            var bits = EscPos.ImagenDesdeArchivo(rutaLogo, anchoDots: 576, altoDots: 147);
            partes.Add(EscPos.ImagenRaster(bits, 576, 147));
            partes.Add(EscPos.SaltoLinea);
        }

        partes.Add(EscPos.EscalaTexto(1, 1));
        partes.Add(EscPos.NegritaOn);
        partes.Add(EscPos.Texto("COMPROBANTE DE OPERACION"));
        partes.Add(EscPos.SaltoLinea);
        partes.Add(EscPos.NegritaOff);
        partes.Add(EscPos.EscalaTexto(0, 0));
        partes.Add(EscPos.Izquierda);

        var separador = new string('-', 32);
        void Linea(string texto) { partes.Add(EscPos.Texto(texto)); partes.Add(EscPos.SaltoLinea); }

        Linea(separador);
        Linea($"Fecha:        {ahora:yyyy-MM-dd HH:mm:ss}");
        Linea("Operacion:    COMPRA DE DOLARES");
        Linea(separador);
        Linea($"Entregado:    USD {montoEntregadoUsd:N2}");
        Linea("  5 x USD   20.00");
        Linea(separador);
        Linea($"Recibido:     MXN {montoRecibidoMxn:N2}");
        Linea("  3 x MXN  500.00");
        Linea("  1 x MXN  200.00");
        Linea("  1 x MXN  100.00");
        Linea(" 10 x MXN    5.00");
        Linea(separador);
        Linea($"Tipo de cambio: {tipoCambio:N2}");
        Linea($"Comision:       MXN {comision:N2}");
        Linea(separador);
        Linea("Identificacion: Nivel 2 - INE validada");
        Linea(separador);
        Linea("Terminal:     ATM-FINSO-01");
        Linea($"Folio:        {folio}");
        Linea(separador);
        partes.Add(EscPos.Centrar);
        Linea("Gracias por su preferencia");
        partes.Add(EscPos.SaltoLinea);
        partes.Add(EscPos.SaltoLinea);
        partes.Add(EscPos.AvanzarYCortar);

        var voucher = EscPos.Concatenar(partes.ToArray());
        r.Criterio("Contenido del voucher armado (logo + texto, ESC/POS crudo)", true);

        if (cfg.Modo.Equals("Serial", StringComparison.OrdinalIgnoreCase))
            EnviarPorSerial(r, cfg, voucher);
        else
        {
            ImpresoraRaw.Enviar(cfg.NombreImpresora, voucher);
            r.Criterio($"{voucher.Length} bytes entregados al spooler de '{cfg.NombreImpresora}'", true);
        }

        r.Criterio("El asistente ve el logo de FINSO impreso arriba del ticket",
            Consola.PreguntarAsistente("¿Se ve el logo de FINSO (letras azules + figura verde) arriba del ticket?"));
        r.Criterio("El asistente ve los datos de la operación con formato de voucher tradicional (fecha, operación, montos, denominaciones, tipo de cambio, identificación, terminal y folio)",
            Consola.PreguntarAsistente("¿El ticket muestra esos datos en líneas separadas, con separadores, como un comprobante normal?"));
        r.Criterio("El autocortador cortó el papel", Consola.PreguntarAsistente("¿El ticket salió cortado (suelto)?"));
    }

    private static void ImprimirVoucherPorSdk(Reporte r, ImpresoraConfig cfg)
    {
        var h = AutoReplyPrintSdk.Abrir(cfg.NombreUsb);
        if (!r.Criterio($"Apertura de impresora USB '{cfg.NombreUsb}' vía SDK (autoreply)", AutoReplyPrintSdk.EsValido(h)))
            return;

        try
        {
            AutoReplyPrintSdk.Reset(h);

            // Datos de muestra: una operación de compra de dólares consistente con los casetes reales
            // (recicladora acepta USD20, dispensa MXN 500/200/100; monederos dispensan MXN $1/$5) —
            // no viene de una transacción real, es solo para probar el formato de impresión.
            var ahora = DateTime.Now;
            var folio = ahora.ToString("yyMMdd-HHmmss");
            var montoEntregadoUsd = 100.00m;
            var tipoCambio = 18.50m;
            var comision = 0.00m;
            var montoRecibidoMxn = montoEntregadoUsd * tipoCambio - comision;

            var rutaLogo = Path.Combine(AppContext.BaseDirectory, cfg.RutaLogo);
            var logoExiste = File.Exists(rutaLogo);
            if (logoExiste)
            {
                // 576 dots = ancho imprimible estándar de 80mm @ 203dpi (72mm; el resto son márgenes
                // del cabezal). 147 = alto proporcional al 2560x655 real de logo.png, para no deformarlo.
                AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionCentro);
                r.Criterio("Logo entregado al SDK (CP_Pos_PrintRasterImageFromFile)",
                    AutoReplyPrintSdk.ImprimirImagen(h, rutaLogo, anchoDots: 576, altoDots: 147));
                AutoReplyPrintSdk.AvanzarLineas(h, 1);
            }
            else
            {
                r.Criterio($"Logo encontrado en '{rutaLogo}' (Impresora.RutaLogo)", false);
            }

            AutoReplyPrintSdk.EscalaTexto(h, 1, 1);
            AutoReplyPrintSdk.Negrita(h, true);
            AutoReplyPrintSdk.Texto(h, "COMPROBANTE DE OPERACION");
            AutoReplyPrintSdk.AvanzarLineas(h, 1);
            AutoReplyPrintSdk.Negrita(h, false);
            AutoReplyPrintSdk.EscalaTexto(h, 0, 0);
            AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionIzquierda);

            var separador = new string('-', 32);
            void Linea(string texto) { AutoReplyPrintSdk.Texto(h, texto); AutoReplyPrintSdk.AvanzarLineas(h, 1); }

            Linea(separador);
            Linea($"Fecha:        {ahora:yyyy-MM-dd HH:mm:ss}");
            Linea("Operacion:    COMPRA DE DOLARES");
            Linea(separador);
            Linea($"Entregado:    USD {montoEntregadoUsd:N2}");
            Linea("  5 x USD   20.00");
            Linea(separador);
            Linea($"Recibido:     MXN {montoRecibidoMxn:N2}");
            Linea("  3 x MXN  500.00");
            Linea("  1 x MXN  200.00");
            Linea("  1 x MXN  100.00");
            Linea(" 10 x MXN    5.00");
            Linea(separador);
            Linea($"Tipo de cambio: {tipoCambio:N2}");
            Linea($"Comision:       MXN {comision:N2}");
            Linea(separador);
            Linea("Identificacion: Nivel 2 - INE validada");
            Linea(separador);
            Linea("Terminal:     ATM-FINSO-01");
            Linea($"Folio:        {folio}");
            Linea(separador);
            AutoReplyPrintSdk.Alinear(h, AutoReplyPrintSdk.AlineacionCentro);
            Linea("Gracias por su preferencia");
            AutoReplyPrintSdk.AvanzarLineas(h, 2);
            r.Criterio("Contenido del voucher entregado al SDK", true);

            r.Criterio("Corte completo aceptado por el SDK", AutoReplyPrintSdk.CorteCompleto(h));

            var salioImpreso = AutoReplyPrintSdk.ConsultarResultadoImpresion(h, 3000);
            r.Criterio("El SDK confirma que el ticket se imprimió (QueryPrintResult)", salioImpreso);

            r.Criterio("El asistente ve el logo de FINSO impreso arriba del ticket",
                Consola.PreguntarAsistente("¿Se ve el logo de FINSO (letras azules + figura verde) arriba del ticket?"));
            r.Criterio("El asistente ve los datos de la operación con formato de voucher tradicional (fecha, operación, montos, denominaciones, tipo de cambio, identificación, terminal y folio)",
                Consola.PreguntarAsistente("¿El ticket muestra esos datos en líneas separadas, con separadores, como un comprobante normal?"));
            r.Criterio("El autocortador cortó el papel", Consola.PreguntarAsistente("¿El ticket salió cortado (suelto)?"));
        }
        finally { AutoReplyPrintSdk.Cerrar(h); }
    }

    private static void EnviarPorSerial(Reporte r, ImpresoraConfig cfg, byte[] datos)
    {
        using var puerto = new SerialPort(cfg.Puerto, cfg.BaudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 2000,
            DtrEnable = true,
            RtsEnable = true,
        };
        puerto.Open();
        r.Criterio($"Apertura de {cfg.Puerto} @ {cfg.BaudRate}", puerto.IsOpen);

        // DLE EOT 1: no todas las KP-80A lo responden por serial; si no hay respuesta solo se avisa.
        puerto.Write(EscPos.EstadoEnLinea, 0, EscPos.EstadoEnLinea.Length);
        Thread.Sleep(200);
        if (puerto.BytesToRead > 0)
        {
            var estado = (byte)puerto.ReadByte();
            r.Criterio("Impresora en línea (DLE EOT 1)", EscPos.EstaEnLinea(estado), $"estado=0x{estado:X2}");
        }
        else
        {
            Consola.Aviso("        Sin respuesta a DLE EOT 1 (se continúa con la impresión).");
        }

        puerto.Write(datos, 0, datos.Length);
        r.Criterio($"{datos.Length} bytes enviados por {cfg.Puerto}", true);
    }
}
```

## Modalidad remota (desarrollador a distancia + asistente en sitio)

En los tres modos, la prueba solo necesita al asistente al final, para confirmar lo que no se puede leer por software:

| Momento | Qué pasa en consola | Qué hace el asistente |
| :-- | :-- | :-- |
| Después de imprimir | "¿Salió un ticket con 'Hola Mundo FINSO' en negritas?" | Mira el ticket y dice "sí" o "no" (ver `Guia_Asistente_Sitio.md`, sección Impresora) |
| Después de imprimir | "¿El ticket salió cortado (suelto)?" | Lo confirma |
| `Test_ImprimirVoucherOperacion`, después de imprimir | "¿Se ve el logo de FINSO arriba del ticket?" | Mira el ticket y dice "sí" o "no" |
| `Test_ImprimirVoucherOperacion`, después de imprimir | "¿El ticket muestra fecha, operación, montos, denominaciones, tipo de cambio, identificación, terminal y folio en líneas separadas, como un comprobante normal?" | Lo confirma o dice qué falta/se ve mal |

No hay `AntesDeMover`: no hay un mecanismo donde el asistente pueda tener la mano dentro.

## Cómo ejecutar

Menú `6` (voucher de prueba), `14` (voucher de operación, logo + formato tradicional) o `15` (ping: abre/consulta estado según el modo, sin imprimir nada). Equivalente desde `Main`:

```csharp
HardwareTests.Test_PingImpresora();
HardwareTests.Test_ImprimirVoucherPrueba();
HardwareTests.Test_ImprimirVoucherOperacion();
```

## Criterios de aceptación

| ID | Criterio | Cómo se verifica |
| :-- | :-- | :-- |
| I-01 | Los bytes/comandos se entregan sin excepción (cola abierta, COM abierto o handle USB-SDK válido) | Criterio automático |
| I-02 | Modo Serial: la impresora responde a `DLE EOT 1` y está en línea | Criterio automático (si no responde, se avisa; no bloquea) |
| I-03 | Se imprime "Hola Mundo FINSO" legible, centrado y **en negritas**, con el resto del texto en normal | Inspección del voucher |
| I-04 | El autocortador corta el papel al final | Inspección del voucher |
| I-05 | Dos impresiones seguidas funcionan sin reiniciar | Repetir la prueba |
| I-06 | Con la tapa abierta o sin papel la prueba falla de forma controlada (sin colgar el programa) | Prueba negativa opcional |
| I-07 | Modo UsbSdk: antes de imprimir no reporta sin papel / tapa abierta / error de cortador | Criterio automático (`CP_Pos_QueryRTStatus`) |
| I-08 | Modo UsbSdk: el SDK confirma que el ticket se imprimió, no solo que se envió | Criterio automático (`CP_Pos_QueryPrintResult`) |
| I-13 | Opción 26 (SDK): antes de imprimir la impresora no reporta errores (`CP_Printer_GetPrinterStatusInfo`); si hay un error latente se intenta `ClearPrinterError` una vez y, si persiste, **no se imprime** | Criterio automático |
| I-14 | Opción 26 (SDK): después de imprimir, el id de página impresa que reporta la impresora cambió y no quedó ningún error | Criterio automático (`GetPrinterPrintedInfo`) |
| I-10 | El logo (`RutaLogo`) se encuentra en disco y se arma el comando de imagen sin excepción (`CP_Pos_PrintRasterImageFromFile` en UsbSdk, `GS v 0`/`ImagenDesdeArchivo` en Spooler/Serial) | Criterio automático |
| I-11 | El asistente ve el logo de FINSO legible arriba del ticket | Inspección del voucher |
| I-12 | El asistente ve los datos de la operación con formato de voucher tradicional (fecha, operación, montos, denominaciones, tipo de cambio, comisión, identificación, terminal, folio) | Inspección del voucher |

**Aprobado** = I-01, I-03, I-04 e I-05 (I-02 solo aplica en Serial; I-07/I-08 solo aplican en UsbSdk, y son los que dan certeza para el despliegue productivo). Para `Test_ImprimirVoucherOperacion`: I-10 a I-12 (ya no exige un modo específico; corre en el que esté configurado).

## Supuestos y riesgos

- **Nombre de la cola confirmado en la máquina (2026-10-14):** `POS80 Printer` (driver `POS80`, puerto `CP001`, tipo `USBPort` — confirmado en Propiedades > Puertos, 2026-10-17), no "KP-80A". Los baudios por serial siguen sin confirmar.
- **Modo `UsbSdk` probado contra hardware real (2026-10-16/17) y no se pudo destrabar — queda documentado para no repetir el mismo camino:**
  - `CP_Port_OpenUsb` detecta un dispositivo USB (log propio de la DLL: VID 0x0FE6/PID 0x811E, `szProduct="Virtual PRN"`) pero el `Open` siempre devuelve handle inválido. Se descartaron, uno por uno: el servicio Spooler de Windows corriendo (se detuvo y persistió el fallo), falta de privilegios (se corrió como administrador y persistió), y la cola `POS80 Printer` reteniendo el puerto (se intentó quitar la cola desde Windows, pero se reinstala sola por Plug & Play mientras el cable siga conectado — prueba no concluyente, no se pudo aislar del todo).
  - Se probó la alternativa `CP_Port_OpenCom` (mismo SDK, puerto serie en vez de USB): la herramienta oficial del fabricante (**Print Test Tool**, `referencias_integracion/Receipt Printer(KP-80A)/Windows SDK + Testing & Configuration Tools/Print Test Tool/`) confirmó manualmente que `COM14` abre con el mismo SDK (`115200/8/NOPARITY/ONESTOPBITS/No FlowControl`). Se corrigió un bug real encontrado en el camino — `CreateFile` (lo que usa `CP_Port_OpenCom` internamente) exige el prefijo `\\.\` para puertos COM10 en adelante, si no falla en silencio aunque el puerto exista y esté libre — pero aun con eso corregido, nuestro código seguía sin poder abrir `COM13` ni `COM14`.
  - Conclusión práctica: Windows sí reconoce la impresora como "USB Printing Support" genuino (`CP001`/`USBPort`, "This device is working properly"), así que el hardware y el cableado están bien; el bloqueo es específico de cómo esta máquina deja (o no) que `autoreplyprint.dll` tome el dispositivo. Pendiente: contactar al proveedor/BEEX con estos logs, o probar en otra máquina para aislar si es algo de esta instalación de Windows en particular.
  - **Mientras tanto, `Impresora.Modo` quedó en `Spooler`** y el logo/tamaños de fuente se reimplementaron por ESC/POS crudo (ver Objetivo) para no bloquear el avance.
- **`QueryRTStatus` puede devolver 0 por dos motivos indistinguibles:** falla de lectura o, según el propio manual del SDK, "no hay verificación, no se garantiza que el resultado sea correcto". Por eso el criterio I-07 solo se evalúa cuando hubo respuesta; si no la hay, se avisa y se continúa (igual que `DLE EOT 1` en Serial).
- El texto es ASCII; los acentos requieren seleccionar la tabla de caracteres (`ESC t n` en ESC/POS, o `CP_Pos_PrintTextInUTF8` en el SDK), fuera del alcance de la prueba.
- `GS V 66 0` es el corte parcial con avance; si el modelo no lo soporta, probar `GS V 0` o `GS V 1`. En modo UsbSdk se usa `CP_Pos_FullCutPaper` (corte completo); si el hardware no lo soporta, usar `CP_Pos_HalfCutPaper` o `CP_Pos_FeedAndHalfCutPaper`.
- **Tamaño del logo (576×147 dots) sin confirmar con una impresión física todavía (pendiente, criterios I-10 a I-12):** 576 es el ancho imprimible estándar de una impresora de 80mm a 203dpi (72mm; el resto son márgenes del cabezal — lo confirma además `EasyLabel-203dpi-...exe`, incluido en el mismo paquete del fabricante), y 147 es el alto proporcional al tamaño real de `logo.png` (2560×655 px) para no deformarlo. `EscPos.ImagenDesdeArchivo` usa un umbral simple (128) para convertir a blanco/negro — si el logo sale muy "quemado" o muy tenue, ese umbral es lo primero a ajustar. `EscPos.EscalaTexto` usa 0-7 (x1 a x8) para `GS !`, el rango estándar documentado de ESC/POS (a diferencia del equivalente del SDK, `CP_Pos_SetTextScale`, cuyo rango nunca se confirmó porque no se llegó a usar).
- Los datos de la operación en `Test_ImprimirVoucherOperacion` son de muestra fija (no vienen de una transacción real ni de los demás tests): sirven para probar el formato de impresión, no para validar aritmética de cambio de divisas.

## Evidencia a guardar

El voucher físico (pegado en el reporte) y la salida de consola.
