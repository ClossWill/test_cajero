using System.Runtime.InteropServices;

namespace IntegracionCajeroPruebas.Protocolos;

/// <summary>
/// Datos de pasaporte/visa que entrega el callback "EX" (sin los punteros crudos de imagen de
/// <c>DocData</c>). Layout y tamaños copiados tal cual de <c>TWRPassportAPI.h</c> y del wrapper
/// del propio fabricante (SDK/Example/c#/IDRead/IDRead/DLL.cs), que es la única referencia que
/// demuestra el marshaling correcto de estas cadenas de ancho fijo.
/// </summary>
public struct PassportInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name_CH;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Name_EN;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Surname;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string GivenName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string MiddleName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)] public string Sex;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)] public string Nationality;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 70)] public string BirthPlace;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 18)] public string BirthDate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 38)] public string PassportNo;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 38)] public string OtherID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string IssuePlace;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 30)] public string IssueDate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 30)] public string ExpiryDate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 72)] public string Address;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Remark;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Remark1;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Remark2;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string chMRZ1;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string chMRZ2;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string chMRZ3;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)] public string PhotoFileName;
    public bool bCheckSum;
}

/// <summary>Metadatos de la captura (tipo de documento, tamaño, si trae chip RFID) sin los buffers de imagen crudos.</summary>
public struct DocDataEX
{
    public int iType;
    public uint FaceWidth;
    public uint FaceHeight;
    public uint Width;
    public uint Height;
    public bool bRFID;
    public bool bID2;
    public int iReserve1;
    public int iReserve2;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)] public string ColorFileName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)] public string IRFileName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)] public string UVFileName;
}

/// <summary>
/// Wrapper propio sobre el SDK nativo del lector MEPR500+ (<c>TWRPassportAPI.dll</c>, carpeta
/// <c>referencias_integracion/Passport scanner(MEPR500+)/MEPR500 Windows SDK 20230815/BinTW-20230815 DEMO</c>).
/// Como con <see cref="AutoReplyPrintSdk"/>, se declaran los <c>[DllImport]</c> directamente a partir de
/// <c>TWRPassportAPI.h</c> en vez de usar el wrapper administrado del demo del fabricante.
///
/// Camino elegido: modo automático por *callback* — <c>TW_SetAutoModel(true)</c> +
/// <c>TW_Capture_RegisterCallbackResultImageEX</c> ("EX": entrega <c>PassportInfo</c> con cadenas ya
/// parseadas, en vez de los punteros crudos a buffers de imagen de la variante no-EX). <b>Ojo:</b> en el demo
/// de ejemplo (IDRead/Form1.cs) esta ruta EX solo se activa si se descomenta <c>#define API_EX</c> en la
/// línea 1 — tal como se entrega el demo, ese define está comentado y usa la variante no-EX. Es decir, la
/// ruta EX es código real y completo del fabricante (y la más práctica de marshalizar en .NET, porque ya
/// trae las cadenas de ancho fijo listas), pero no es la que corre por defecto en el demo que ellos
/// distribuyen, así que no hay evidencia de que la hayan probado tanto como la no-EX. El header también
/// declara <c>TW_MonitoringDocEX</c> + <c>TW_Capture_Manual_GetInfo</c> para un flujo síncrono por sondeo
/// (sin callback), pero ningún demo lo ejercita bajo ningún define: implementarlo sería confiar en una firma
/// de la que no hay ni un solo ejemplo funcionando. Ambos puntos quedan anotados en
/// "Supuestos y riesgos" de Docs/Prueba_Dia5_Escaner.md.
/// </summary>
public static class TwrPassportSdk
{
    private const string Dll = "TWRPassportAPI.dll";

    // El tipo de función del callback nativo usa la macro CALLBACK (= __stdcall) en el .h; la función que
    // lo registra (esta de abajo) es una función normal exportada (= __cdecl, igual que el resto del SDK).
    // Son dos convenciones distintas en la misma declaración: PassportInfoCallback no lleva
    // [UnmanagedFunctionPointer] porque el default de .NET para delegados en Windows x86 (el proyecto está
    // fijado a x86 por la DLL de GRG) ya es StdCall, igual que hace el propio wrapper del fabricante
    // (SDK/Example/c#/IDRead/IDRead/DLL.cs) sin ese atributo.
    public delegate void PassportInfoCallback(IntPtr contexto, DocDataEX datos, PassportInfo info);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int TW_Capture_RegisterCallbackResultImageEX(PassportInfoCallback callback, IntPtr context);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TW_Initialize(int nSpeed);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern bool TW_InitIDCard(string path, int[] com);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TW_SetCapture_Start();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TW_SetAutoModel(bool bAuto);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void TW_StopCapture();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TW_DeInitialize();

    // Debe quedar en un campo estático: si el delegado solo viviera como variable local, el recolector de
    // basura podría liberarlo entre el registro y la siguiente vez que la DLL nativa invoque el callback,
    // porque desde el punto de vista del GC nada administrado sigue referenciándolo.
    private static PassportInfoCallback? _callback;

    /// <summary>Se dispara en un hilo propio de la DLL nativa cada vez que detecta y captura un documento.</summary>
    public static event Action<PassportInfo>? DocumentoCapturado;

    /// <param name="velocidad">1 = Full speed, 2 = High speed (según TWRPassportAPI.h). El demo usa 2.</param>
    public static bool Inicializar(int velocidad = 2)
    {
        if (!TW_Initialize(velocidad)) return false;

        // Igual que el demo del fabricante (IDRead/Form1.cs, btnInit_Click): llama TW_InitIDCard justo después
        // de TW_Initialize. El demo tampoco aborta si devuelve false (solo lo reporta en pantalla), así que
        // aquí se ignora el resultado también; no hay documentación de qué rompe si se omite esta llamada,
        // por eso se agrega por si el SDK la necesita para dejar el subsistema completo listo.
        TW_InitIDCard("", new int[5]);

        _callback = (_, _, info) => DocumentoCapturado?.Invoke(info);
        TW_Capture_RegisterCallbackResultImageEX(_callback, IntPtr.Zero);

        if (!TW_SetCapture_Start()) return false;
        return TW_SetAutoModel(true);
    }

    public static void Detener()
    {
        TW_StopCapture();
        TW_DeInitialize();
        _callback = null;
    }
}
