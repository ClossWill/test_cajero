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

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr CP_Library_Version();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint CP_Port_EnumUsb(byte[] pBuf, uint cbBuf, out uint pcbNeeded);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint CP_Port_EnumCom(byte[] pBuf, uint cbBuf, out uint pcbNeeded);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Port_IsOpened(IntPtr handle);

    // Estado que la impresora reporta sola en modo autoreply (no hace falta consultarlo con QueryRTStatus).
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Printer_GetPrinterStatusInfo(IntPtr handle, out long errorStatus, out long infoStatus,
        out long timestampMs);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Printer_GetPrinterReceivedInfo(IntPtr handle, out uint bytesRecibidos, out long timestampMs);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Printer_GetPrinterPrintedInfo(IntPtr handle, out uint paginaImpresa, out long timestampMs);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Printer_GetPrinterFirmwareVersion(IntPtr handle, byte[] pBuf, uint cbBuf, out uint pcbNeeded);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Printer_ClearPrinterError(IntPtr handle);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CP_Printer_ClearPrinterBuffer(IntPtr handle);

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

    /// <summary>Igual que <see cref="Abrir"/> pero eligiendo el modo autoreply: el .h avisa que solo algunos modelos lo
    /// soportan, así que el diagnóstico prueba con y sin él.</summary>
    public static IntPtr AbrirUsb(string nombre, bool autoreply) => CP_Port_OpenUsb(nombre, autoreply ? 1 : 0);

    public static string Version() => Marshal.PtrToStringAnsi(CP_Library_Version()) ?? "(desconocida)";

    /// <summary>Nombres de las impresoras USB que ve el SDK (CP_Port_EnumUsb).</summary>
    public static List<string> EnumerarUsb() => Enumerar(CP_Port_EnumUsb);

    /// <summary>Puertos COM que ve el SDK (CP_Port_EnumCom).</summary>
    public static List<string> EnumerarCom() => Enumerar(CP_Port_EnumCom);

    private delegate uint EnumFn(byte[] buf, uint cb, out uint needed);

    // El .h no documenta el formato del buffer; se leen las cadenas ANSI separadas por '\0' (o por salto de línea).
    private static List<string> Enumerar(EnumFn fn)
    {
        var buf = new byte[4096];
        var n = fn(buf, (uint)buf.Length, out _);
        if (n == 0) return new List<string>();
        return System.Text.Encoding.Default.GetString(buf)
            .Split(new[] { '\0', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
    }

    public static bool EstaAbierto(IntPtr handle) => EsValido(handle) && CP_Port_IsOpened(handle) != 0;

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

    /// <summary>Firmware de la impresora; null si no responde (mismo "sin verificación garantizada" que QueryRTStatus).</summary>
    public static string? VersionFirmware(IntPtr handle)
    {
        var buf = new byte[256];
        if (CP_Printer_GetPrinterFirmwareVersion(handle, buf, (uint)buf.Length, out _) == 0) return null;
        var texto = System.Text.Encoding.Default.GetString(buf).TrimEnd('\0').Trim();
        return texto.Length == 0 ? null : texto;
    }

    /// <summary>Estado autónomo de la impresora (modo autoreply). <paramref name="errores"/> y <paramref name="info"/> son las
    /// máscaras CP_PRINTERSTATUS_ERROR_XXX / CP_PRINTERSTATUS_INFO_XXX; false si el SDK aún no recibió ninguna.</summary>
    public static bool ConsultarEstadoAutonomo(IntPtr handle, out long errores, out long info) =>
        CP_Printer_GetPrinterStatusInfo(handle, out errores, out info, out _) != 0;

    /// <summary>Bytes que la impresora ha recibido en total (para comparar con los enviados) y el id de la última página impresa.</summary>
    public static bool ConsultarContadores(IntPtr handle, out uint bytesRecibidos, out uint paginaImpresa)
    {
        paginaImpresa = 0;
        var ok = CP_Printer_GetPrinterReceivedInfo(handle, out bytesRecibidos, out _) != 0;
        ok &= CP_Printer_GetPrinterPrintedInfo(handle, out paginaImpresa, out _) != 0;
        return ok;
    }

    /// <summary>Limpia el error latente de la impresora (atasco resuelto, tapa cerrada) para poder reintentar sin reiniciarla.</summary>
    public static bool LimpiarError(IntPtr handle) => CP_Printer_ClearPrinterError(handle) != 0;

    /// <summary>Descarta lo pendiente en el buffer de la impresora (un trabajo a medias de una falla anterior).</summary>
    public static bool LimpiarBuffer(IntPtr handle) => CP_Printer_ClearPrinterBuffer(handle) != 0;

    // Máscaras de CP_PRINTERSTATUS_ERROR_XXX / CP_PRINTERSTATUS_INFO_XXX (autoreplyprint.h).
    public static string DescribirErrores(long errores)
    {
        var problemas = new List<string>();
        if ((errores & 0x01) != 0) problemas.Add("cortador");
        if ((errores & 0x02) != 0) problemas.Add("flash");
        if ((errores & 0x04) != 0) problemas.Add("sin papel");
        if ((errores & 0x08) != 0) problemas.Add("voltaje");
        if ((errores & 0x10) != 0) problemas.Add("marca de papel");
        if ((errores & 0x20) != 0) problemas.Add("motor de impresión");
        if ((errores & 0x40) != 0) problemas.Add("sobrecalentamiento");
        if ((errores & 0x80) != 0) problemas.Add("tapa abierta");
        if ((errores & 0x100) != 0) problemas.Add("motor");
        return problemas.Count == 0 ? "sin errores" : string.Join(", ", problemas);
    }

    public static bool ImpresoraOciosa(long info) => (info & 0x40) != 0; // CP_PRINTERSTATUS_INFO_PRINTIDLE
    public static bool HayDatosPendientes(long info) => (info & 0x08) != 0; // CP_PRINTERSTATUS_INFO_HAVEDATA

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
