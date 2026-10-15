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

    public const int AlineacionIzquierda = 0;
    public const int AlineacionCentro = 1;
    public const int AlineacionDerecha = 2;

    /// <summary>Abre el puerto USB en modo autoreply (la impresora reporta su estado sin que se le pregunte).
    /// <paramref name="nombre"/> puede ser cualquier texto si solo hay una impresora USB conectada
    /// (así lo documenta el SDK); null/handle inválido si no la encuentra.</summary>
    public static IntPtr Abrir(string nombre) => CP_Port_OpenUsb(nombre, autoreplymode: 1);

    public static bool EsValido(IntPtr handle) => handle != IntPtr.Zero;

    public static void Cerrar(IntPtr handle)
    {
        if (EsValido(handle)) CP_Port_Close(handle);
    }

    public static bool Reset(IntPtr handle) => CP_Pos_ResetPrinter(handle) != 0;
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
